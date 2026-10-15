using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>"Use at the beginning of your Play step": your Play step with the play unspent, as ResponseMobileForce. Pair with IsFreePlayStepActivation.</summary>
    public static List<Condition> AtPlayStepStart(CardLogic card) => new()
    {
        Condition.Build(new Condition.IsPlayCardStep(), card),
        Condition.Build(new Condition.IsFactionTurn(card.Faction), card)
    };

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
