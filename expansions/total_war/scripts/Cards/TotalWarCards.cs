using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Board questions the Total War cards share. A card that doesn't name France or China never acts on
/// their pieces, so "Allied" and "Axis" here mean the six base countries; a card that names a minor
/// country passes that faction instead.
/// </summary>
public static class TotalWarCards
{
    /// <summary>The team's base countries' pieces of one type standing in these spaces.</summary>
    public static List<UnitState> TeamUnitsIn(BoardState board, FactionTeam team, UnitType type, IEnumerable<Country> countries)
    {
        HashSet<int> ids = countries.Select(country => (int)country).ToHashSet();
        return StaticGameData.FactionsForTeam(team)
            .SelectMany(board.ActiveUnits)
            .Where(unit => unit.Type == type && ids.Contains(board.CountryOf(unit)))
            .ToList();
    }

    /// <summary>A sea space with "Pacific" in its name.</summary>
    public static bool IsPacificSea(CountryState country) => country.IsSea && country.Label.Contains("Pacific");

    public static List<Country> PacificSeas => CountryState.AllCountryStates.Where(IsPacificSea).Select(country => country.Country).ToList();

    /// <summary>The faction's Armies, Navies and Air Forces on the board.</summary>
    public static List<UnitState> Pieces(BoardState board, Faction faction) =>
        board.ActiveUnits(faction).Concat(UnitState.ForIds(board.ActiveAirForceIds(faction))).ToList();

    /// <summary>The pieces within <paramref name="spaces"/> spaces of <paramref name="country"/>, counted on the map alone.</summary>
    public static List<UnitState> Within(BoardState board, IEnumerable<UnitState> pieces, Country country, int spaces) =>
        pieces.Where(unit => PathFindingService.IsWithinGeographicDistance(board.CountryOf(unit), (int)country, spaces)).ToList();

    /// <summary>Where this card's last build or recruit this round went, or null: the space of "the piece just built".</summary>
    public static CountryState DeployedBy(GameSituation situation, CardLogic card) =>
        situation.Pool.OfType<DeployUnitChangeEvent>().LastOrDefault(deploy => deploy.SourceCardId == card.CardState.Id)?.CountryState;

    /// <summary>The spaces where <paramref name="faction"/> may build or recruit a piece of this type now, limited to <paramref name="countries"/> when given.</summary>
    public static List<CountryState> DeployTargets(BoardState board, Faction faction, DeployType type, UnitType unitType, IEnumerable<Country> countries = null)
    {
        List<CountryState> open = (type, unitType) switch
        {
            (DeployType.BUILD, UnitType.ARMY) => board.BuildableLand(faction),
            (DeployType.BUILD, _) => board.BuildableSea(faction),
            (_, UnitType.ARMY) => board.RecruitableLand(faction),
            _ => board.RecruitableSea(faction)
        };
        return countries == null ? open : open.Where(country => countries.Contains(country.Country)).ToList();
    }

    /// <summary>
    /// "Build/Recruit an Army or Navy in X" for the card's own country: offers the open spaces and is
    /// skipped when there are none, or when the card's "To use" test <paramref name="canUse"/> fails.
    /// The card's faction is read when the step runs: OnActivate builds steps before the card is dealt to a country.
    /// </summary>
    public static CardStep DeployStep(CardLogic card, DeployType type, UnitType unitType, IReadOnlyCollection<Country> countries,
        string guidance, Func<BoardState, bool> canUse = null) =>
        Deploy(card, () => card.Faction, type, unitType, countries, guidance, canUse);

    /// <summary>As <see cref="DeployStep"/>, for a country the card names: France, China, or an ally.</summary>
    public static CardStep DeployStepFor(CardLogic card, Faction faction, DeployType type, UnitType unitType, IReadOnlyCollection<Country> countries,
        string guidance, Func<BoardState, bool> canUse = null) =>
        Deploy(card, () => faction, type, unitType, countries, guidance, canUse);

    private static CardStep Deploy(CardLogic card, Func<Faction> faction, DeployType type, UnitType unitType, IReadOnlyCollection<Country> countries,
        string guidance, Func<BoardState, bool> canUse) =>
        new ResultStep(card, Choose.CountryFrom(c => DeployTargets(c.Board, faction(), type, unitType, countries).ToCountryIds(),
                (countryId, _) => new DeployUnitChangeEvent(faction(), countryId, type)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                (canUse == null || canUse(s.Board)) && DeployTargets(s.Board, faction(), type, unitType, countries).Count > 0), card))
            .WithGuidance(guidance);

    /// <summary>"Battle X" by the card's own country: offers <paramref name="targets"/>; skipped when there are none or <paramref name="canUse"/> fails.</summary>
    public static CardStep BattleStep(CardLogic card, Func<GameSituation, List<BattleTarget>> targets, string guidance,
        Func<BoardState, bool> canUse = null) =>
        Battle(card, () => card.Faction, targets, guidance, canUse);

    /// <summary>As <see cref="BattleStep"/>, with a country the card names doing the battling, e.g. France beside its own pieces.</summary>
    public static CardStep BattleStepFor(CardLogic card, Faction attacker, Func<GameSituation, List<BattleTarget>> targets, string guidance,
        Func<BoardState, bool> canUse = null) =>
        Battle(card, () => attacker, targets, guidance, canUse);

    private static CardStep Battle(CardLogic card, Func<Faction> attacker, Func<GameSituation, List<BattleTarget>> targets, string guidance,
        Func<BoardState, bool> canUse) =>
        new ResultStep(card, Choose.BattleTargetFrom(c => targets(c.Situation), (target, c) => target.ToAttackChangeEvent(attacker(), c.Board)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => (canUse == null || canUse(s.Board)) && targets(s).Count > 0), card))
            .WithGuidance(guidance);

    /// <summary>
    /// "Shuffle your draw deck": the host draws the order from GameRandom and the reorder event carries it,
    /// as the opening shuffle does. Free-form, not Choose.Fixed, whose factory a preview may run and must not draw.
    /// </summary>
    public static CardStep ShuffleStep(CardLogic card) =>
        new RequirementStep(card, async () => {
            List<int> order = new(DeckState.ForFaction(card.Faction).DeckCardIds);
            GameRandom.Shuffle(order);
            await Task.CompletedTask;
            return new ReorderDeckChangeEvent(card.Faction, order);
        })
        .WithGuidance("Shuffle its draw deck");

    /// <summary>
    /// The faction's draw deck, up to <paramref name="perName"/> cards per name in name order: copies are interchangeable,
    /// and the list says nothing of the deck's order. A pick of several cards needs as many copies to take two of a name.
    /// </summary>
    public static List<int> DeckChoices(BoardState board, Faction faction, Func<CardData, bool> filter = null, int perName = 1) =>
        board.ForFaction(faction).Deck
            .Where(cardId => filter == null || filter(CardState.ForId(cardId).CardData))
            .GroupBy(cardId => CardState.ForId(cardId).CardData.UniqueName)
            .OrderBy(sameName => CardState.ForId(sameName.First()).CardData.Label)
            .SelectMany(sameName => sameName.Take(perName))
            .ToList();

    /// <summary>"Use at the beginning of your Play step": your Play step with the play unspent, as ResponseMobileForce. Pair with IsFreePlayStepActivation.</summary>
    public static List<Condition> AtPlayStepStart(CardLogic card) => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), card),
        Condition.Build(new Condition.IsFactionTurn(card.Faction), card)
    };

    /// <summary>"Use at the beginning of your [step]", plus the card's "To use" test: the window GameFlow opens as the step starts.</summary>
    public static List<Condition> AtStepStart(CardLogic card, TurnStep step, Func<GameSituation, bool> canUse) => new()
    {
        Condition.Build(new Condition.AtStepStart(step), card),
        Condition.Build(new Condition.CustomCondition(canUse), card)
    };

    /// <summary>Where <paramref name="faction"/> may deploy an Air Force now, limited to <paramref name="countries"/> when given; none while its pool is empty.</summary>
    public static List<int> AirDeployTargets(BoardState board, Faction faction, IEnumerable<Country> countries = null) =>
        board.AvailableUnitCount(faction, UnitType.AIR_FORCE) == 0
            ? new()
            : board.AirDeployTargets(faction).Where(countryId => countries == null || countries.Contains(CountryState.ForId(countryId).Country)).ToList();

    /// <summary>"Deploy an [X] Air Force [in these spaces]", for the card's own country or one it names. Skipped when there is nowhere to go.</summary>
    public static CardStep AirDeployStep(CardLogic card, Faction faction, string guidance, IReadOnlyCollection<Country> countries = null) =>
        new ResultStep(card, Choose.CountryFrom(c => AirDeployTargets(c.Board, faction, countries), (countryId, _) => new DeployAirForceChangeEvent(faction, countryId)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => AirDeployTargets(s.Board, faction, countries).Count > 0), card))
            .WithGuidance(guidance);

    /// <summary>Where a supplied Chinese Air Force could gain air superiority over an enemy Air Force: the Air step's targets, less the empty spaces.</summary>
    public static List<int> ChineseAirSuperiorityTargets(BoardState board) => board.AirSuperiorityTargets(Faction.CHINA)
        .Where(countryId => board.EnemyAirForcesIn(Faction.CHINA, CountryState.ForId(countryId)).Count > 0).ToList();

    /// <summary>
    /// "Deploy a Chinese Air Force, or a Chinese Air Force may gain air superiority" (Operation Zet, Flying Tigers): one step,
    /// the half to use picked first when both are open. Air superiority goes through the round as in the Air step, so cards react to it.
    /// </summary>
    public static CardStep ChineseAirStep(CardLogic card)
    {
        const int deploy = 0, airSuperiority = 1;
        return new ResultStep(card, async () => {
            List<int> deployIds = AirDeployTargets(BoardState.Live, Faction.CHINA);
            List<int> superiorityIds = ChineseAirSuperiorityTargets(BoardState.Live);
            List<string> labels = new();
            List<int> ids = new();
            if (deployIds.Count > 0) { labels.Add("Deploy a Chinese Air Force"); ids.Add(deploy); }
            if (superiorityIds.Count > 0) { labels.Add("A Chinese Air Force gains air superiority"); ids.Add(airSuperiority); }

            int choice = ids.Count == 1 ? ids[0]
                : (await new InputRequest.SelectOptionRequestHandler(card.Faction, labels, ids, card.CardState.CardData.Label).BroadCast()).ResponseCardIds[0];
            if (choice == deploy)
            {
                int countryId = (await new InputRequest.SelectCountryRequestHandler(card.Faction, deployIds).BroadCast()).ResponseCountryIds[0];
                return new DeployAirForceChangeEvent(Faction.CHINA, countryId);
            }
            int targetCountry = (await new InputRequest.SelectCountryRequestHandler(card.Faction, superiorityIds).BroadCast()).ResponseCountryIds[0];
            List<int> enemies = BoardState.Live.EnemyAirForcesIn(Faction.CHINA, CountryState.ForId(targetCountry));
            int target = enemies.Count == 1 ? enemies[0]
                : (await new InputRequest.SelectUnitRequestHandler(card.Faction, enemies).BroadCast()).ResponseUnitIds[0];
            return new RemoveUnitChangeEvent(Faction.CHINA, target, UnitRemovalReason.AIR_SUPERIORITY);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => CanUseChineseAir(s.Board)), card))
        .WithGuidance("Deploy a Chinese Air Force, or gain air superiority with one");
    }

    public static bool CanUseChineseAir(BoardState board) =>
        AirDeployTargets(board, Faction.CHINA).Count > 0 || ChineseAirSuperiorityTargets(board).Count > 0;

    /// <summary>The Response cards in <paramref name="owner"/>'s discard pile, less <paramref name="card"/> (a used Bolster lies there too).</summary>
    public static List<int> DiscardedResponses(BoardState board, Faction owner, CardLogic card) => board.ForFaction(owner).Discarded
        .Where(cardId => cardId != card.CardState.Id && CardState.ForId(cardId).CardData.CardType == CardType.RESPONSE).ToList();

    /// <summary>
    /// "Place a Response card from [owner]'s discard pile face down on the table": back to hand, then placed as a play
    /// that spends no play (PlayCardChangeEvent only takes a card from hand or deck). Both halves open no window.
    /// </summary>
    public static List<CardStep> ResponseFromDiscardSteps(CardLogic card, Faction owner, string guidance, Func<GameSituation, bool> canUse = null) => new()
    {
        new RequirementStep(card, async () => {
            List<int> options = DiscardedResponses(BoardState.Live, owner, card);
            int chosen = options.Count == 1 ? options[0]
                : (await new InputRequest.SelectCardsRequestHandler(card.Faction, options, guidance, 1, 1).BroadCast()).ResponseCardIds.FirstOrDefault(-1);
            if (chosen < 0) throw new StepSkippedException();
            return new RecycleCardChangeEvent(card.Faction, owner, chosen, RecycleDestination.Hand);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            DiscardedResponses(s.Board, owner, card).Count > 0 && (canUse == null || canUse(s))), card))
        .WithGuidance(guidance),

        // The card the first half took back, read from the round's pool; a free-form step, as bot previews may run a fixed one early.
        new RequirementStep(card, async () => {
            await Task.CompletedTask;
            int recycled = CardPlayPool.ChangeEventsPool.OfType<RecycleCardChangeEvent>()
                .LastOrDefault(recycle => recycle.SourceCardId == card.CardState.Id)?.CardId ?? -1;
            if (recycled < 0) throw new StepSkippedException();
            return new PlayCardChangeEvent(recycled) { CountsAsPlay = false };
        })
        .RequiringPreviousStep()
        .WithGuidance(guidance)
    };

    // ── Bolster costs and triggers ──────────────────────────────────────────

    /// <summary>The faction's hand cards of this type.</summary>
    public static List<int> InHand(BoardState board, Faction faction, CardType type) =>
        board.ForFaction(faction).Hand.Where(cardId => CardState.ForId(cardId).CardData.CardType == type).ToList();

    /// <summary>The faction's hand without <paramref name="card"/>: what a Bolster still in hand can pay with.</summary>
    public static int OtherHandCards(BoardState board, CardLogic card) =>
        board.ForFaction(card.Faction).Hand.Count(cardId => cardId != card.CardState.Id);

    /// <summary>
    /// "Discard a [type] card from your hand", a cost that shows other players only the type
    /// (DiscardHandCardsChangeEvent.RevealedType). Copies of one name are interchangeable, so the player picks
    /// only when the cards differ (Responses, EW cards). The card's "To use" test goes here, on its first step.
    /// </summary>
    public static CardStep TypedDiscardStep(CardLogic card, CardType type, string guidance, Func<GameSituation, bool> canUse = null) =>
        new RequirementStep(card, async () => {
            List<int> options = InHand(BoardState.Live, card.Faction, type);
            int chosen = options.Select(id => CardState.ForId(id).CardData.UniqueName).Distinct().Count() <= 1
                ? options.FirstOrDefault(-1)
                : (await new InputRequest.SelectCardsRequestHandler(card.Faction, options, guidance, 1, 1).BroadCast()).ResponseCardIds.FirstOrDefault(-1);
            if (chosen < 0) throw new StepSkippedException();
            return new DiscardHandCardsChangeEvent(card.Faction, card.Faction, new List<int> { chosen }) { RevealedType = type };
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            InHand(s.Board, card.Faction, type).Count > 0 && (canUse == null || canUse(s))), card))
        .WithGuidance(guidance);

    /// <summary>"Discard [count] cards from your hand", the player's choice; counted without the Bolster paying it.</summary>
    public static CardStep HandDiscardStep(CardLogic card, int count, string guidance, Func<GameSituation, bool> canUse = null) =>
        new RequirementStep(card, Choose.Fixed(_ => new ForceDiscardHandCardsChangeEvent(card.Faction, card.Faction, count)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
                OtherHandCards(s.Board, card) >= count && (canUse == null || canUse(s))), card))
            .WithGuidance(guidance);

    /// <summary>"Discard the top [count] cards of your draw deck" as a cost, with the card's "To use" test.</summary>
    public static CardStep DeckDiscardStep(CardLogic card, int count, string guidance, Func<GameSituation, bool> canUse = null) =>
        new RequirementStep(card, Choose.Fixed(_ => new ForceDiscardCardsChangeEvent(card.Faction, card.Faction, count)))
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(s => canUse == null || canUse(s)), card))
            .WithGuidance(guidance);

    /// <summary>
    /// Where <paramref name="faction"/>'s Air Force went in <paramref name="trigger"/>, a deploy or a marshal (not a
    /// reposition), or null: "that Air Force" of "when you deploy or marshal an Air Force".
    /// </summary>
    public static CountryState AirForceMovedTo(ChangeEvent trigger, Faction faction) => trigger switch
    {
        DeployAirForceChangeEvent deploy when deploy.TriggeringFaction == faction => CountryState.ForId(deploy.CountryId),
        MoveAirForceChangeEvent move when move.Reason == AirForceMoveReason.MARSHAL && UnitState.ForId(move.UnitId).Faction == faction
            => CountryState.ForId(move.ToCountryId),
        _ => null
    };

    /// <summary>
    /// The Economic Warfare card <paramref name="faction"/> played this round, or null: "when you play an EW card",
    /// answered in the windows that card's own steps open (playing a card opens none).
    /// </summary>
    public static CardState EconomicWarfarePlayed(GameSituation situation, Faction faction) => situation.Pool.OfType<PlayCardChangeEvent>()
        .LastOrDefault(play => play.TriggeringFaction == faction && play.SourceCardState.CardData.CardType == CardType.ECONOMIC_WARFARE)
        ?.SourceCardState;

    /// <summary>The first card of this type in the faction's hand, or -1. Build Army and Land Battle cards are all alike, so any one pays a cost.</summary>
    public static int FirstInHand(BoardState board, Faction faction, CardType type) =>
        board.ForFaction(faction).Hand.FirstOrDefault(cardId => CardState.ForId(cardId).CardData.CardType == type, -1);

    /// <summary>Whether any of <paramref name="factions"/> has an Army in <paramref name="country"/>.</summary>
    public static bool HasArmyIn(BoardState board, IEnumerable<Faction> factions, Country country) =>
        factions.Any(faction => board.ActiveUnits(faction).Any(unit => unit.IsArmy && board.CountryOf(unit) == (int)country));

    /// <summary>What <paramref name="attacker"/> may battle in these spaces: the space when empty, else each attackable piece in it.</summary>
    public static List<BattleTarget> BattleTargetsIn(BoardState board, Faction attacker, IEnumerable<Country> countries) =>
        BattleTarget.In(board, countries, attacker);

    /// <summary>What <paramref name="attacker"/> may battle anywhere on land or at sea.</summary>
    public static List<BattleTarget> BattleTargetsOn(BoardState board, Faction attacker, CountryType type) =>
        BattleTarget.In(board, CountryState.AllCountryStates.Where(country => country.Type == type).Select(country => country.Id), attacker);
}
