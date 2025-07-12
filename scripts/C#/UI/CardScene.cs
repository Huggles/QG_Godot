using Godot;
using System;

public partial class CardScene : Control
{
    public int CardId = -1;
    public CardState CardState => CardState.ForId(CardId);
    public FactionState FactionState => FactionState.ForEnum(CardState.Faction);

    private VBoxContainer textBackgroundContainerNode;
    private VBoxContainer textContainerNode;
    private RichTextLabel titleNode;
    private RichTextLabel textNode;
    private TextureRect cardTextureNode;
    private Button cardButton;

    public override void _Ready()
    {

        textBackgroundContainerNode = GetNode<VBoxContainer>("%TextBackgroundBox");
        textContainerNode = GetNode<VBoxContainer>("%TextContainer");
        titleNode = GetNode<RichTextLabel>("%Title");
        textNode = GetNode<RichTextLabel>("%Text");
        cardTextureNode = GetNode<TextureRect>("%CardTexture");

        cardButton = GetNode<Button>("%CardButton");
        cardButton.Pressed += CardButton_Pressed;

        BuildCard();
    }

    private void CardButton_Pressed()
    {
        DebugUtilities.PrintPeer(CardId);
    }


    private void BuildCard()
    {
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
    }

    public void SetClickable(bool clickable)
    {
        cardButton.Disabled = !clickable;
    }
}
