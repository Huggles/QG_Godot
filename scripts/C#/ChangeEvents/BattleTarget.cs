using System.Collections.Generic;
using System.Linq;

public class BattleTarget
{

    public int Id { get; set; }
    public TargetType Type { get; set; }

    public BattleTarget(int id, TargetType type)
    {
        this.Id = id;
        this.Type = type;
    }

    /// <summary>
    /// Everything a faction may attack in these countries: the empty country itself where it is
    /// attackable, plus each attackable unit standing in it. The two halves of a battle offer, for
    /// the cards that name their battlegrounds outright rather than taking the board-wide
    /// CountryState.AttackableLand/UnitState.AttackableArmies pair.
    ///
    /// Reads Tag.Attackable, which GameStateCalculator has already computed per faction — the same
    /// source the board-wide lists use, so an offer built here and one built there agree.
    ///
    /// Not the same question as the instance method CountryState.BattleTargets, which additionally
    /// requires an adjacent supplied unit of its own: that answers "can I reach this country?", this
    /// answers "what is attackable in it?". Both are live.
    /// </summary>
    public static List<BattleTarget> In(IEnumerable<int> countryIds, Faction faction) =>
        (countryIds ?? Enumerable.Empty<int>())
            .Select(CountryState.ForId)
            .Where(countryState => countryState != null)
            .SelectMany(countryState =>
            {
                List<BattleTarget> targets = new();
                if (countryState.Tags.Has(Tag.Attackable, faction))
                    targets.Add(new BattleTarget(countryState.Id, TargetType.COUNTRY));
                targets.AddRange(countryState.Units.Values
                    .Where(unitId => UnitState.ForId(unitId).Tags.Has(Tag.Attackable, faction))
                    .Select(unitId => new BattleTarget(unitId, TargetType.UNIT)));
                return targets;
            })
            .ToList();

    /// <inheritdoc cref="In(IEnumerable{int}, Faction)"/>
    public static List<BattleTarget> In(IEnumerable<Country> countries, Faction faction) =>
        In(countries?.Select(country => (int)country), faction);

    public BattleCountryChangeEvent ToAttackChangeEvent(Faction faction)
    {
        if (Type == TargetType.UNIT)
        {
            return new BattleUnitChangeEvent(faction, Id);
        }
        else
        {
            return new BattleCountryChangeEvent(faction, Id);
        }
    }
    public override bool Equals(object obj)
    {
        BattleTarget other = obj as BattleTarget;
        return other.Id == Id && other.Type == Type;
    }
    public override int GetHashCode()
    {
        return this.Id.GetHashCode() + this.Type.GetHashCode();
    }
}

