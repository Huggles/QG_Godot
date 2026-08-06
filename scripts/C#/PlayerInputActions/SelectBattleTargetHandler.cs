using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SelectBattleTargetHandler : IGameEventHandler<BattleTarget>
{
    private List<int> unitIds;
    private List<int> countryIds;
    public SelectBattleTargetHandler(List<BattleTarget> battleTargets)
    {
        Init(
            battleTargets.Where(battleTarget => battleTarget.Type == TargetType.COUNTRY).ToList().Map(battleTarget => battleTarget.Id),
            battleTargets.Where(battleTarget => battleTarget.Type == TargetType.UNIT).ToList().Map(battleTarget => battleTarget.Id)
            );
    }
    public SelectBattleTargetHandler(List<int> countryIds, List<int> unitIds)
    {
        Init(countryIds, unitIds);
    }

    public void Init(List<int> countryIds, List<int> unitIds)
    {
        this.countryIds = countryIds != null ? countryIds : new List<int>();
        this.unitIds = unitIds != null ? unitIds : new List<int>();
    }

    public async Task<BattleTarget> Handle()
    {
        var tcs = new TaskCompletionSource<BattleTarget>();

        void onCountry(int id) { tcs.TrySetResult(new BattleTarget(id, TargetType.COUNTRY)); }
        void onUnit(int id)    { tcs.TrySetResult(new BattleTarget(id, TargetType.UNIT)); }
        void onSkip()          { tcs.TrySetResult(null); }

        UnitState.ForIds(unitIds).ForEach(us => us.Tags.AddForAll(Tag.Clickable));
        CountryState.ForIds(countryIds).ForEach(cs => cs.Tags.AddForAll(Tag.Clickable));
        InputManager.Current.EnableRayTraceCasting();
        SelectionSkipButton.Current?.Show();

        EventBus.Instance.CountryClicked += onCountry;
        EventBus.Instance.UnitClicked    += onUnit;
        EventBus.Instance.SelectionSkipped += onSkip;

        // Registered so error recovery can release this selection — see SelectCountryHandler.
        BattleTarget result;
        using (PendingLocalInput.Register(onSkip))
        {
            try
            {
                result = await tcs.Task;
            }
            finally
            {
                EventBus.Instance.CountryClicked -= onCountry;
                EventBus.Instance.UnitClicked    -= onUnit;
                EventBus.Instance.SelectionSkipped -= onSkip;
                SelectionSkipButton.Current?.Hide();
                UnitState.ForIds(unitIds).RemoveTag(Tag.Clickable, Faction.ALL);
                CountryState.ForIds(countryIds).RemoveTag(Tag.Clickable, Faction.ALL);
                InputManager.Current.DisableRayTraceCasting();
            }
        }

        return result;
    }
}
