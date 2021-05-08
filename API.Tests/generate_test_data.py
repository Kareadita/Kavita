""" This script should be run on a directory which will generate a test case file
    that can be loaded into the renametest.py"""
import os
from pathlib import Path
import shutil

verbose = False

def print_log(val):
    if verbose:
        print(val)


def create_test_base(file, root_dir):
    """ Creates and returns a new base directory for data creation for a given testcase."""
    base_dir = os.path.split(file.split('-testcase.txt')[0])[-1]
    print_log('base_dir: {0}'.format(base_dir))
    new_dir = os.path.join(root_dir, base_dir)
    print_log('new dir: {0}'.format(new_dir))
    p = Path(new_dir)
    if not p.exists():
        os.mkdir(new_dir)

    return new_dir



def generate_data(file, root_dir):
    ''' Generates directories and fake files for testing against '''

    base_dir = ''
    if file.endswith('-testcase.txt'):    
        base_dir = create_test_base(file, root_dir)
    
    files_to_create = []
    with open(file, 'r') as in_file:
        files_to_create = in_file.read().splitlines() 

    for filepath in files_to_create:
        for part in os.path.split(filepath):
            part_path = os.path.join(base_dir, part)
            print_log('Checking if {0} exists '.format(part_path))
            p = Path(part_path)

            if not p.exists():
                print_log('Creating: {0}'.format(part))

                if p.suffix != '':
                    with open(os.path.join(root_dir, base_dir + '/' + filepath), 'w+') as f:
                        f.write('')
                else:
                    os.mkdir(part_path)

def clean_up_generated_data(root_dir):
    for root, dirs, files in os.walk(root_dir):
        for dir in dirs:
            shutil.rmtree(os.path.join(root, dir))
        for file in files:
            if not file.endswith('-testcase.txt'):
                print_log('Removing {0}'.format(os.path.join(root, file)))
                os.remove(os.path.join(root, file))


def generate_test_file():
    root_dir = os.path.abspath('.')
    current_folder = os.path.split(root_dir)[-1]
    out_files = []
    for root, _, files in os.walk(root_dir):
        for file in files:
            if not file.endswith('-testcase.txt'):
                filename = os.path.join(root.replace(root_dir, ''), file) # root_dir or root_dir + '//'? 
                out_files.append(filename)

    with open(os.path.join(root_dir, current_folder + '-testcase.txt'), 'w+') as f:
        for filename in out_files:
            f.write(filename + '\n')

if __name__ == '__main__':
    verbose = True
    generate_test_file()