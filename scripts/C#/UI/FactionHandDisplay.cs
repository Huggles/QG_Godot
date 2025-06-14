using Godot;
using System;
using System.Collections.Generic;

public partial class FactionHandDisplay : Control
{
    private static readonly PackedScene CardScenePacked = GD.Load<PackedScene>("res://scenes/cards/CardScene.tscn");

    [Export]
    public Faction Faction;

    private Control _cardsContainer;

    public override void _Ready()
    {
        _cardsContainer = GetNode<Control>("CardsContainerPanel");
        InitHand();
    }

    public void ShowNode(bool visible)
    {
        _cardsContainer.Visible = visible;
    }

    private void InitHand()
    {
        //Fix to use deckstates
        // if (StaticGameData.Deck.TryGetValue(Faction, out DeckState deckState))
        // {
        //     InitCards(deckState.HandCardStates);
        // }
    }

    private void InitCards(List<CardState> cards)
    {
        DeleteCurrentCards();

        const int cardStepSize = 100;
        const int rotationStepSize = 10;
        Vector2 cardScale = new Vector2(0.5f, 0.5f);

        float totalRotationSize = (cards.Count - 1) * rotationStepSize;
        float totalSizeX = (cards.Count - 1) * cardStepSize;

        for (int index = 0; index < cards.Count; index++)
        {
            CardState card = cards[index];
            var cardSceneInstance = CardScenePacked.Instantiate<CardScene>();
            cardSceneInstance.Card = card.CardLogic;
            cardSceneInstance.Scale = cardScale;
            _cardsContainer.AddChild(cardSceneInstance);

            Vector2 basePosition = new Vector2(_cardsContainer.Size.X * cardScale.X, 0);
            basePosition -= new Vector2(cardSceneInstance.PivotOffset.X, cardSceneInstance.PivotOffset.Y / 2f);
            basePosition -= new Vector2(totalSizeX / 2f, 0);
            basePosition += new Vector2(index * cardStepSize, 0);

            cardSceneInstance.Position = basePosition;
            cardSceneInstance.ZIndex = index;
            cardSceneInstance.RotationDegrees = index * rotationStepSize - (totalRotationSize / 2f);
        }
    }

    private void DeleteCurrentCards()
    {
        foreach (Node cardDisplay in _cardsContainer.GetChildren())
        {
            _cardsContainer.RemoveChild(cardDisplay);
            cardDisplay.QueueFree(); // Ensure memory is cleaned up
        }
    }
}
