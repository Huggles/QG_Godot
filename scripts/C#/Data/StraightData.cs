using Godot;
using System;

public partial class StraightData : DataObject
{
    public bool IsStraight = false;
    public bool IconInversed = false;
    public string ControlledCountry1 = null;
    public string ControlledCountry2 = null;

    public int ControlledCountry1Id {
        get {
            return CountryState.ForName(ControlledCountry1).Id;
        }
    }
    public int ControlledCountry2Id {
        get {
            return CountryState.ForName(ControlledCountry2).Id;
        }
    }
    
    public TransformData StraightTransform;


}
