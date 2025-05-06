using Godot;
using System;
using System.Collections.Generic;

public partial class CountryData : DataObject
{
    public string UniqueName { get; set; }
    public string UniqueNameCamelCase { get; set; }
    public string Label { get; set; }
    public int Number { get; set; }
    public int Type { get; set; }
    public string IsSupply { get; set; }    
    public List<string> Neighbors { get; private set; } = new List<string>();    

    public StraightData StraightData { get; set; }
    public TransformData WorldTransformData { get; set; }
    public TransformData SupplyStarTransformData { get; set; }
    public UnitTransformData UnitTransformData { get; set; }

    public Texture2D Texture { get; private set; }    

    public Vector3 WorldPositionCenterUnscaled
    {
        get
        {
            return new Vector3(
                WorldTransformData.XPosition,
                -WorldTransformData.YPosition,
                WorldTransformData.ZPosition
            );
        }
    }

    public Vector3 WorldPositionCenter
    {
        get
        {
            const float scale = 4f;
            return new Vector3(
                (WorldPositionCenterUnscaled.X / 100f) * scale,
                (WorldPositionCenterUnscaled.Y / 100f) * scale,
                WorldPositionCenterUnscaled.Z + 1f
            ) - new Vector3(149, -50, 0);
        }
    }

    

    public void Init(Godot.Collections.Dictionary jsonObject)
    {
        _SetNeighborCountries(jsonObject);

        var texturePath = $"res://assets/textures/Countries/{NameToCamelCase()}.png";
        Texture = GD.Load<Texture2D>(texturePath);
        if (Texture == null)
        {
            GD.PrintErr($"Error loading texture: {texturePath}");
        }
    }

    private void _SetNeighborCountries(Godot.Collections.Dictionary jsonObject)
    {
        Neighbors.Clear();
        for (int n = 0; n < 10; n++)
        {
            var key = $"neighbor{n + 1}";
            if (jsonObject.ContainsKey(key))
            {
                string value = jsonObject[key].As<string>();
                if (value != null)
                {
                    Neighbors.Add(value.ToString());
                }
            }
        }
    }

    private string NameToCamelCase()
    {
        // Placeholder for name_camel_case conversion logic
        // Adjust according to your actual naming scheme
        return UniqueName; // If `Name` is already camelCase
    }
}
