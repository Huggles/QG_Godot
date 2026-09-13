using Godot;
using System.Collections.Generic;

/// <summary>
/// The scenario dropdown shared by the two screens that can start a game — the Skirmish screen and the
/// multiplayer lobby. Sibling of <see cref="MenuSeedField"/>, extracted for the same reason and at the
/// same seam: both screens were carrying their own copy of populate, preselect-by-path, the "no
/// scenarios" message and the description wrapper.
///
/// The real reason this exists rather than a third copy: **the item index is not the scenario index.**
/// Both screens used to pass the OptionButton's selected index straight to
/// <see cref="GameManager.SetSelectedScenarioByIndex"/>, which indexes into AvailableScenarios. That is
/// only correct while the picker shows every scenario. The moment one is filtered out — which is
/// exactly what the lobby now does to tutorials — the two diverge silently and the picker starts
/// selecting the wrong scenario, with no error anywhere.
///
/// So every item carries its AvailableScenarios index as the item's **id**, and callers ask
/// <see cref="ScenarioIndexAt"/> rather than doing arithmetic. Godot's OptionButton gives us the id
/// channel for free; the bug was only ever that nobody used it.
///
/// Deliberately NOT here: anything about the opening-discard checkbox. The lobby's is RPC-synced and
/// its handler is unsubscribed and resubscribed around the write, precisely so that seeding the box
/// cannot be mistaken for a host toggling it and re-broadcast. Hiding that behind a menu utility to
/// save one line would conceal the one thing about it that has to stay visible.
/// </summary>
public static class MenuScenarioPicker
{
	/// <summary>Shown in the description panel when the scenario directory turned up nothing.</summary>
	public const string NoScenariosMessage =
		"No scenarios available. Make sure the scenario files are present in assets/data/scenarios.";

	/// <summary>
	/// Fill the picker, carrying each entry's AvailableScenarios index as the item's id.
	///
	/// <paramref name="includeTutorials"/> is false for the multiplayer lobby: a tutorial drives five
	/// factions from a script and blocks the game on a button only one peer can press, so it is single
	/// player by construction — TutorialRuntime declines to start one at all when peers are attached.
	/// Offering it in a lobby only produces a game whose scenario nobody chose to play that way.
	/// </summary>
	public static void Populate(OptionButton picker, List<ScenarioInfo> scenarios, bool includeTutorials)
	{
		picker.Clear();
		if (scenarios == null) return;

		for (int i = 0; i < scenarios.Count; i++)
		{
			if (!includeTutorials && scenarios[i].IsTutorial) continue;
			picker.AddItem(scenarios[i].Title, i);
		}
	}

	/// <summary>
	/// The AvailableScenarios index behind one of the picker's items, or -1 when there is none.
	///
	/// -1 is a real answer rather than a failure: the lobby's restore branch deliberately adds a one-off
	/// item for a scenario that lives inside the save and resolves against nothing local.
	/// </summary>
	public static int ScenarioIndexAt(OptionButton picker, int itemIndex)
	{
		if (itemIndex < 0 || itemIndex >= picker.ItemCount) return -1;
		return picker.GetItemId(itemIndex);
	}

	/// <summary>
	/// Select the item for this scenario path. False when the picker has no item for it — which is what
	/// a filtered-out tutorial looks like, and is a case the caller has to handle rather than ignore:
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

	/// <summary>The shared description styling, so the two screens cannot drift apart on it.</summary>
	public static void ShowDescription(RichTextLabel label, string description)
	{
		if (label == null) return;
		label.BbcodeEnabled = true;
		label.Text = $"[color=#bbbbbb]{description}[/color]";
	}
}
