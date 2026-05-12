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

    private VBoxContainer textBackgroundContainerNode;
    private VBoxContainer textContainerNode;
    private VBoxContainer textInnerContainerNode;
    private RichTextLabel titleNode;
    private RichTextLabel textNode;
    private TextureRect cardTextureNode;
    private TextureRect textBoxTexture;
    private Button cardButton;

    [Signal] public delegate void SelectedEventHandler(int cardId);

    public override void _Ready()
    {        
        textBackgroundContainerNode = GetNode<VBoxContainer>("%TextBackgroundBox");
        textContainerNode = GetNode<VBoxContainer>("%TextContainer");
        textInnerContainerNode = GetNode<VBoxContainer>("%TextInnerContainer");
        titleNode = GetNode<RichTextLabel>("%Title");
        textNode = GetNode<RichTextLabel>("%Text");
        cardTextureNode = GetNode<TextureRect>("%CardTexture");
        textBoxTexture = GetNode<TextureRect>("%TextBoxTexture");

        cardButton = GetNode<Button>("%CardButton");
        cardButton.Pressed += CardButton_Pressed;
        cardButton.MouseEntered += CardButton_MouseEntered;
        cardButton.MouseExited += CardButton_MouseExited;
    }




    private void CardButton_Pressed()
    {
        EmitSignal(SignalName.Selected, this.CardId);
    }
    private void CardButton_MouseEntered()
    {        
        if (triggersEmphasis)
        {
            FactionHandDisplay.Instance.ShowCardEmphasis(CardId);
        }
        
    }
    private void CardButton_MouseExited()
    {
        DebugUtilities.PrintPeer($"Mouse exited card with ID: {CardId}");
        FactionHandDisplay.Instance.HideCardEmphasis();        
    }

    public void ShowCard(int cardId)
    {
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;

        CardId = cardId;
        if (CardId > -1)
        {
            Name = $"{CardState.Faction}-{CardState.CardData.UniqueName}";
            cardTextureNode.Texture = FactionState.FactionData.CardFrontTextures[CardState.CardData.CardType];
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
    public void TriggersEmphasis(bool triggersEmphasis)
    {
        this.triggersEmphasis = triggersEmphasis;
    }
}
