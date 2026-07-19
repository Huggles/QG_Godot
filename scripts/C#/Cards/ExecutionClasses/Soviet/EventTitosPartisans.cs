using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class EventTitosPartisans : EventCardLogic
{
    private List<int> AxisArmiesInBalkans =>
        CountryState.ForEnum(Country.Balkans).Units.Values
            .Where(uId => StaticGameData.FactionTeamForFaction(UnitState.ForId(uId).Faction) == FactionTeam.AXIS
                       && UnitState.ForId(uId).IsArmy
                       && !UnitState.ForId(uId).ImmuneForTurn)
            .ToList();

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            // Step 1: Eliminate an Axis Army in the Balkans
            new CardStep(this, async () => {
                int selectedUnitId = (await new InputRequest.SelectUnitRequestHandler(Faction, AxisArmiesInBalkans).BroadCast()).ResponseUnitIds[0];
                RemoveUnitChangeEvent removeEvent = BuildChangeEvent(
                    new RemoveUnitChangeEvent(Faction, selectedUnitId, UnitRemovalReason.ELIMINATE));
                removeEvent.IsTrigger = true;
                return removeEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() => AxisArmiesInBalkans.Count > 0), this))
            .WithGuidance("Eliminate an Axis Army in the Balkans"),

            // Step 2: Recruit a Soviet or United Kingdom Army in the Balkans
            new CardStep(this, async () => {
                var factionResp = await new InputRequest.SelectFactionRequestHandler(
                    Faction, new List<Faction> { Faction.SOVIET, Faction.UNITED_KINGDOM }).BroadCast();
                Faction selectedFaction = (Faction)factionResp.ResponseCardIds[0];

                DeployUnitChangeEvent deployEvent = BuildChangeEvent(
                    new DeployUnitChangeEvent(selectedFaction, (int)Country.Balkans, DeployType.RECRUIT));
                deployEvent.IsTrigger = true;
                return deployEvent;
            })
            .WithCondition(() => Condition.Build(new Condition.CustomCondition(() =>
                Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Balkans], Faction.SOVIET), this).MeetCondition() ||
                Condition.Build(new Condition.CountryIsRecruitable([(int)Country.Balkans], Faction.UNITED_KINGDOM), this).MeetCondition()
            ), this))
            .WithGuidance("Recruit a Soviet or United Kingdom Army in the Balkans")
        };
    }
}