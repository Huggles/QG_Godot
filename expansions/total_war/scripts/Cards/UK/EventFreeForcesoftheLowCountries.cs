using System.Collections.Generic;

public partial class EventFreeForcesoftheLowCountries : EventCardLogic
{
    private const int CHOICE_RECRUIT = 0;
    private const int CHOICE_BATTLE  = 1;
    private static readonly List<Country> westernEurope = [Country.WesternEurope];

    private static List<CountryState> RecruitTargets(BoardState board) =>
        TotalWarCards.DeployTargets(board, Faction.FRANCE, DeployType.RECRUIT, UnitType.ARMY, westernEurope);

    /// <summary>France does the battling, so Western Europe is attackable exactly when a supplied French piece is next to it.</summary>
    private static List<BattleTarget> BattleTargets(BoardState board) =>
        TotalWarCards.BattleTargetsIn(board, Faction.FRANCE, westernEurope);

    public override TargetSet Targets() =>
        TargetSet.Countries(RecruitTargets(BoardState.Live)).Plus(TargetSet.FromBattleTargets(BattleTargets(BoardState.Live)));

    /// <summary>One step: the player picks which half of the card to use, and the step returns that event.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, async () => {
            List<int> recruitIds = RecruitTargets(BoardState.Live).ToCountryIds();
            List<BattleTarget> battleTargets = BattleTargets(BoardState.Live);
            List<string> labels = new();
            List<int> ids = new();
            if (recruitIds.Count > 0) { labels.Add("Recruit a French Army in Western Europe"); ids.Add(CHOICE_RECRUIT); }
            if (battleTargets.Count > 0) { labels.Add("Battle Western Europe"); ids.Add(CHOICE_BATTLE); }

            int choice = ids.Count == 1 ? ids[0]
                : (await new InputRequest.SelectOptionRequestHandler(Faction, labels, ids, "Free Forces of the Low Countries").BroadCast()).ResponseCardIds[0];
            if (choice == CHOICE_RECRUIT)
                return new DeployUnitChangeEvent(Faction.FRANCE, recruitIds[0], DeployType.RECRUIT);

            var target = await new InputRequest.SelectBattleTargetRequestHandler(Faction, battleTargets).BroadCast();
            BattleTarget chosen = target.ResponseCountryIds.Count > 0
                ? new BattleTarget(target.ResponseCountryIds[0], TargetType.COUNTRY)
                : new BattleTarget(target.ResponseUnitIds[0], TargetType.UNIT);
            return chosen.ToAttackChangeEvent(Faction.FRANCE);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            RecruitTargets(s.Board).Count > 0 || BattleTargets(s.Board).Count > 0), this))
        .WithGuidance("Recruit a French Army in Western Europe, or battle it with a supplied French piece adjacent")
    };
}
