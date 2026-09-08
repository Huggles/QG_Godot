using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public partial class FactionState : StateObject
{
	[JsonIgnore] public FactionData FactionData { get; private set; }
	private int _score { get; set; }

	public Faction Faction => FactionData.Faction;
	/// <summary>Debug-log only (never on the wire — FactionStateDto does not carry it).</summary>
	public string FactionLabel => Faction.Label();
	public bool Playable => StaticGameData.PlayableFactions.Contains(Faction) ? true : false;

	[JsonIgnore] private MultiplayerGameState GameState => GameSession.Current.GameState;


	public int Score
	{
		get => _score;
		set
		{
			_score = value;
			//EventBusLocal.EmitSignal(nameof(EventBusLocal.FactionScoredPoints), Faction, _score);
		}
	}

	public DeckState DeckState { get; private set; }

	public FactionState(Faction faction)
	{
		FactionData = new FactionData(){ 
			Index = (int)faction,
			UniqueName = faction.ToString(), 
			Label = faction.ToString(), 
			ColorString = "#FFFFFF", 
			ColorStringText = "#000000", 
			Team = "NONE", 
			Homespace = "NONE", 
			NumberOfArmyUnits = 0, 
			NumberOfNavyUnits = 0};
	}

	public FactionState(FactionData factionData)
	{
		FactionData = factionData;
		_score = 0;
		DeckState = new DeckState(this);
	}

	/// <summary>
	/// Every unit this faction owns, deployed or not — resolved once.
	///
	/// Which units belong to which faction is fixed at setup: UnitStates is filled by
	/// InstantiateUnitStates and never added to or removed from afterwards ("in the pool" is just
	/// CountryId == -1). This used to rescan every unit in the game on each read, and it is read from
	/// ActiveUnitIds, which GameStateCalculator calls for every faction after every ChangeEvent.
	/// </summary>
	private List<int> _allUnits;
	public List<int> AllUnits => _allUnits ??=
		GameState.UnitStates.Where(unit => unit.Faction == Faction).Select(unit => unit.Id).ToList();

	/// <summary>
	/// The units currently on the board. NOT cached — CountryId changes whenever a unit is deployed or
	/// removed — but built in one pass over the cached ownership list instead of the three
	/// intermediate lists the LINQ chain used to allocate.
	/// </summary>
	public List<int> ActiveUnitIds
	{
		get
		{
			List<int> active = new();
			foreach (int unitId in AllUnits)
				if (UnitState.ForId(unitId) is { CountryId: >= 0 }) active.Add(unitId);
			return active;
		}
	}
	public List<int> OccupiedCountryIds => ActiveUnitIds.Select(id => GameState.UnitStatesById[id].CountryId).ToList();
	public List<int> SuppliedUnitIds => ActiveUnitIds.Where(id => GameState.UnitStatesById[id].InSupply).ToList();
	public List<int> UnsuppliedUnitIds => ActiveUnitIds.Where(id => !GameState.UnitStatesById[id].InSupply).ToList();

	public static FactionState ForEnum(Faction factionEnum)
		=> MultiplayerSession.Instance.GameState.FactionStatesByFaction
			.TryGetValue(factionEnum, out FactionState state) ? state : null;    
}
