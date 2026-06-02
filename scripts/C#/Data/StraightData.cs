using Godot;
using System;
using System.Text.Json.Serialization;

public partial class StraightData : DataObject
{
    public bool IsStraight { get; set; } = false;
    public bool IconInversed { get; set; } = false;
    public string ControlledCountry1 { get; set; } = null;
    public string ControlledCountry2 { get; set; } = null;

    [JsonIgnore] public int ControlledCountry1Id => CountryState.ForName(ControlledCountry1)?.Id ?? -1;
    [JsonIgnore] public int ControlledCountry2Id => CountryState.ForName(ControlledCountry2)?.Id ?? -1;
    
    public TransformData StraightTransform { get; set; } = new TransformData();
}
