using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerActionLabel : RichTextLabel
{
    private static List<MessageQueueItem> messageQueue = new List<MessageQueueItem>();
    private static MessageQueueItem currentShowingMessage;
    private static PlayerActionLabel instance;

    private Panel ContainerPanel => GetNode<Panel>("%ContainerPanel");

    public override void _Ready()
    {
        if(instance != null)
            throw new NotSupportedException("Only 1 Player Action Label should be initia");
        instance = this;
        BbcodeEnabled = true;
        HideNode();

        ShowNextMessage();
    }

    public static void ShowText(string text, Faction faction = (Faction)(-1)){
        ShowText(text, -1, faction);

    }
    public static void ShowText(string text, int duration, Faction faction = (Faction)(-1)){
        messageQueue.Add(new MessageQueueItem(text, faction, duration));
        if(instance != null) {
            instance.ShowNextMessage();
        }
    }
    public static void HideCurrentText(){
        if(instance != null){
            instance.HideNode();
        }
    }

    public void ShowNextMessage(){
        if(messageQueue.Count > 0 && currentShowingMessage == null){
            MessageQueueItem nextMessage = messageQueue[0];
            currentShowingMessage = nextMessage;
            ShowTextForDuration(nextMessage.message, nextMessage.duration, nextMessage.faction);
        }
    }

    public class MessageQueueItem {
        public string message;
        public Faction faction;
        public int duration;
        public MessageQueueItem(string message, Faction faction, int duration){
            this.message = message;
            this.faction = faction;
            this.duration = duration;
        }
    }

    public void ShowTextForDuration(string text, int duration = -1, Faction faction = (Faction)(-1))
    {
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
                FactionState factionState = GameSession.Instance.GameState.FactionStateForEnum(faction);
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

    public void HideNode()
    {
        currentShowingMessage = null;
        Visible = false;
        if (ContainerPanel != null)
        {
            ContainerPanel.Visible = false;
        }
        ShowNextMessage();
    }
}
