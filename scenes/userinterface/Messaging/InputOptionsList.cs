using Godot;
using System;
using System.Collections.Generic;

public partial class InputOptionsList : ItemList
{
	public static InputOptionsList Instance;
	private Panel ContainerPanel => GetNode<Panel>("%ContainerPanel");
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
		ItemSelected += HandleItemSelected;
		Clear();
		EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "InputOptionsList");
	}

	private void HandleItemSelected(long index)
	{
		DebugUtilities.PrintPeer(index);
		Clear();
	}

	public static void ShowOptions(List<CardActivationOption> cardActivationOptions)
	{
		Instance.ContainerPanel.Visible = true;
		foreach (CardActivationOption option in cardActivationOptions)
		{
			int index = Instance.AddItem(option.Label, null, option.Activatable);
			Instance.SetItemDisabled(index, !option.Activatable);
		}
	}

	public static void HideList()
	{
		Instance.ContainerPanel.Visible = false;
	}
}
