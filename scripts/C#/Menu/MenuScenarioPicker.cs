using Godot;
using System.Collections.Generic;

/// <summary>
/// The Skirmish screen's scenario dropdown: populate, preselect-by-path, the "no scenarios" message
/// and the description wrapper. The multiplayer lobby has no picker: it always plays
/// <see cref="GameManager.MultiplayerScenario"/>.
///
/// The real reason this exists: **the item index is not the scenario index.** Passing the
/// OptionButton's selected index straight to <see cref="GameManager.SetSelectedScenarioByIndex"/>,
/// which indexes into AvailableScenarios, is only correct while the picker shows every scenario. The
/// moment one is filtered out — which is exactly what a release build does to debug scenarios — the
/// two diverge silently and the picker starts selecting the wrong scenario, with no error anywhere.
///
/// So every item carries its AvailableScenarios index as the item's **id**, and callers ask
/// <see cref="ScenarioIndexAt"/> rather than doing arithmetic. Godot's OptionButton gives us the id
/// channel for free; the bug was only ever that nobody used it.
/// </summary>
public static class MenuScenarioPicker
{
	/// <summary>Shown in the description panel when the scenario directory turned up nothing.</summary>
	public const string NoScenariosMessage =
		"No scenarios available. Make sure the scenario files are present in assets/data/scenarios.";

	/// <summary>
	/// Fill the picker, carrying each entry's AvailableScenarios index as the item's id.
	///
	/// Debug scenarios are dropped in a release export: a test fixture is not something a player
	/// chooses. <see cref="GameContext.IsProductionBuild"/> rather than GameSettings.ShowDebugMenu —
	/// the export decides this, not a setting a player can flip.
	///
	/// THIS IS A VIEW FILTER AND NOTHING MORE. AvailableScenarios still holds every scenario on every
	/// build, and the files all ship (export_filter is all_resources).
	/// </summary>
	public static void Populate(OptionButton picker, List<ScenarioInfo> scenarios)
	{
		picker.Clear();
		if (scenarios == null) return;

		for (int i = 0; i < scenarios.Count; i++)
		{
			if (GameContext.IsProductionBuild && scenarios[i].IsDebug) continue;
			picker.AddItem(scenarios[i].Title, i);
		}
	}

	/// <summary>The AvailableScenarios index behind one of the picker's items, or -1 when there is none.</summary>
	public static int ScenarioIndexAt(OptionButton picker, int itemIndex)
	{
		if (itemIndex < 0 || itemIndex >= picker.ItemCount) return -1;
		return picker.GetItemId(itemIndex);
	}

	/// <summary>
	/// Select the item for this scenario path. False when the picker has no item for it — which is what
	/// a debug scenario in a release build looks like, and is a case the caller has to handle rather than ignore:
	/// the widget would otherwise sit on item 0 while GameManager.SelectedScenario still pointed at the
	/// scenario that is not shown.
	/// </summary>
	public static bool SelectByPath(OptionButton picker, List<ScenarioInfo> scenarios, string path)
	{
		if (string.IsNullOrEmpty(path) || scenarios == null) return false;

		for (int itemIndex = 0; itemIndex < picker.ItemCount; itemIndex++)
		{
			int scenarioIndex = picker.GetItemId(itemIndex);
			if (scenarioIndex < 0 || scenarioIndex >= scenarios.Count) continue;
			if (scenarios[scenarioIndex].Path != path) continue;

			picker.Selected = itemIndex;
			return true;
		}

		return false;
	}

	/// <summary>The description styling.</summary>
	public static void ShowDescription(RichTextLabel label, string description)
	{
		if (label == null) return;
		label.BbcodeEnabled = true;
		label.Text = $"[color=#bbbbbb]{description}[/color]";
	}
}
