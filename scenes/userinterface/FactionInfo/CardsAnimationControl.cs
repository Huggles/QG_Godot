using Godot;
using System;
using System.Threading.Tasks;

public partial class CardsAnimationControl : Control
{

    public Label CardsAnimationControlLabel => GetNode<Label>("CardsAnimationControlLabel");
    public TextureRect CardsAnimationControlTextureRect => GetNode<TextureRect>("CardsAnimationControlTextureRect");

    private FactionInfoRow FactionInfoRow => GetParent<FactionInfoRow>();
    public Faction Faction => FactionInfoRow != null ? FactionInfoRow.Faction : Faction.NONE; 

    public static Vector2 NodeOriginalPosition = new Vector2(0, 0);    


    public override void _Ready()
    {        
        Visible = false;        
        NodeOriginalPosition = this.Position;
        CardsAnimationControlTextureRect.Texture = FactionState.ForEnum(Faction).FactionData.CardBackTexture;
        //Test(3);
    }

    public async void Test(int n)
    {        
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);     
        if (IsInstanceValid(this) && !IsQueuedForDeletion())        
            ShowCardsAnimation(n);
            Test(-n);
    }

    public void ShowCardsAnimation(int numberOfCards)
    {        
        this.CardsAnimationControlLabel.Text = $"{numberOfCards}";

        Vector2 pos1 = NodeOriginalPosition;
        Vector2 pos2 = NodeOriginalPosition + new Vector2(30, 0);
        Vector2 pos3 = NodeOriginalPosition +  new Vector2(30, -300);
        Vector2 pos4 = NodeOriginalPosition +  new Vector2(60, 0);

        if(numberOfCards == 0)
        {            
            return;
        }

        Modulate = new Color(1, 1, 1, 0);
        Visible = true;
        Tween tween = GetTree().CreateTween();
        tween.Finished += () => {
            Visible = false;
            tween.Dispose();
        };
        if(numberOfCards > 0)
        {
            this.Position = pos1;
            tween.TweenProperty(this, "position", pos2, GameSettings.DurationMediumSeconds).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(this, "position", pos3, GameSettings.DurationMediumSeconds).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);            
        } 
        else
        {
            this.Position = pos3;
            tween.TweenProperty(this, "position", pos2, GameSettings.DurationMediumSeconds).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(this, "position", pos4, GameSettings.DurationMediumSeconds).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);
        }

        Tween fadeTween = GetTree().CreateTween();
        fadeTween.TweenProperty(this, "modulate:a", 1.0f, GameSettings.DurationMediumSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        fadeTween.TweenProperty(this, "modulate:a", 0.0f, GameSettings.DurationMediumSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        
        
        
    }
}
