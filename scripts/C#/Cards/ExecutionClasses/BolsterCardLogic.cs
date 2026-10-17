using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A Bolster card: a reaction used straight from hand, then discarded. Its CardTriggers() are its use
/// conditions while in hand (CardLogic.ConditionsOn), it answers the same block and after-reaction windows
/// a table card does, and CardPlayRound.DoCard introduces it with UseBolsterChangeEvent, which spends no
/// play. A Bolster with no steps is never offered (CanBeActivated needs an executable step).
/// </summary>
public abstract partial class BolsterCardLogic : CardLogic
{
    public override List<CardStep> OnActivate() => new();

    /// <summary>Never the turn's play, so a "beginning of your Play step" Bolster leaves the hand playable.</summary>
    public override bool IsFreePlayStepActivation => true;
}
