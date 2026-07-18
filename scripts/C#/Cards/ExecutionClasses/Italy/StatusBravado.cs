using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class StatusBravado : StatusCardLogic
{
    private List<BattleTarget> LandBattleTargets =>
        CountryState.AttackableLand(Faction)
            .Select(cs => new BattleTarget(cs.Id, TargetType.COUNTRY))
            .Concat(UnitState.AttackableArmies(Faction)
                .Select(us => new BattleTarget(us.Id, TargetType.UNIT)))
            .Distinct()
            .ToList();

    protected override List<Condition> CardTriggers()
    {
        return new List<Condition> {
            Condition.Build(new Condition.IsPlayCardStep(), this),
            Condition.Build(new Condition.IsFactionTurn(Faction), this),
            Condition.Build(new Condition.Not(new Condition.HasPlayedCardThisTurnStep(Faction)), this),
            Condition.Build(new Condition.HasLandBattleTarget(Faction), this),
        };
    }

    public override List<CardStep> OnActivate()
    {
        return new List<CardStep> {
            new CardStep(this, async () => {
                ForceDiscardCardsChangeEvent discardEvent = BuildChangeEvent(new ForceDiscardCardsChangeEvent(Faction, Faction, 2));
                discardEvent.IsTrigger = false;
                await discardEvent.ApplyChange();

                List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
                await PresentationModal.Current.ShowModal(presentationItems, "Discarded cards");

                var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, LandBattleTargets).BroadCast();
                BattleTarget battleTarget = resp.ResponseCountryIds.Count > 0
                    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
                    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);

                if (!GameFlow.Instance.CardsPlayedThisTurnStep.ContainsKey(Faction))
                    GameFlow.Instance.CardsPlayedThisTurnStep[Faction] = 1;
                else
                    GameFlow.Instance.CardsPlayedThisTurnStep[Faction] += 1;

                BattleCountryChangeEvent battleEvent = BuildChangeEvent(battleTarget.ToAttackChangeEvent(Faction));
                battleEvent.IsTrigger = true;
                return battleEvent;
            })
            .WithGuidance("Discard the top 2 cards of your draw deck to battle a land space")
        };
    }
}