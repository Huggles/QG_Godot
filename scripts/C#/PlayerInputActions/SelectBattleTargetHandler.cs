using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SelectBattleTargetHandler : GodotObject, IGameEventHandler<BattleTarget>
{

    [Signal] public delegate void BattleTargetSelectedEventHandler(int target, TargetType type);

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
        UnitState.ForIds(unitIds).ForEach(us => {
            us.Tags.AddForAll(Tag.Clickable);
        });
        CountryState.ForIds(countryIds).ForEach(cs => cs.Tags.AddForAll(Tag.Clickable));
        InputManager.Instance.EnableRayTraceCasting();
        
        EventBus.Instance.CountryClicked += OnCountrySelected;
        EventBus.Instance.UnitClicked += OnUnitSelected;

        Variant[] results = await ToSignal(this, SignalName.BattleTargetSelected);
        EventBus.Instance.CountryClicked -= OnCountrySelected;
        EventBus.Instance.UnitClicked -= OnUnitSelected;

        UnitState.ForIds(unitIds).RemoveTag(Tag.Clickable, Faction.ALL);
        CountryState.ForIds(countryIds).RemoveTag(Tag.Clickable, Faction.ALL);
        InputManager.Instance.DisableRayTraceCasting();

        int targetId = results[0].As<int>();
        TargetType type = results[1].As<TargetType>();
        return new BattleTarget(targetId, type);
    }

    public void OnCountrySelected(int unitId)
    {
        EmitSignal(SignalName.BattleTargetSelected, unitId, Variant.From(TargetType.COUNTRY));
    }
    public void OnUnitSelected(int unitId)
    {
        EmitSignal(SignalName.BattleTargetSelected, unitId, Variant.From(TargetType.UNIT));
    }
}
