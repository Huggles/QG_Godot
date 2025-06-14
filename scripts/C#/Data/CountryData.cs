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

    public override async Task LoadData()
    {
        var texturePath = $"res://assets/textures/Countries/{UniqueNameCamelCase}.png";        
        Texture = GD.Load<Texture2D>(texturePath);
        if (Texture == null)
        {
            throw new DataNotFoundException($"Error loading texture: {texturePath}");            
        }
    }
}
