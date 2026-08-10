using Godot;
using System;

public partial class AssetRepository : Node
{
    public const string ArmySpritePath = "res://assets/textures/Units/QGArmyDetailed.png";
    public const string NavySpritePath = "res://assets/textures/Units/QGNavyDetailed.png";

    public static readonly PackedScene UserInterfaceScenePacked = GD.Load<PackedScene>("res://scenes/userinterface/user_interface.tscn");
    public static readonly PackedScene FactionInfoRowScenePacked = GD.Load<PackedScene>("res://scenes/userinterface/FactionInfo/FactionInfoRow.tscn");
    public static readonly PackedScene multiplayerSessionScenePacked = GD.Load<PackedScene>("res://scenes/network/MultiplayerSession.tscn");
    public static readonly PackedScene PlayerScenePackged = GD.Load<PackedScene>("res://scenes/Player/Player.tscn");
    public static readonly PackedScene UnitScenePacked = GD.Load<PackedScene>("res://scenes/Units/Unit.tscn");    
    public static readonly PackedScene GameHistoryItemScenePackaged = GD.Load<PackedScene>("res://scenes/userinterface/History/game_history_item.tscn");
    public static readonly Texture2D ArmySprite = GD.Load<Texture2D>(ArmySpritePath);
    public static readonly Texture2D NavySprite = GD.Load<Texture2D>(NavySpritePath);    
    public static readonly Texture2D StraightIcon = GD.Load<Texture2D>("res://assets/textures/Other/StraightIcon.png");
    public static readonly Texture2D StraightIconInverse = GD.Load<Texture2D>("res://assets/textures/Other/StraightIconInverse.png");
    public static readonly Texture2D TargetSprite = GD.Load<Texture2D>("res://assets/textures/Other/target.png");
    public static readonly Texture2D TargetCountrySprite = GD.Load<Texture2D>("res://assets/textures/Other/target_country.png");

    // Materials
    public ShaderMaterial normalShaderMaterial = GD.Load<ShaderMaterial>("res://assets/materials/unit_shader_material.tres").Duplicate() as ShaderMaterial;
    public ShaderMaterial outOfSupplyShaderMaterial = GD.Load<ShaderMaterial>("res://assets/materials/UnitOutOfSupplyShaderMaterial.tres").Duplicate() as ShaderMaterial;

    
}
