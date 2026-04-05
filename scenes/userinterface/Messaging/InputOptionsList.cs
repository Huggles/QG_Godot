using Godot;
using System;
using System.Collections.Generic;

public partial class InputOptionsList : ItemList, LoadableUI
{
	public static InputOptionsList Instance;
	private Panel ContainerPanel => GetNode<Panel>("%ContainerPanel");
	private bool isSubscribed = false;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
		
		// Hide by default until LoadUI is called
		if (ContainerPanel != null)
		{
			ContainerPanel.Visible = false;
		}
		
		EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "InputOptionsList");
	}

	public override void _ExitTree()
	{
		// Unsubscribe from events
		if (isSubscribed)
		{
			ItemSelected -= HandleItemSelected;
			isSubscribed = false;
		}
	}

	public void LoadUI()
	{
		// Unsubscribe first to prevent duplicate connections
		if (isSubscribed)
		{
			ItemSelected -= HandleItemSelected;
		}
		ItemSelected += HandleItemSelected;
		isSubscribed = true;
		Clear();
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

		int indexDoNothing = Instance.AddItem("Do nothing", null, true);
		Instance.SetItemDisabled(indexDoNothing, false);
	}

	public static void HideList()
	{		
        Instance.ContainerPanel.MouseFilter = MouseFilterEnum.Pass;
        Instance.MouseFilter = MouseFilterEnum.Pass;
		Instance.ContainerPanel.Visible = false;
	}
}
