using System.Collections.Generic;

/// <summary>
/// "France, the United States, and the United Kingdom may trace supply through each other's pieces,
/// including the Army in the Supply space." Passive: through BoardState.SupplyLinkFactions the three
/// share supply links and sources. Building still needs a supplied piece of the builder's own country.
/// </summary>
public partial class StatusSHAEF : StatusCardLogic, ISupplyShareModifier
{
    private static readonly List<Faction> Partners = new() { Faction.FRANCE, Faction.UNITED_STATES, Faction.UNITED_KINGDOM };

    public bool SharesSupply(Faction tracer, Faction owner) => Partners.Contains(tracer) && Partners.Contains(owner);

    public override TargetSet Targets() => TargetSet.Factions(Partners);
}
