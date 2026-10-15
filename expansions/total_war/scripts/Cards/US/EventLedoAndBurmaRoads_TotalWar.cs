using System.Collections.Generic;
using System.Linq;

public partial class EventLedoAndBurmaRoads_TotalWar : EventCardLogic
{
    private const int CHOICE_RECRUIT   = 0;
    private const int CHOICE_ELIMINATE = 1;
    private static readonly List<Country> chinaSpaces = [Country.China, Country.Szechuan];

    /// <summary>"To use": an Allied Army in South East Asia. A Chinese one counts, since the card names China.</summary>
    private static bool CanUse(BoardState board) =>
        TotalWarCards.TeamUnitsIn(board, FactionTeam.ALLIES, UnitType.ARMY, [Country.SouthEastAsia]).Count > 0
        || board.ActiveUnits(Faction.CHINA).Any(unit => unit.IsArmy && board.CountryOf(unit) == (int)Country.SouthEastAsia);

    private static List<CountryState> RecruitTargets(BoardState board) =>
        TotalWarCards.DeployTargets(board, Faction.CHINA, DeployType.RECRUIT, UnitType.ARMY, chinaSpaces);

    private static List<UnitState> AxisArmies(BoardState board) =>
        TotalWarCards.TeamUnitsIn(board, FactionTeam.AXIS, UnitType.ARMY, chinaSpaces).Where(unit => !board.ImmuneForTurn(unit)).ToList();

    public override TargetSet Targets() =>
        TargetSet.Countries(RecruitTargets(BoardState.Live)).Plus(TargetSet.Units(AxisArmies(BoardState.Live)));

    /// <summary>One step: the player picks which half of the card to use, and the step returns that event.</summary>
    public override List<CardStep> OnActivate() => new()
    {
        new ResultStep(this, async () => {
            List<int> recruitIds = RecruitTargets(BoardState.Live).ToCountryIds();
            List<int> armyIds = AxisArmies(BoardState.Live).Select(unit => unit.Id).ToList();
            List<string> labels = new();
            List<int> ids = new();
            if (recruitIds.Count > 0) { labels.Add("Recruit a Chinese Army in China or Szechuan"); ids.Add(CHOICE_RECRUIT); }
            if (armyIds.Count > 0) { labels.Add("Eliminate an Axis Army in China or Szechuan"); ids.Add(CHOICE_ELIMINATE); }

            int choice = ids.Count == 1 ? ids[0]
                : (await new InputRequest.SelectOptionRequestHandler(Faction, labels, ids, "Ledo and Burma Roads").BroadCast()).ResponseCardIds[0];
            if (choice == CHOICE_RECRUIT)
            {
                int countryId = (await new InputRequest.SelectCountryRequestHandler(Faction, recruitIds).BroadCast()).ResponseCountryIds[0];
                return new DeployUnitChangeEvent(Faction.CHINA, countryId, DeployType.RECRUIT);
            }
            int unitId = (await new InputRequest.SelectUnitRequestHandler(Faction, armyIds).BroadCast()).ResponseUnitIds[0];
            return new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE);
        })
        .WithCondition(() => Condition.Build(new Condition.CustomCondition(s =>
            CanUse(s.Board) && (RecruitTargets(s.Board).Count > 0 || AxisArmies(s.Board).Count > 0)), this))
        .WithGuidance("Recruit a Chinese Army, or eliminate an Axis Army, in China or Szechuan")
    };
}
