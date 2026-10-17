using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The Air step (rulebook p. 6), run after the Play step. The country may take one action, paid for by
/// discarding a card: Deploy (an Air Power card), Marshal (any card) or Air Superiority (an Air Power card).
/// The UK and US may then also marshal the French and Chinese Air Force. Every action goes through the
/// step's card round, so cards can block or react to it.
/// </summary>
public static class AirStep
{
    private const int Pass = 0, Deploy = 1, Marshal = 2, AirSuperiority = 3;

    public static async Task Run(Faction faction)
    {
        await TakeAction(faction);
        foreach (Faction minor in StaticGameData.MinorsOf(faction))
            await MarshalMinor(faction, minor);
    }

    private static async Task TakeAction(Faction faction)
    {
        // Asked on public facts only. Asking only when the hand held an Air Power card would tell everyone it does;
        // a Pass-only prompt is instead answered on the player's own client (AirStepRequestHandler.Handle).
        if (DeckState.ForFaction(faction).HandCardIds.Count == 0) return;
        BoardState board = BoardState.Live;
        if (board.SuppliedAirForceIds(faction).Count == 0 && !(HasAirForces(faction) && board.AirDeployTargets(faction).Count > 0))
            return;

        // A selection backed out of before anything was paid returns to this choice; passing it ends the step.
        while (true)
        {
            int action = await Choose(faction, Actions(faction), "Air step: take one action, or pass");
            bool done = action switch
            {
                Deploy         => await TryDeploy(faction),
                Marshal        => await TryMarshal(faction, faction),
                AirSuperiority => await TryAirSuperiority(faction),
                _              => true,
            };
            if (done) return;
        }
    }

    /// <summary>The UK or US marshalling its minor ally's Air Force, in addition to its own action.</summary>
    private static async Task MarshalMinor(Faction patron, Faction minor)
    {
        string airForce = $"the {FactionState.ForEnum(minor).FactionData.FactionAdjactiveLabel} Air Force";
        while (DeckState.ForFaction(patron).HandCardIds.Count > 0 && BoardState.Live.SuppliedAirForceIds(minor).Count > 0)
        {
            List<AirAction> options = new()
            {
                new(Marshal, $"Marshal {airForce} (discard any card)", Card(Marshal, $"Marshal {airForce}",
                    $"Discard any card: move {airForce} to a space with one of its supplied Armies or Navies.",
                    unitIds: BoardState.Live.SuppliedAirForceIds(minor))),
                new(Pass, "Pass"),
            };
            if (await Choose(patron, options, $"Air step: {minor.Label()}") != Marshal || await TryMarshal(patron, minor))
                return;
        }
    }

    /// <summary>One choice of the Air step prompt: its label for the CLI and bots, and the card the GUI draws for it (none for Pass, which is the Skip button).</summary>
    private sealed record AirAction(int Id, string Label, InputRequest.OptionCard Card = null)
    {
        public bool Available => Card?.Available ?? true;
    }

    /// <summary>All three actions and Pass. One that cannot be taken now is drawn greyed out and is not an answer.</summary>
    private static List<AirAction> Actions(Faction faction)
    {
        BoardState board = BoardState.Live;
        bool hasAirPower = AirPowerCards(faction).Count > 0;

        return new List<AirAction>
        {
            new(Deploy, "Deploy an Air Force (discard an Air Power card)", Card(Deploy, "Deploy an Air Force",
                "Discard an Air Power card: place an Air Force with one of your supplied Armies or Navies.",
                countryIds: board.AirDeployTargets(faction),
                available: hasAirPower && HasAirForces(faction) && board.AirDeployTargets(faction).Count > 0)),
            new(Marshal, "Marshal an Air Force (discard any card)", Card(Marshal, "Marshal an Air Force",
                "Discard any card: move one of your supplied Air Forces to a space with one of your supplied Armies or Navies.",
                unitIds: board.SuppliedAirForceIds(faction),
                available: board.SuppliedAirForceIds(faction).Count > 0)),
            new(AirSuperiority, "Air Superiority (discard an Air Power card)", Card(AirSuperiority, "Air Superiority",
                "Discard an Air Power card: eliminate an enemy Air Force adjacent to one of your supplied Air Forces.",
                countryIds: board.AirSuperiorityTargets(faction),
                available: hasAirPower && board.AirSuperiorityTargets(faction).Count > 0)),
            new(Pass, "Pass"),
        };
    }

    // An unavailable option lights nothing on hover: its targets would suggest an action that cannot be taken.
    private static InputRequest.OptionCard Card(int id, string title, string text,
        List<int> countryIds = null, List<int> unitIds = null, bool available = true) => new()
    {
        OptionId = id, Title = title, Text = text, ArtCardType = CardType.AIR_POWER, Available = available,
        TargetCountryIds = available ? countryIds : null, TargetUnitIds = available ? unitIds : null,
    };

    private static async Task<bool> TryDeploy(Faction faction)
    {
        int card = await PickCard(faction, AirPowerCards(faction), "Discard an Air Power card to deploy an Air Force");
        if (card < 0) return false;
        int country = await PickCountry(faction, BoardState.Live.AirDeployTargets(faction));
        if (country < 0) return false;

        await Discard(faction, card);
        await CardPlayPool.DoChangeEvent(new DeployAirForceChangeEvent(faction, country));
        return true;
    }

    /// <summary>Marshal one of <paramref name="owner"/>'s Air Forces, paid for from <paramref name="payer"/>'s hand: the card, which one, then where to.</summary>
    private static async Task<bool> TryMarshal(Faction payer, Faction owner)
    {
        int card = await PickCard(payer, DeckState.ForFaction(payer).HandCardIds, "Discard a card to marshal the Air Force");
        if (card < 0) return false;
        int airForce = await PickUnit(payer, BoardState.Live.SuppliedAirForceIds(owner), alwaysAsk: true);
        if (airForce < 0) return false;
        int country = await PickCountry(payer, BoardState.Live.MarshalTargets(UnitState.ForId(airForce)));
        if (country < 0) return false;

        await Discard(payer, card);
        await CardPlayPool.DoChangeEvent(new MoveAirForceChangeEvent(owner, airForce, country, AirForceMoveReason.MARSHAL));
        return true;
    }

    private static async Task<bool> TryAirSuperiority(Faction faction)
    {
        int card = await PickCard(faction, AirPowerCards(faction), "Discard an Air Power card for Air Superiority");
        if (card < 0) return false;
        int country = await PickCountry(faction, BoardState.Live.AirSuperiorityTargets(faction));
        if (country < 0) return false;
        List<int> enemies = BoardState.Live.EnemyAirForcesIn(faction, CountryState.ForId(country));
        int target = await PickUnit(faction, enemies);
        if (enemies.Count > 0 && target < 0) return false;

        await Discard(faction, card);
        ChangeEvent action = target < 0
            ? new AirSuperiorityChangeEvent(faction, country)
            : new RemoveUnitChangeEvent(faction, target, UnitRemovalReason.AIR_SUPERIORITY);
        await CardPlayPool.DoChangeEvent(action);
        return true;
    }

    /// <summary>The chosen option's id; Pass for a skip, a release, or an answer that was not offered.</summary>
    private static async Task<int> Choose(Faction faction, List<AirAction> options, string title)
    {
        // Only the available options are answers; the GUI draws the rest greyed out beside them.
        List<AirAction> offered = options.Where(option => option.Available).ToList();
        try
        {
            InputRequest answer = await new InputRequest.AirStepRequestHandler(faction,
                offered.Select(option => option.Label).ToList(), offered.Select(option => option.Id).ToList(), title)
            {
                OptionCards = options.Where(option => option.Card != null).Select(option => option.Card).ToList(),
            }.BroadCast();
            return answer.ResponseCardIds.Where(id => offered.Any(option => option.Id == id)).DefaultIfEmpty(Pass).First();
        }
        catch (StepSkippedException)
        {
            return Pass;
        }
    }

    /// <summary>
    /// The card to discard. Required: the action is chosen by now, so the player pays rather than backing out.
    /// -1 only when the host releases the prompt. Identical cards (Air Power) are taken without asking.
    /// </summary>
    private static async Task<int> PickCard(Faction faction, List<int> cardIds, string title)
    {
        if (cardIds.Count == 0) return -1;
        if (cardIds.Select(id => CardState.ForId(id).CardData.UniqueName).Distinct().Count() == 1) return cardIds[0];
        try
        {
            InputRequest answer = await new InputRequest.SelectCardRequestHandler(faction, cardIds, title, required: true).BroadCast();
            return answer.ResponseCardIds.Where(cardIds.Contains).DefaultIfEmpty(-1).First();
        }
        catch (StepSkippedException)
        {
            return -1;
        }
    }

    /// <summary>The chosen unit, -1 when there is none or the player backs out. A single unit is taken without asking unless <paramref name="alwaysAsk"/>.</summary>
    private static async Task<int> PickUnit(Faction faction, List<int> unitIds, bool alwaysAsk = false)
    {
        if (unitIds.Count == 0) return -1;
        if (unitIds.Count == 1 && !alwaysAsk) return unitIds[0];
        try
        {
            InputRequest answer = await new InputRequest.SelectUnitRequestHandler(faction, unitIds).BroadCast();
            return answer.ResponseUnitIds.Where(unitIds.Contains).DefaultIfEmpty(-1).First();
        }
        catch (StepSkippedException)
        {
            return -1;
        }
    }

    /// <summary>The chosen space, or -1 when the player backs out. Always asked: it is where the action lands.</summary>
    private static async Task<int> PickCountry(Faction faction, List<int> countryIds)
    {
        if (countryIds.Count == 0) return -1;
        try
        {
            InputRequest answer = await new InputRequest.SelectCountryRequestHandler(faction, countryIds) { MarkRebuildTargets = false }.BroadCast();
            return answer.ResponseCountryIds.Where(countryIds.Contains).DefaultIfEmpty(-1).First();
        }
        catch (StepSkippedException)
        {
            return -1;
        }
    }

    /// <summary>The action's cost. Not a trigger, like a Discard-step discard.</summary>
    private static Task Discard(Faction faction, int cardId) =>
        CardPlayPool.DoChangeEvent(new DiscardHandCardsChangeEvent(faction, faction, new List<int> { cardId }) { IsTrigger = false });

    private static List<int> AirPowerCards(Faction faction) => DeckState.ForFaction(faction).HandCardIds
        .Where(id => CardState.ForId(id).CardData.CardType == CardType.AIR_POWER)
        .ToList();

    private static bool HasAirForces(Faction faction) =>
        FactionState.ForEnum(faction).AllUnits.Any(unitId => UnitState.ForId(unitId).IsAirForce);
}
