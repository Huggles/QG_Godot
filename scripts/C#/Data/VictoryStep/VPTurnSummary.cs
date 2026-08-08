using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class VPTurnSummary
{
    public int TurnNumber { get; private set; } = -1;
    public List<VPEntry> victoryPointEntries { get; set; }

    private Faction? scoringFaction;

    /// <summary>
    /// Who these points belong to. Defaults to the faction whose turn <see cref="TurnNumber"/> is,
    /// which is correct for the VP step and for cards scoring on their own turn. Set explicitly when
    /// the points land on another faction — e.g. the empty-deck discard penalty, which hits the
    /// discard target while the attacker is the active faction.
    /// </summary>
    public Faction Faction
    {
        get => scoringFaction ?? StaticGameData.PlayableFactions[(TurnNumber-1) % StaticGameData.PlayableFactions.Count];
        set => scoringFaction = value;
    }
    /// <summary>The round this summary's turn falls in. Several summaries can share a round.</summary>
    public int Round => StaticGameData.RoundForTurn(TurnNumber);
    public int TotalScore => victoryPointEntries.Map(vpe => vpe.VictoryPoints).Sum();

    /// <remarks>
    /// Keep this the only public constructor: System.Text.Json binds the parameters by name, which is
    /// how a summary round-trips through ScorePointsChangeEventDto. A second ctor breaks that binding.
    /// Faction.NONE (not a nullable parameter) is the "unset" sentinel because STJ also requires the
    /// parameter type to match the property type — a Faction? parameter does not bind to a Faction
    /// property and makes every client throw on deserialization.
    /// </remarks>
    public VPTurnSummary(int turnNumber, Faction faction = Faction.NONE)
    {
        victoryPointEntries = new List<VPEntry>();
        TurnNumber = turnNumber;
        scoringFaction = faction == Faction.NONE ? null : faction;
    }

    public void AddScore(VPEntry victoryPointEntry)
    {
        victoryPointEntries.Add(victoryPointEntry);
    }
}
