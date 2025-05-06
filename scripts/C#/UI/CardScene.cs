using Godot;
using System;

public partial class CardScene : Control
{    
    public CardLogic Card;

    private AspectRatioContainer _textContainerNode;
    private Label _titleNode;
    private RichTextLabel _textNode;
    private TextureRect _cardTextureNode;

    public override void _Ready()
    {
        _textContainerNode = GetNode<AspectRatioContainer>("Panel/TextContainerNode");
        _titleNode = GetNode<Label>("Panel/TextContainerNode/TextBoxTexture/VBoxContainer/Title");
        _textNode = GetNode<RichTextLabel>("Panel/TextContainerNode/TextBoxTexture/VBoxContainer/Text");
        _cardTextureNode = GetNode<TextureRect>("Panel/CardTexture");

        BuildCard();
    }

    private void BuildCard()
    {
        if (Card != null)
        {
            Name = $"{Card.Faction}-{Card.CardData.UniqueName}";

            // if (Card.IsPubliclyVisible)
            // {
            //     _cardTextureNode.Texture = Card.CardFrontTexture;
            //     if (!string.IsNullOrEmpty(Card.CardData.Text))
            //     {
            //         _textContainerNode.Visible = true;
            //         _titleNode.Text = Card.CardData.Clabel;
            //         _textNode.Text = Card.CardData.Text;
            //     }
            //     else
            //     {
            //         _textContainerNode.Visible = false;
            //     }
            // }
            // else
            // {
            //     _cardTextureNode.Texture = Card.CardBackTexture;
            //     _textContainerNode.Visible = false;
            // }
        }
    }
}
