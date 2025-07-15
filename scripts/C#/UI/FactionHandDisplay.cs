using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class FactionHandDisplay : Control, LoadableUI
{

    public static FactionHandDisplay Instance;

    private Control CardsContainer;
    private Panel CardPreviewContainer;
    private CardScene CardPreview;
    private Button SkipButton;
    private Button DeckButton;
    private Button DiscardedDeckButton;
    private Button TestButton;

    private List<CardScene> CardScenes = new List<CardScene>();
    private Faction showingFaction;

    [Signal] public delegate void CardSelectedEventHandler(int cardId);

    


    public override void _Ready()
    {
        Instance = this;
        EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "FactionHandDisplay");
    }

    public void LoadUI()
    {
        CardsContainer = GetNode<Panel>("%CardsContainerPanel");
        CardPreviewContainer = GetNode<Panel>("%CardPreviewContainer");
        CardPreview = GetNode<CardScene>("%CardPreview");
        SkipButton = GetNode<Button>("%SkipButton");
        DeckButton = GetNode<Button>("%DeckButton");
        DiscardedDeckButton = GetNode<Button>("%DiscardedDeckButton");
        TestButton = GetNode<Button>("%TestButton");

        Hide();
        EventBus.Instance.NextStepStarted += (int turnStep) =>
        {
            if ((TurnStep)turnStep == TurnStep.PLAY_CARD)
            {
                Show(GameSession.Instance.GameFlow.CurrentFaction);
            }
        };
        SkipButton.Pressed += () => { OnCardSelected(-1); };
        DeckButton.Pressed += () =>
        {
            List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(DeckState.ForFaction(showingFaction).DeckCardIds, false);
            PresentationModal.Instance.ShowModal(presentationItems, "Your Draw Deck", false);
        };
        DiscardedDeckButton.Pressed += () =>
        {
            List<PresentationItem> presentationItems = (List<PresentationItem>)PresentationItemCard.FromCardIds(DeckState.ForFaction(showingFaction).DiscardedCardIds, false);            
            PresentationModal.Instance.ShowModal(presentationItems, "Your Discarded Cards");
        };
        TestButton.Pressed += () =>
        {
            List<PresentationItem> presentationItems = new List<PresentationItem>
            {
                new PresentationItemImageButton(0, FactionData.GERMANY_FLAG_TEXTURE, true),
                new PresentationItemImageButton(0, FactionData.UNITED_KINGDOM_FLAG_TEXTURE, true),
                new PresentationItemImageButton(0, FactionData.JAPAN_FLAG_TEXTURE, true),
                new PresentationItemImageButton(0, FactionData.SOVIET_FLAG_TEXTURE, true),
                new PresentationItemImageButton(0, FactionData.ITALY_FLAG_TEXTURE, true),
                new PresentationItemImageButton(0, FactionData.UNITED_STATES_FLAG_TEXTURE, true)
            };
            PresentationModal.Instance.ShowModal(presentationItems, "Select a faction");
        };
    }

    public void Show(Faction faction)
    {
        showingFaction = faction;
        ResetVisibility();
        if (faction != Faction.NONE && faction != Faction.ALL)
        {
            InitCards(DeckState.ForFaction(faction).HandCardIds);
        }
    }

    public void Show(List<CardActivationOption> cardActivationOptions)
    {
        showingFaction = cardActivationOptions[0].CardState.Faction;
        ResetVisibility();
        List<int> cardIds = new List<int>();
        foreach (CardActivationOption cardActivationOption in cardActivationOptions)
        {
            cardIds.Add(cardActivationOption.CardId);
        }
        InitCards(cardIds);
    }

    private void ResetVisibility()
    {
        Visible = true;
        CardsContainer.MouseFilter = MouseFilterEnum.Stop;
        HideCardEmphasis();
    }

    public new void Hide()
    {
        Visible = false;
        CardsContainer.MouseFilter = MouseFilterEnum.Ignore;
        DeleteCurrentCards();
    }

    private void InitCards(List<int> cardIds)
    {
        DeleteCurrentCards();

        const int cardStepSize = 100;
        const int rotationStepSize = 10;
        Vector2 cardSize = new Vector2(500, 700);
        Vector2 cardScale = new Vector2(0.5f, 0.5f);

        float totalRotationSize = (cardIds.Count - 1) * rotationStepSize;
        float totalSizeX = (cardIds.Count - 1) * cardStepSize;

        foreach (var (cardId, index) in cardIds.Select((cardId, index) => (cardId, index)))
        {
            var cardSceneInstance = CardScene.CardScenePackedPath.Instantiate<CardScene>();
            cardSceneInstance.Size = cardSize;
            cardSceneInstance.Scale = cardScale;
            CardsContainer.AddChild(cardSceneInstance);

            Vector2 basePosition = new Vector2(CardsContainer.Size.X * 0.5f, 0);
            basePosition.X += cardStepSize * index;
            basePosition.X -= (totalSizeX / 2) + cardSceneInstance.PivotOffset.X;
            basePosition.Y -= cardSceneInstance.Size.Y / 3;


            cardSceneInstance.Position = basePosition;
            cardSceneInstance.ZIndex = index;
            cardSceneInstance.RotationDegrees = index * rotationStepSize - (totalRotationSize / 2f);

            cardSceneInstance.Selected += OnCardSelected;
            CardScenes.Add(cardSceneInstance);
            cardSceneInstance.ShowCard(cardId);
        }
    }

    private void OnCardSelected(int cardId)
    {
        EmitSignal(SignalName.CardSelected, cardId);
    }

    private void DeleteCurrentCards()
    {
        foreach (CardScene cardScene in CardScenes)
        {
            CardsContainer.RemoveChild(cardScene);
            cardScene.Selected -= OnCardSelected;
            cardScene.QueueFree(); // Ensure memory is cleaned up
        }
        CardScenes.Clear();
    }

    public void ShowCardEmphasis(int cardId)
    {
        CardPreview.ShowCard(cardId);
    }
    public void HideCardEmphasis()
    {
        CardPreview.Visible = false;
        CardPreview.MouseFilter = MouseFilterEnum.Ignore;
    }
}
