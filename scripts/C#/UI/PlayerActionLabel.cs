using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerActionLabel : RichTextLabel, LoadableUI
{    
    public static PlayerActionLabel Instance;
    
    private Panel ContainerPanel => GetNode<Panel>("%ContainerPanel");

    public override void _Ready()
    {        
        Instance = this;
        
        // Hide by default until LoadUI is called
        Visible = false;
    }

    public void LoadUI()
    {
        BbcodeEnabled = true;
        HideNode();        
    }

    public static void ShowText(string text, Faction faction = (Faction)(-1))
    {
        ShowText(text, -1, faction);

    }
    public static void ShowText(string text, int duration, Faction faction = (Faction)(-1)){        
        Instance.ShowTextForDuration(text, duration, faction);
    }

    public void ShowTextForDuration(string text, int duration = -1, Faction faction = (Faction)(-1))
    {
        ContainerPanel.MouseFilter = MouseFilterEnum.Stop;
        this.MouseFilter = MouseFilterEnum.Stop;
        Text = text;
        Visible = true;
        if (ContainerPanel != null)
            
        {
            ContainerPanel.Visible = true;
            if (faction == (Faction)(-1))
            {
                var stylebox = ContainerPanel.GetThemeStylebox("panel") as StyleBoxFlat;
                if (stylebox != null)
                    stylebox.BgColor = Colors.White;

                AddThemeColorOverride("default_color", Colors.Green);
            }
            else
            {
                FactionState factionState = FactionState.ForEnum(faction);
                FactionData factionData = factionState.FactionData;
                var stylebox = ContainerPanel.GetThemeStylebox("panel") as StyleBoxFlat;
                if (stylebox != null)
                    stylebox.BgColor = factionData.FactionColor;

                AddThemeColorOverride("default_color", factionData.FactionColorText);
            }
        }

        if(duration > -1){
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += (s, e)=>{
                HideNode();
                timer.Dispose();
            };
            timer.Start();
            
        }        
    }

    public static void HideText()
    {
        Instance.HideNode();
    }
    public void HideNode()
    {
        Visible = false;
        if (ContainerPanel != null)
        {
            ContainerPanel.Visible = false;
        }
        ContainerPanel.MouseFilter = MouseFilterEnum.Pass;
        this.MouseFilter = MouseFilterEnum.Pass;
    }
}
