using Godot;
using System;
using System.Diagnostics;

public partial class CardScene : Control
{
    public static readonly PackedScene CardScenePackedPath = GD.Load<PackedScene>("res://scenes/cards/CardScene.tscn");
    public static Vector2 DEFAULT_SIZE = new Vector2(500, 700);
    public static Vector2 DEFAULT_TEXT_BLOCK_SIZE = new Vector2(500, 250);
    public float CalculatedScale => Size.Y / DEFAULT_SIZE.Y;

    public int CardId = -1;
    public CardState CardState => CardState.ForId(CardId);
    public FactionState FactionState => FactionState.ForEnum(CardState.Faction);

    private bool triggersEmphasis = true;
    
    private bool showActivatableOverlay = false;

    private VBoxContainer textBackgroundContainerNode => GetNode<VBoxContainer>("%TextBackgroundBox");
    private VBoxContainer textContainerNode => GetNode<VBoxContainer>("%TextContainer");
    private VBoxContainer textInnerContainerNode => GetNode<VBoxContainer>("%TextInnerContainer");
    private RichTextLabel titleNode => GetNode<RichTextLabel>("%Title");
    private RichTextLabel textNode => GetNode<RichTextLabel>("%Text");
    private TextureRect cardTextureNode => GetNode<TextureRect>("%CardTexture");
    private TextureRect textBoxTexture => GetNode<TextureRect>("%TextBoxTexture");
    private Button cardButton => GetNode<Button>("%CardButton");
    private ColorRect activatableColorOverlay => GetNode<ColorRect>("ActivatableColorOverlay");

    [Signal] public delegate void SelectedEventHandler(int cardId);

    public override void _Ready()
    {     
        cardButton.Pressed += CardButton_Pressed;
        cardButton.MouseEntered += CardButton_MouseEntered;
        cardButton.MouseExited += CardButton_MouseExited;

        SetActivatable(!showActivatableOverlay);
    }

    private void CardButton_Pressed()
    {
        EmitSignal(SignalName.Selected, this.CardId);
    }
    private void CardButton_MouseEntered()
    {        
        if (triggersEmphasis)
        {
            FactionHandDisplay.Current.ShowCardEmphasis(CardId);
        }
        
    }
    private void CardButton_MouseExited()
    {
        DebugUtilities.PrintPeerFinest($"Mouse exited card with ID: {CardId}");
        FactionHandDisplay.Current.HideCardEmphasis();        
    }

    public void ShowCard(int cardId)
    {
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;

        CardId = cardId;
        if (CardId > -1)
        {            
            cardTextureNode.Texture = CardState.FrontTexture;
            if (!string.IsNullOrEmpty(CardState.CardData.Text))
            {
                textContainerNode.Visible = true;
                textBackgroundContainerNode.Visible = true;
                titleNode.Text = CardState.CardData.Label;
                textNode.Text = CardState.CardData.Text;
            }
            else
            {
                textContainerNode.Visible = false;
                textBackgroundContainerNode.Visible = false;
            }
        }
        else
        {
            titleNode.Text = "Card Not Found";
            textNode.Text = $"Card Id = {CardId}";
            SetClickable(false);
        }
        RecalculateSizes();
    }

    public void RecalculateSizes()
    {
        textInnerContainerNode.CustomMinimumSize = new Vector2(Size.X, DEFAULT_TEXT_BLOCK_SIZE.Y * CalculatedScale);
        textBoxTexture.CustomMinimumSize = new Vector2(Size.X, DEFAULT_TEXT_BLOCK_SIZE.Y * CalculatedScale);
        titleNode.AddThemeFontSizeOverride("normal_font_size", (int)(30f * CalculatedScale));
        textNode.AddThemeFontSizeOverride("normal_font_size", (int)(20f * CalculatedScale));
    }

    public void SetClickable(bool clickable)
    {
        cardButton.Disabled = !clickable;
    }
    public void SetActivatable(bool activatable)
    {
        showActivatableOverlay = !activatable;
        if(activatableColorOverlay != null)
        {
            activatableColorOverlay.Visible = !activatable;
        }
        
    }

    
    public void TriggersEmphasis(bool triggersEmphasis)
    {
        this.triggersEmphasis = triggersEmphasis;
    }
}
