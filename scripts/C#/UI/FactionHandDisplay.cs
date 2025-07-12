using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionHandDisplay : Control, LoadableUI
{
    private static readonly PackedScene CardScenePacked = GD.Load<PackedScene>("res://scenes/cards/CardScene.tscn");
    public static FactionHandDisplay Instance;

    private Control _cardsContainer;

    public override void _Ready()
    {
        Instance = this;
        EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "FactionHandDisplay");        
    }

    public void LoadUI()
    {
        _cardsContainer = GetNode<Control>("CardsContainerPanel");
        
        EventBus.Instance.NewTurnStarted += (int turnCounter) =>
        {
            InitHand(GameSession.Instance.GameFlow.CurrentFaction);
        };
    }

    public void ShowNode(bool visible)
    {
        _cardsContainer.Visible = visible;
    }

    private void InitHand(Faction faction)
    {
        if (faction != Faction.NONE && faction != Faction.ALL)
        {
            InitCards(DeckState.ForFaction(faction).HandCardStates);        
        }
    }

    private void InitCards(List<CardState> cards)
    {
        DeleteCurrentCards();

        const int cardStepSize = 100;
        const int rotationStepSize = 10;
        Vector2 cardScale = new Vector2(0.5f, 0.5f);

        float totalRotationSize = (cards.Count - 1) * rotationStepSize;
        float totalSizeX = (cards.Count - 1) * cardStepSize;

        foreach (var (cardState, index) in cards.Select((cardState, index)=> (cardState, index))) {
            var cardSceneInstance = CardScenePacked.Instantiate<CardScene>();
            cardSceneInstance.CardId = cardState.Id;
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
