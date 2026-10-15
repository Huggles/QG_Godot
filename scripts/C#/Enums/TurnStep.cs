using Godot;
using System;

/// <summary>Appended to only: saves and the wire carry these values. AIR is only in a turn whose RuleSet adds it.</summary>
public enum TurnStep
{
    START, PLAY_CARD, SUPPLY, VICTORY_POINT, DISCARD, DRAW, END, AIR
}
