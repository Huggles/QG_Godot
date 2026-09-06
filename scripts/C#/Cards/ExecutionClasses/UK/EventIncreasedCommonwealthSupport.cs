using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventIncreasedCommonwealthSupport : EventCardLogic
{
    private static readonly List<Country> targetCountries = [Country.India, Country.Australia, Country.Canada];

    /// <summary>The spaces the recruit is actually available in right now — the same filtered list
    /// the step offers, so the preview and the offer cannot disagree.</summary>
    private List<CountryState> RecruitTargets =>
        CountryState.RecruitableLand(Faction).Where(cs => targetCountries.Contains(cs.Country)).ToList();

    public override TargetSet Targets() => TargetSet.Countries(RecruitTargets);

    public override List<CardStep> OnActivate()
    {
        
        return new List<CardStep> {
            new CardStep(this, async() => {
                int selectedCountryId = (await new InputRequest.SelectCountryRequestHandler(Faction, RecruitTargets.ToCountryIds()).BroadCast()).ResponseCountryIds[0];
                DeployUnitChangeEvent deployUnitChangeEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.RECRUIT));
                deployUnitChangeEvent.IsTrigger = true;
                await CardPlayPool.DoChangeEvent(deployUnitChangeEvent);
            })
            .WithCondition(()=> Condition.Build(new Condition.CustomCondition(() => {
                return RecruitTargets.Count > 0;
            }), this))
            .WithGuidance("Recruit an army in India, Australia, or Canada"),
        }; 
    }
}