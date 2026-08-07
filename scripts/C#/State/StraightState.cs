using Godot;
using System;
using System.Text.Json.Serialization;

public partial class StraightState : StateObject
{
    [JsonIgnore] public StraightData StaticStraightData;
    public int ControllingCountryId { get; set; }
    public int ControlledCountryId1 { get; set; }
    public int ControlledCountryId2 { get; set; }

    [JsonIgnore] public CountryState ControllingCountryState => CountryState.ForId(ControllingCountryId);
    [JsonIgnore] public CountryState ControlledCountryState1 => CountryState.ForId(ControlledCountryId1);
    [JsonIgnore] public CountryState ControlledCountryState2 => CountryState.ForId(ControlledCountryId2);
    [JsonIgnore] public Sprite2D StraightSpriteNode => ControllingCountryState.CountryScene.StraightSpriteNode;

    public StraightState(int controllingCountryId, StraightData staticStraightData)
    {
        StaticStraightData = staticStraightData;
        ControllingCountryId = controllingCountryId;

        if (staticStraightData.IsStraight)
        {
            ControlledCountryId1 = staticStraightData.ControlledCountry1Id;
            ControlledCountryId2 = staticStraightData.ControlledCountry2Id;
        }

        // Subscribe to tag events for visual updates. These tags are added by GameStateCalculator
        // (which runs on the authoritative server), and the handlers touch the straight's Sprite2D
        // via CountryScene — absent on a headless server (the getter throws) — so skip them there.
        if (!GameContext.IsHeadless)
        {
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

    public FactionTeam ControlledByFaction => ControllingCountryState.OccupyingTeam == FactionTeam.NONE ? FactionTeam.ALLIES : ControllingCountryState.OccupyingTeam;
    public bool IsControlledByFaction(Faction faction) => ControlledByFaction == StaticGameData.FactionTeamForFaction(faction);

    public void ShowStraightSprite()
    {
        var sprite = StraightSpriteNode;

        sprite.Texture = StaticStraightData.IconInversed ? AssetRepository.StraightIconInverse : AssetRepository.StraightIcon;
        sprite.Visible = true;

        var t = StaticStraightData.StraightTransform;
        sprite.Position = new Vector2(t.XPosition, t.YPosition);
        sprite.RotationDegrees = t.ZRotation;
        sprite.Scale = new Vector2(t.Scale, t.Scale);
    }

    public void HideStraightSprite()
    {
        StraightSpriteNode.Visible = false;
    }

    public bool IsForIds(int countryId1, int countryId2) => (ControlledCountryId1 == countryId1 && ControlledCountryId2 == countryId2) || (ControlledCountryId1 == countryId2 && ControlledCountryId2 == countryId1);

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
