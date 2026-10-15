using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ResponseHeavyTanks : ResponseCardLogic
{
    private const int CHOICE_DISCARD = 0;
    private const int CHOICE_SPARE   = 1;

    /// <summary>Set by the first step when the attacker won't (or can't) pay; read by the block step.</summary>
    private bool _attackerRefused;

    public override TargetSet Targets() => BlockTargets();

    protected override List<Condition> CardTriggers() => new()
    {
        Condition.Build(new Condition.IsBlockRequest(), this),
        Condition.Build(new Condition.CustomCondition(s =>
            s.BlockTrigger is BattleUnitChangeEvent battle && battle.UnitState.Faction == Faction && battle.UnitState.IsArmy), this)
    };

    /// <summary>The attacker chooses: discard 4 cards from hand, or the battle fails. With fewer than 4 in hand it fails outright.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, async () => {
            _attackerRefused = true;
            Faction attacker = BlockContext.TriggeringFaction;
            if (DeckState.ForFaction(attacker).HandCardIds.Count < 4) return CardStepResult.Nothing;

            var choice = await new InputRequest.SelectOptionRequestHandler(attacker,
                new List<string> { "Discard 4 cards from your hand", "Leave the Soviet Army in place" },
                new List<int> { CHOICE_DISCARD, CHOICE_SPARE },
                $"Heavy Tanks: {attacker.Label()} must choose").BroadCast();
            if (choice.ResponseCardIds[0] != CHOICE_DISCARD) return CardStepResult.Nothing;

            _attackerRefused = false;
            return new ForceDiscardHandCardsChangeEvent(Faction, attacker, 4);
        })
        .WithGuidance("Make the attacker discard 4 cards from its hand, or the battle fails"),

        new BlockStep<RemoveUnitChangeEvent>(this, async _ => {
            if (!_attackerRefused) return CardStepResult.Nothing;
            await new ShowActionLabelPresentationEvent(Faction, $"{Faction.WithPlayer()} plays Heavy Tanks: the Soviet Army is not removed").Apply();
            await Task.Delay(GameSettings.DurationMedium);
            return CardStepResult.Block();
        })
    };
}
