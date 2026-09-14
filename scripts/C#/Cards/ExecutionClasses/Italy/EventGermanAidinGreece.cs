using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class EventGermanAidinGreece : EventCardLogic
{
    public List<Country> targetCountries = [Country.Balkans];

    private List<int> AlliedArmiesInBalkans =>
        CountryState.ForEnum(targetCountries[0]).Units.Values
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.ALLIES
                       && UnitState.ForId(uId).IsArmy
                       && !UnitState.ForId(uId).ImmuneForTurn)
            .ToList();

    /// <summary>The Allied armies step 1 clears out, and the space step 2 recruits into.</summary>
    public override TargetSet Targets() =>
        TargetSet.Units(AlliedArmiesInBalkans).Plus(TargetSet.Countries(targetCountries));

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new ResultStep(this, async() => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AlliedArmiesInBalkans).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE);
                return removeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => AlliedArmiesInBalkans.Count > 0),this))
            .WithGuidance($"Eliminate an Allied army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new ResultStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = new DeployUnitChangeEvent(Faction.GERMANY, selectedCountryId, DeployType.RECRUIT);
                return deployUnitChangeEvent;
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], Faction.GERMANY),this))
            .WithGuidance($"Recruit a German army in {CountryState.ForEnum(targetCountries[0]).Label}"),
        };
        
    }
}