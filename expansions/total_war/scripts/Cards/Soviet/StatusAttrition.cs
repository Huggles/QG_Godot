using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// "During your Discard step, gain +1 Victory Point for each Build Army card you discard from your hand."
/// Passive: it runs as the Discard step ends (Rosie the Riveter runs as it begins), finds that step's
/// discard in the game's journal and scores without a prompt. No reaction window is involved.
/// </summary>
public partial class StatusAttrition : StatusCardLogic, IStepMutator
{
    public TurnStep      Step   => TurnStep.DISCARD;
    public MutatorTiming Timing => MutatorTiming.AFTER;

    /// <summary>This card is the mutator, so it announces itself with its own face rather than a Bulletin.</summary>
    public int SourceCardId => CardState.Id;

    public string Description => "Attrition: +1 Victory Point for each Build Army card discarded";

    /// <summary>
    /// The Build Army cards this country discarded in the Discard step just ended. The journal keeps every
    /// applied event; walking back stops at the step's start, so a turn with no discard finds none, and
    /// the IsDiscardStep flag leaves out other hand discards in the step.
    /// </summary>
    private List<int> DiscardedBuildArmies()
    {
        List<int> found = new();
        List<GameMessage> journal = MultiplayerSession.Instance?.GameState.GameMessages;
        if (journal == null) return found;

        for (int i = journal.Count - 1; i >= 0 && journal[i] is not ChangeStepChangeEvent; i--)
        {
            if (journal[i] is DiscardHandCardsChangeEvent { IsDiscardStep: true } discard && discard.TargetFaction == Faction)
                found.AddRange(discard.CardIds.Where(cardId => CardState.ForId(cardId).CardData.CardType == CardType.BUILD_ARMY));
        }
        return found;
    }

    /// <summary>Only the owner's own Discard step, and only when there is something to score: otherwise the card would announce itself for nothing.</summary>
    public bool ShouldRun(Faction activeFaction) => activeFaction == Faction && DiscardedBuildArmies().Count > 0;

    /// <summary>The points show over the home space: the cards came from the hand, which has no place on the map.</summary>
    public async Task Run(Faction activeFaction)
    {
        int count = DiscardedBuildArmies().Count;
        VPEntry entry = VPEntry.ForCountry(count, "Build Army cards discarded (Attrition)", FactionData.HomeSpaceCountryState.Id);
        await this.Do(new ScorePointsChangeEvent(entry, Faction), isTrigger: false);
    }
}
