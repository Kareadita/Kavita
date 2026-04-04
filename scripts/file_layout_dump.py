"""
Script to generate a test case json from an actual file system. Forward to users to help debug scanner issues

Should be ran from the library root
"""
import os
import json


def map_files(root_dir):
    files_map = []

    for dirpath, dirnames, filenames in os.walk(root_dir):
        # Skip directories that start with "."
        dirnames[:] = [d for d in dirnames if not d.startswith('.')]

        for filename in filenames:
            # Skip files that start with "."
            if not filename.startswith('.'):
                # Get the relative path of the file
                relative_path = os.path.relpath(os.path.join(dirpath, filename), root_dir)
                files_map.append(relative_path)

    # Export the map to a JSON file
    with open('files_map.json', 'w') as outfile:
        json.dump(files_map, outfile, indent=4)


if __name__ == "__main__":
    root_dir = os.getcwd()
    map_files(root_dir)
    print("File map generated and saved to files_map.json.")
