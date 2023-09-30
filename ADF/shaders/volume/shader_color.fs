uniform vec3 uMinCorner;
uniform vec3 uMaxCorner;
uniform vec3 uTextureScale;
uniform vec3 uGradientDelta;
uniform sampler3D uVolume;
uniform sampler3D sdfVolume;
uniform sampler3D sdfVolume1;
uniform sampler3D sdfVolume2;
uniform float uIsosurface;
uniform float uResolution;

varying vec4 vPosition;
uniform bool uSmoothVolume;
uniform int uSmoothingLevel;

vec3 dx = vec3(uGradientDelta.x, 0.0, 0.0);
vec3 dy = vec3(0.0, uGradientDelta.y, 0.0);
vec3 dz = vec3(0.0, 0.0, uGradientDelta.z);


//----------------------------------------------------------------------
// Finds the entering intersection between a ray e1+d and the volume's
// bounding box.
//----------------------------------------------------------------------

float entry(vec3 e1, vec3 d)
{
    float t = distance(uMinCorner, uMaxCorner);

    vec3 a = (uMinCorner - e1) / d;
    vec3 b = (uMaxCorner - e1) / d;
    vec3 u = min(a, b);

    return max( max(-t, u.x), max(u.y, u.z) );
}


float isoValue(vec3 tc){
  return texture3D(uVolume, tc).a;
}

//----------------------------------------------------------------------
// Estimates the intensity gradient of the volume in model space
//----------------------------------------------------------------------

vec3 gradient(vec3 tc)
{
    vec3 nabla = vec3(
        isoValue(tc + dx) - isoValue(tc - dx),
        isoValue(tc + dy) - isoValue(tc - dy),
        isoValue(tc + dz) - isoValue(tc - dz)
    );

    return (nabla / uGradientDelta) * uTextureScale;
}


//----------------------------------------------------------------------
//  Performs interval bisection and returns the value between a and b
//  closest to isosurface. When s(b) > s(a), direction should be +1.0,
//  and -1.0 otherwise.
//----------------------------------------------------------------------

vec3 refine(vec3 a, vec3 b, float isosurface, float direction)
{
    for (int i = 0; i < 6; ++i)
    {
        vec3 m = 0.5 * (a + b);
        float v = (texture3D(uVolume, m).a - isosurface) * direction;
        if (v >= 0.0)   b = m;
        else            a = m;
    }
    return b;
}


//----------------------------------------------------------------------
//  Computes phong shading based on current light and material
//  properties.
//---------------------------------------------------------------------- 

vec3 shade(vec3 p, vec3 v, vec3 n)
{
    vec4 lp = gl_ModelViewMatrixInverse * gl_LightSource[0].position;
    vec3 l = normalize(lp.xyz - p * lp.w);
    vec3 h = normalize(l+v);
    float cos_i = max(dot(n, l), 0.0);
    float cos_h = max(dot(n, h), 0.0);

    vec3 Ia = gl_FrontLightProduct[0].ambient.rgb;
    vec3 Id = gl_FrontLightProduct[0].diffuse.rgb * cos_i;
    vec3 Is = gl_FrontLightProduct[0].specular.rgb * pow(cos_h, gl_FrontMaterial.shininess);

    return (Ia + Id + Is);
}


//----------------------------------------------------------------------
//  Main fragment shader code.
//----------------------------------------------------------------------

void main(void)
{
    vec4 camera = gl_ModelViewMatrixInverse * vec4(0.0, 0.0, 0.0, 1.0);
    vec3 raydir = normalize(vPosition.xyz - camera.xyz);

    float t_entry = entry(vPosition.xyz, raydir);
    t_entry = max(t_entry, -distance(camera.xyz, vPosition.xyz));

    // estimate a reasonable step size
    float t_step = distance(uMinCorner, uMaxCorner) / uResolution;
    vec3 tc_step = uTextureScale * (t_step * raydir);

    // cast the ray (in model space)
    vec4 sum = vec4(0.0);
    vec3 tc = gl_TexCoord[0].stp + t_entry * tc_step / t_step;

    for (float t = t_entry; t < 0.0; t += t_step, tc += tc_step)
    {
        // sample the volume for intensity (red channel)
        float intensity = isoValue(tc);
        vec3 nabla;
        if (intensity > uIsosurface)
        {
            vec3 tcr = refine(tc - tc_step, tc, uIsosurface, 1.0);

            if (uSmoothVolume){
              // //Smoothing
              nabla = vec3(0., 0., 0.);
              vec3 tr;
              int cnt = uSmoothingLevel;
              vec3 half_i = vec3(1., 1., 1.) * float(cnt)/2.0;
              // vec3 tcr = tc;
              for (int x = 0 ; x < cnt ; x++){
                for (int y = 0 ; y < cnt ; y++){
                  for (int z = 0 ; z < cnt ; z++){
                    tr[0] = tcr[0] + (float(x) - half_i[0]) * uGradientDelta[0];
                    tr[1] = tcr[1] + (float(y) - half_i[1]) * uGradientDelta[1];
                    tr[2] = tcr[2] + (float(z) - half_i[2]) * uGradientDelta[2];
                    nabla += gradient(tr);
                  }
                }
              }
            }
            else{
              nabla = gradient(tcr);
            }

            float dt = length(tcr - tc) / length(tc_step);
            vec3 position = vPosition.xyz + (t - dt * t_step) * raydir;
            vec3 normal = -normalize(nabla);
            vec3 view = -raydir;
            vec3 colour = shade(position, view, normal) * texture3D(uVolume, tcr).rgb / uIsosurface;
            sum = vec4(colour, 1.0);

            // calculate fragment depth
            vec4 clip = gl_ModelViewProjectionMatrix * vec4(position, 1.0);
            gl_FragDepth = (gl_DepthRange.diff * clip.z / clip.w + gl_DepthRange.near + gl_DepthRange.far) * 0.5;


            // vec3 reference_point = vec3(0.3, 0.3,0.0);
            // if(position.x > 0.0 && position.y > 0.0 && position.z > 0.0)
            // if(length(position.xyz - reference_point) < 0.35)
            // {
            //   sum.r += 5.0 * ( 0.35 - length(position.xyz - reference_point));
            // }
            
            vec3 sdf_t = tcr;

            float thres = 0.1;
            // float sdf_distance = texture3D(sdfVolume, tcr).r - texture3D(sdfVolume, tcr).b;
            float sdf_distance = texture3D(sdfVolume, sdf_t).r - texture3D(sdfVolume, sdf_t).b;
            float sdf_distance1 = texture3D(sdfVolume1, sdf_t).r - texture3D(sdfVolume1, sdf_t).b;
            float sdf_distance2 = texture3D(sdfVolume2, sdf_t).r - texture3D(sdfVolume2, sdf_t).b;
            // if (sdf_distance > -0.001 && sdf_distance < 0.001)

            // if(false)
            // if (sum.r > 0.37  && sum.g > 0.22  && sum.b > 0.0 && sum.b < 0.2)
            if (sum.g < 0.3  && sum.g > 0.08 && sum.r > 0.1  &&  sum.b > 0.2)
            // if (sum.g > 0.8)
            // if ((sdf_distance1 > 0.001 && sdf_distance1 < 0.02) ||(sdf_distance2 > 0.001 && sdf_distance2 < 0.02))
            {
              // if (sdf_distance > 0.02 && sdf_distance1 > 0.02 && sdf_distance2 > 0.01)
              // {
              //   sum.r = 0.0;
              //   sum.g = 0.8;
              //   sum.b = 0.0;
              // }

              // else if ((sdf_distance < 0.02 && sdf_distance > 0.01) || (sdf_distance2 < 0.02 && sdf_distance2 > 0.01))
              // {
              //   sum.r = 0.8;
              //   sum.g = 0.8;
              //   sum.b = 0.0;
              // }
              // else if ((sdf_distance < 0.03 && sdf_distance > 0.01) || (sdf_distance1 < 0.03 && sdf_distance1 > 0.01))
              // {
              //   sum.r = 0.8;
              //   sum.g = 0.8;
              //   sum.b = 0.0;
              // }


              if ((sdf_distance < 0.03 && sdf_distance > 0.0) || (sdf_distance1 < 0.01 && sdf_distance1 > 0.0) || (sdf_distance1 < 0.01 && sdf_distance1 > 0.0))
              {
                sum.r = 1.0;
                sum.g = 0.0;
                sum.b = 0.0;
              }
              // else if (sdf_distance1 < 0.01 && sdf_distance1 > 0.0) 
              // {
              //   sum.r = 1.0;
              //   sum.g = 0.0;
              //   sum.b = 0.0;
              // }
              
              // else if (sdf_distance2 < 0.01 && sdf_distance2 > 0.0)
              // {
              //   sum.r = 1.0;
              //   sum.g = 0.0;
              //   sum.b = 0.0;
              // }

            }
              


            // sum.r += - (sdf_distance);
            // sum.r += 5.0;
            break;
        }
    }

    // discard the fragment if no geometry was intersected
    if (sum.a <= 0.0) discard;

    gl_FragColor = sum;
}
