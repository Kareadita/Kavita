import os
from fontTools.ttLib import TTFont

# Function to convert a font file to woff2
def convert_to_woff2(font_path):
    font = TTFont(font_path)
    font.flavor = 'woff2'
    new_path = os.path.splitext(font_path)[0] + '.woff2'
    font.save(new_path)
    return new_path

def main():
    # Get the current directory of the script
    current_directory = os.path.dirname(os.path.abspath(__file__))

    # Search for all OTF files recursively in the current directory
    for root, _, files in os.walk(current_directory):
        for file in files:
            if file.lower().endswith(('.otf', '.ttf')) and not file.lower().endswith(('.woff', '.woff2')):
                font_file = os.path.join(root, file)
                new_path = convert_to_woff2(font_file)
                print(f"Converted {font_file} to {new_path}")

if __name__ == "__main__":
    main()
