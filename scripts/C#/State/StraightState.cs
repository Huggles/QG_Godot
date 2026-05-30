using Godot;
using System;

public partial class StraightState : StateObject
{
    public StraightData StaticStraightData;
    public int ControllingCountryId { get; set; }
    public int ControlledCountryId1 { get; set; }
    public int ControlledCountryId2 { get; set; }

    public CountryState ControllingCountryState => CountryState.ForId(ControllingCountryId);
    public CountryState ControlledCountryState1 => CountryState.ForId(ControlledCountryId1);
    public CountryState ControlledCountryState2 => CountryState.ForId(ControlledCountryId2);
    public Sprite2D StraightSpriteNode => ControllingCountryState.CountryScene.StraightSpriteNode;

    public StraightState(int controllingCountryId, StraightData staticStraightData)
    {
        StaticStraightData = staticStraightData;
        ControllingCountryId = controllingCountryId;

        if (staticStraightData.IsStraight)
        {
            ControlledCountryId1 = staticStraightData.ControlledCountry1Id;
            ControlledCountryId2 = staticStraightData.ControlledCountry2Id;
        }

        // Subscribe to tag events for visual updates
        Tags.TagAdded += (Tag t, Faction f) =>
        {
            if (t == Tag.AxisControlled)
                ChangeColorTeam(FactionTeam.AXIS);
            else if (t == Tag.AlliesControlled)
                ChangeColorTeam(FactionTeam.ALLIES);
        };
        
        Tags.TagRemoved += (Tag t, Faction f) =>
        {
            if (t == Tag.AxisControlled || t == Tag.AlliesControlled)
                ChangeColorTeam(FactionTeam.NONE);
        };
    }

    public void OnReady()
    {
        if (StaticStraightData.IsStraight)
            ShowStraightSprite();
        else
            HideStraightSprite();

        // Initial color will be set by tag system
        ChangeColorTeam(ControllingCountryState.OccupyingTeam);
    }

    public FactionTeam ControlledByFaction()
    {
        return ControllingCountryState.OccupyingTeam;
    }

    public void ShowStraightSprite()
    {
        var sprite = StraightSpriteNode;

        sprite.Texture = StaticStraightData.IconInversed ? AssetRepository.StraightIconInverse : AssetRepository.StraightIcon;
        sprite.Visible = true;

        var t = StaticStraightData.StraightTransform;
        sprite.Position = new Vector2(t.XPosition, t.YPosition);
        sprite.RotationDegrees = 0;
        sprite.Scale = new Vector2(t.Scale, t.Scale);
    }

    public void HideStraightSprite()
    {
        StraightSpriteNode.Visible = false;
    }

    public void ChangeColorTeam(FactionTeam controllingTeam)
    {
        var sprite = StraightSpriteNode;

        switch (controllingTeam)
        {
            case FactionTeam.AXIS:
                sprite.Modulate = Colors.Red;
                break;
            case FactionTeam.ALLIES:
                sprite.Modulate = Colors.DarkBlue;
                break;
            case FactionTeam.NONE:
                sprite.Modulate = Colors.Blue;
                break;
        }
    }
}
