using Godot;
using System;

public partial class StraightState : StateObject
{
    private static readonly Texture2D StraightIcon = GD.Load<Texture2D>("res://assets/textures/Other/StraightIcon.png");
    private static readonly Texture2D StraightIconInverse = GD.Load<Texture2D>("res://assets/textures/Other/StraightIconInverse.png");

    public StraightData StaticStraightData;
    public int ControllingCountryId { get; set; }

    public CountryState ControllingCountryState =>
        CountryState.ForId(ControllingCountryId);

    public int ControlledCountryId1;
    public CountryState ControlledCountryState1 =>
        CountryState.ForId(ControlledCountryId1);

    public int ControlledCountryId2;
    public CountryState ControlledCountryState2 =>
        CountryState.ForId(ControlledCountryId2);

    public Sprite2D StraightSpriteNode =>
        ControllingCountryState.Node.StraightSpriteNode;

    public StraightState(int controllingCountryId, StraightData staticStraightData)
    {
        StaticStraightData = staticStraightData;
        ControllingCountryId = controllingCountryId;

        if (staticStraightData.IsStraight)
        {
            ControlledCountryId1 = staticStraightData.ControlledCountry1Id;
            ControlledCountryId2 = staticStraightData.ControlledCountry2Id;
        }
    }

    public void OnReady()
    {
        if (StaticStraightData.IsStraight)
            ShowStraightSprite();
        else
            HideStraightSprite();

        RecalculateControlledBy();
    }

    public void RecalculateControlledBy()
    {
        ChangeColorTeam(ControllingCountryState.OccupyingTeam);
    }

    public FactionTeam ControlledByFaction()
    {
        return ControllingCountryState.OccupyingTeam;
    }

    public void ShowStraightSprite()
    {
        var sprite = StraightSpriteNode;

        sprite.Texture = StaticStraightData.IconInversed ? StraightIconInverse : StraightIcon;
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
