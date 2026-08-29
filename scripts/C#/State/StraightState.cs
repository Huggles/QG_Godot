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
    [JsonIgnore] public TextureRect StraightSpriteNode => ControllingCountryState.CountryScene.StraightSpriteNode;

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
        // ShowStraightSprite applies the initial color itself; the tag system takes it from there.
        if (StaticStraightData.IsStraight)
            ShowStraightSprite();
        else
            HideStraightSprite();
    }

    public FactionTeam ControlledByFaction => ControllingCountryState.OccupyingTeam == FactionTeam.NONE ? FactionTeam.ALLIES : ControllingCountryState.OccupyingTeam;
    public bool IsControlledByFaction(Faction faction) => ControlledByFaction == StaticGameData.FactionTeamForFaction(faction);

    public void ShowStraightSprite()
    {
        var sprite = StraightSpriteNode;

        sprite.Texture = StaticStraightData.IconInversed ? AssetRepository.StraightIconInverse : AssetRepository.StraightIcon;
        sprite.Visible = true;

        // The team color goes through the shader's fill_color now, so the border keeps its own color
        // instead of being tinted along with the silhouette. Modulate multiplies the shader's final
        // output — border included — so it has to stay neutral for that split to hold.
        sprite.Modulate = Colors.White;
        ChangeColorTeam(ControllingCountryState.OccupyingTeam);

        // Centre of the country plus the authored offset, scaled about the icon's own middle — the
        // country owns that placement, because the rect it is centred in is the country's.
        var t = StaticStraightData.StraightTransform;
        ControllingCountryState.CountryScene.CenterSprite(sprite, t);
        sprite.RotationDegrees = t.ZRotation;
    }

    public void HideStraightSprite()
    {
        StraightSpriteNode.Visible = false;
    }

    public bool IsForIds(int countryId1, int countryId2) => (ControlledCountryId1 == countryId1 && ControlledCountryId2 == countryId2) || (ControlledCountryId1 == countryId2 && ControlledCountryId2 == countryId1);

    public void ChangeColorTeam(FactionTeam controllingTeam)
    {
        // Every country subscribes to the control tags, but only a straight ever draws the icon. Writing
        // the color anyway would have each of the others duplicate a ShaderMaterial for a sprite that
        // stays hidden for the whole game.
        if (!StaticStraightData.IsStraight) return;

        Color fill = StaticGameData.FactionTeamColor(controllingTeam == FactionTeam.NONE ? FactionTeam.ALLIES : controllingTeam);
        fill += (Colors.White / 10);
        ControllingCountryState.CountryScene.SetStraightColor(fill);
    }
}
