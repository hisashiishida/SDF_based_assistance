import nrrd                                    
import numpy.linalg as la
import click
from pathlib import Path


@click.command()
@click.option('--input', default="Path", help='number of greetings')
def get_nrrd_info(input):
    print (Path(input))
    d, h = nrrd.read(Path(input))    
    dir = h['space directions']                    

    s = h['sizes']                                 

    dims = [0, 0, 0]
    dims_max = 0
    for i in range(3): 
        dims[i]= s[i+1] * la.norm(dir[1:4, i]) 

        if dims_max < dims[i]:
            dims_max = dims[i]

    for i in range(3):
        dims[i] = dims[i]/dims_max

    print("dim is :", dims)
    # print("Spacial Direction: ", dir)
    print("Unit: ", 1.0/dims_max)

if __name__ == "__main__":
    get_nrrd_info()