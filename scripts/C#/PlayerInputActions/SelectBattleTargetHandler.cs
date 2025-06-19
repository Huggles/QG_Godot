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
        EventBus.Emit(EventBus.SignalName.SetUnitsClickable, unitIds.ToArray());
        EventBus.Emit(EventBus.SignalName.SetCountriesClickable, countryIds.ToArray());
        EventBus.Instance.CountryClicked += OnCountrySelected;
        EventBus.Instance.UnitClicked += OnUnitSelected;

        Variant[] results = await ToSignal(this, SignalName.BattleTargetSelected);
        EventBus.Instance.CountryClicked -= OnCountrySelected;
        EventBus.Instance.UnitClicked -= OnUnitSelected;

        EventBus.Emit(EventBus.SignalName.SetAllUnitsUnclickable);
        EventBus.Emit(EventBus.SignalName.SetAllCountriesUnclickable);


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
