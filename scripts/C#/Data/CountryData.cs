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

    public TransformData LabelTransformData { get; set; }

    public Texture2D Texture { get; private set; }

    public class DataNotFoundException : Exception { public DataNotFoundException(String message) : base(message) { } }

    /// <summary>
    /// Where the country's top-left corner sits on the board — the authored value straight from the
    /// data file, which is a Control's Position as-is. It is NOT the middle of the country: offsets
    /// that ARE authored from the middle (unit slots, the supply star, the straight icon) add half
    /// the texture themselves.
    /// </summary>
    public Vector2 WorldPositionTopLeft => new Vector2(WorldTransformData.XPosition, WorldTransformData.YPosition);

    public override void LoadData()
    {
        // A headless/dedicated server has no rendering and does not import textures — skip entirely.
        if (GameContext.IsHeadless) return;
        var texturePath = $"res://assets/textures/Countries/{UniqueNameCamelCase}.png";
        Texture = GD.Load<Texture2D>(texturePath);
        if (Texture == null)
        {
            throw new DataNotFoundException($"Error loading texture: {texturePath}");
        }
        
    }
}
