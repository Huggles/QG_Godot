import os
from PIL import Image

# === CONFIGURATION ===
ROOT_FOLDER = r"C:\Users\Hugo\Documents\Godot\QG_Godot\assets\factions"  # Change this to your target folder
OUTPUT_FOLDER = r"C:\Users\Hugo\Documents\Godot\QG_Godot\assets\factions"  # Output folder for resized images
RESIZE_WIDTH = 500
RESIZE_HEIGHT = 450

VALID_SUFFIXES = [
    "_status.png",
    "_event.png",
    "_economicwarfare.png",
    "_response.png"
]

def is_valid_png(filename):
    lower = filename.lower()
    return (        
        not lower.endswith(".png.import") and
        any(lower.endswith(suffix) for suffix in VALID_SUFFIXES)
    )
    return (filename.endswith('.png') or filename.endswith('.PNG')) and not filename.endswith('.png.import')

def resize_top_crop(image_path, output_path):
    with Image.open(image_path) as img:
        # Crop to top-left 500x450 or adjust if image is smaller
        width, height = img.size
        crop_width = min(RESIZE_WIDTH, width)
        crop_height = min(RESIZE_HEIGHT, height)
        cropped = img.crop((0, 0, crop_width, crop_height))
        cropped.save(output_path)
        print(f"Saved resized image to: {output_path}")

def main():
    if not os.path.exists(OUTPUT_FOLDER):
        os.makedirs(OUTPUT_FOLDER)

    for root, _, files in os.walk(ROOT_FOLDER):
        for file in files:
            if is_valid_png(file):
                input_path = os.path.join(root, file)
                
                # Create mirrored directory structure in output folder
                rel_path = os.path.relpath(root, ROOT_FOLDER)
                output_dir = os.path.join(OUTPUT_FOLDER, rel_path)
                os.makedirs(output_dir, exist_ok=True)

                output_path = os.path.join(output_dir, file)
                resize_top_crop(input_path, output_path)

if __name__ == "__main__":
    main()
