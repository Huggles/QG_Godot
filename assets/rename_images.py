import os

# Define mappings for factions and card types
factions = {
    "japan": "Japan",
    "soviet": "Soviet",
    "italy": "Italy",
    "united_states": "United_States"
}

card_types = {
    "buildarmy": "BuildArmy",
    "buildnavy": "BuildNavy",
    "landbattle": "LandBattle",
    "seabattle": "SeaBattle",
    "event": "Event",
    "status": "Status",
    "economicwarfare": "EconomicWarfare",
    "response": "Response",
    "cardback": "CardBack"
}

def rename_card_files(folder_path):
    for root, _, files in os.walk(folder_path):
        for filename in files:
            lower_name = filename.lower()
            ext = ""

            # Determine extension
            if lower_name.endswith(".png.import"):
                ext = ".png.import"
                base_name = filename[:-11]  # Remove '.png.import'
            elif lower_name.endswith(".png"):
                ext = ".png"
                base_name = filename[:-4]  # Remove '.png'
            else:
                continue  # Not a target file

            base_name_lower = base_name.lower()

            # Find matching faction
            matched_faction = None
            for key in factions:
                if base_name_lower.startswith(key + "_"):
                    matched_faction = key
                    break

            if not matched_faction:
                continue  # Skip files with unknown faction

            # Extract and map card type
            card_part = base_name_lower[len(matched_faction) + 1:]
            if card_part not in card_types:
                continue  # Skip unknown card type

            # Construct new filename
            new_filename = f"{factions[matched_faction]}_{card_types[card_part]}{ext}"

            old_path = os.path.join(root, filename)
            new_path = os.path.join(root, new_filename)

            if old_path != new_path:
                os.rename(old_path, new_path)
                print(f"Renamed: {old_path} -> {new_path}")



# Example usage:
# Replace this with your actual folder path
folder_path = r"C:\Users\Hugo\Documents\Godot\QG_Godot\assets\factions"
rename_card_files(folder_path)