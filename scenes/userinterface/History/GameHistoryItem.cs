using Godot;
using System;
using System.Threading.Tasks;

public partial class GameHistoryItem : Control
{
    private GameHistoryList gameHistoryList => GetParent<GameHistoryList>();
    private Panel Panel => GetNode<Panel>("Panel");
    private Label Label => Panel.GetNode<Label>("Label");

    private Tween appearTween;
    private Timer fadeTimer;

    public static GameHistoryItem Create(string text, Faction faction)
    {
        GameHistoryItem item = AssetRepository.GameHistoryItemScenePackaged.Instantiate<GameHistoryItem>();
        item.Label.Text = text;

        if(faction == Faction.NONE)
        {
            item.Panel.SelfModulate = Colors.Gray;
        }
        else
        {            
            item.Panel.SelfModulate = FactionState.ForEnum(faction).FactionData.FactionColor;
        }        
        return item;
    }

    public override void _Ready()
    {        
        base._Ready();

        fadeTimer = new Timer { OneShot = true, WaitTime = 5.0 };
        fadeTimer.Timeout += Fade;
        AddChild(fadeTimer);

        this.Visible = false;
        this.Modulate = new Color(1, 1, 1, 0);
        Callable.From(Appear).CallDeferred();        
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        appearTween?.Kill();
    }

    public async void Appear()
    {
        await Task.Delay(100); // slight delay to allow for any necessary setup before showing the item
        if (!GodotObject.IsInstanceValid(this)) return;
        this.Visible = true;
        appearTween = CreateTween();
        appearTween.TweenProperty(this, "modulate:a", 1, GameSettings.DurationMediumSeconds);        
        appearTween.Finished += OnAppear;
    }

    public async void Fade()
    {
        await Task.Delay(100); // slight delay to allow for any necessary setup before hiding the item
        if (!GodotObject.IsInstanceValid(this)) return;
        Tween fadeTween = CreateTween();
        fadeTween.TweenProperty(this, "modulate:a", 0, GameSettings.DurationMediumSeconds);
    }


    public async void AppearInstantly()
    {
        await Task.Delay(100); // slight delay to allow for any necessary setup before showing the item
        if (!GodotObject.IsInstanceValid(this)) return;
        this.Visible = true;
        this.Modulate = new Color(1, 1, 1, 1);
        OnAppear();
    }

    public async void FadeInstantly()
    {
        await Task.Delay(100); // slight delay to allow for any necessary setup before hiding the item
        if (!GodotObject.IsInstanceValid(this)) return;
        this.Visible = false;
        this.Modulate = new Color(1, 1, 1, 0);
    }

    public void OnAppear()
    {
        fadeTimer.Start();
    }
}