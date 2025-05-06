using Godot;
using System;

public partial class InputMessageLabel : RichTextLabel
{
    public static string CurrentText;
    public static Faction CurrentFaction = (Faction)(-1);
    public static InputMessageLabel instance;

    private Panel ContainerPanel => GetNode<Panel>("%ContainerPanel");

    public override void _Ready()
    {
        BbcodeEnabled = true;
        if(instance != null){
            throw new NotSupportedException("Only one input message modal is allowed");
        }

        instance = this;
        HideNode();
        if (!string.IsNullOrEmpty(CurrentText))
        {
            ShowText(CurrentText, CurrentFaction);
        }
    }

    public static void ShowText(string text, Faction faction = (Faction)(-1))
    {
        CurrentText = text;
        CurrentFaction = faction;

        if (instance != null)
        {
            instance.Text = CurrentText;
            instance.Visible = true;

            if (instance.ContainerPanel != null)
            {
                instance.ContainerPanel.Visible = true;
            }
        }
    }

    public static void HideNode()
    {
        if (instance != null)
        {
            CurrentText = null;
            instance.Visible = false;

            if (instance.ContainerPanel != null)
            {
                instance.ContainerPanel.Visible = false;
            }
        }
    }
}
