#include "EdtReader.h"
#include <iostream>
#include <stdexcept>

#include "lodepng.h"

using namespace std;

void encodeTwoSteps(const char* filename, std::vector<unsigned char>& image, unsigned width, unsigned height) {
  std::vector<unsigned char> png;

  unsigned error = lodepng::encode(png, image, width, height);
  if(!error) lodepng::save_file(png, filename);

  //if there's an error, display it
  if(error) std::cout << "encoder error " << error << ": "<< lodepng_error_text(error) << std::endl;
}

int main()
{
    cout << "Edt Image generation example\n";

    char CT_scan [80];
    char structure[80];
    char resolution[5];
    printf ("Enter your the CT scan name(ex.RT147): ");
    scanf ("%79s",&CT_scan);  
    printf("Enter the structure name: ");
    scanf ("%79s", &structure);
    printf ("Enter resolution: ");
    scanf ("%4s",&resolution);
    
    char input_file_name[80];

    sprintf(input_file_name, "../../edt_grids_%s_%s/%s.edt", &resolution,&CT_scan, &structure);
    // string structure = "../../edt_grids_512_RT147/Bone";
    // string file_name = "../../edt_grids_512_RT147/Bone.edt";
    
    char error_msg[100];
    sprintf(error_msg, "Reading %s", &input_file_name);
    cout << error_msg << endl;

    float *values_buffer;
    unsigned int res[3];

    Array3d<float> *edt_grid;
    edt_reader(input_file_name, &values_buffer, res);
    
    edt_grid = new Array3d<float>(values_buffer, res);
    
    cout << "Resolution:" << res[0] << ","  << res[1] << "," << res[2] << endl;

    unsigned width = res[0], height = res[1];
    std::vector<unsigned char> image;
    image.resize(width * height * 4);
    int z=0;
    double max_edt = 0;
    int scale = res[0];
    for (z = 0; z < res[2]; z++){
        for(unsigned y = 0; y < height; y++){
            for(unsigned x = 0; x < width; x++) {
                double tmp = (*edt_grid)(x,y,z) * 255/scale;
                if (tmp > max_edt) max_edt =tmp/255 * scale;
                if (tmp > 255) cout << "[ERROR]edt distance over "<< scale  << " mm(" << max_edt << ")" << endl;

                if (tmp > 0) image[4 * width * y + 4 * x + 0] = tmp; //R
                if (tmp < 0) image[4 * width * y + 4 * x + 2] = -tmp; //B
                // image[4 * width * y + 4 * x + 2] = 0; //G
                image[4 * width * y + 4 * x + 3] = 255;  //A
            }
        }

   
        string index = to_string(z);
        // string resolution = to_string(res[1]);
        char out_filename[100];
        // sprintf(out_filename, "./../../edt_grids_512_RT147/%s_%s/edtplane_%s.png",&structure, &resolution, &index[0]);
        sprintf(out_filename, "./../../edt_grids_%s_%s/%s_%s/edtplane_%s.png", &resolution,&CT_scan,&structure, &resolution, &index[0]);
        encodeTwoSteps(out_filename, image, width, height);
    }

    
    return 0;
}