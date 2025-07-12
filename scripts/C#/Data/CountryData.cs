using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class CountryData : DataObject
{
    public string UniqueName { get; set; }
    public string UniqueNameCamelCase { get; set; }
    public string Label { get; set; }
    public int Number { get; set; }
    public int Type { get; set; }
    public string IsSupply { get; set; }    
    public List<string> Neighbors { get; set; }

    public StraightData StraightData { get; set; }
    public TransformData WorldTransformData { get; set; }
    public TransformData SupplyStarTransformData { get; set; }
    public UnitTransformData UnitTransformData { get; set; }

    public Texture2D Texture { get; private set; }

    public class DataNotFoundException : Exception { public DataNotFoundException(String message) : base(message) { } }

    public Vector2 WorldPositionCenterUnscaled
    {
        get
        {
            return new Vector2(
                WorldTransformData.XPosition,
                WorldTransformData.YPosition
            );
        }
    }

    public Vector2 WorldPositionCenter
    {
        get
        {
            const float scale = 0.5f;
            return new Vector2(
                (WorldPositionCenterUnscaled.X) * scale,
                (WorldPositionCenterUnscaled.Y) * scale
            );
        }
    }

    public override void LoadData()
    {
        var texturePath = $"res://assets/textures/Countries/{UniqueNameCamelCase}.png";
        Texture = GD.Load<Texture2D>(texturePath);
        if (Texture == null)
        {
            throw new DataNotFoundException($"Error loading texture: {texturePath}");
        }
        
    }
}
