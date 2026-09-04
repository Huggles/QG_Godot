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

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep>
        {
            new CardStep(this, async() => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AlliedArmiesInBalkans).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(removeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => AlliedArmiesInBalkans.Count > 0),this))
            .WithGuidance($"Eliminate an Allied army in {CountryState.ForEnum(targetCountries[0]).Label}"),
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, [(int)targetCountries[0]]).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction.GERMANY, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CountryIsRecruitable([(int)targetCountries[0]], Faction.GERMANY),this))
            .WithGuidance($"Recruit a German army in {CountryState.ForEnum(targetCountries[0]).Label}"),
        };
        
    }
}