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

    // Event handlers for cleanup
    private EventBus.NextStepStartedEventHandler onNextStepStarted;
    private Action onSkipButtonPressed;
    private Action onDiscardedDeckButtonPressed;
    private Action onTestButtonPressed;

    


    public override void _Ready()
    {
        DebugUtilities.PrintPeer("FactionHandDisplay _Ready called");
        Instance = this;
        
        // Hide by default until LoadUI is called
        Hide();
        
        DebugUtilities.PrintPeer("FactionHandDisplay about to emit UserInterfaceLoaded");
        EventBus.Emit(EventBus.SignalName.UserInterfaceLoaded, "FactionHandDisplay");
        DebugUtilities.PrintPeer("FactionHandDisplay emitted UserInterfaceLoaded");
    }

    public override void _ExitTree()
    {
        UnsubscribeFromEvents();
    }

    public void LoadUI()
    {
        CardsContainer = GetNode<Panel>("%CardsContainerPanel");
        CardPreviewContainer = GetNode<Panel>("%CardPreviewContainer");
        CardPreview = GetNode<CardScene>("%CardPreview");
        SkipButton = GetNode<Button>("%SkipButton");
        DiscardedDeckButton = GetNode<Button>("%DiscardedDeckButton");
        TestButton = GetNode<Button>("%TestButton");

        Hide();

        // Unsubscribe first to prevent duplicate connections
        UnsubscribeFromEvents();

        // Subscribe to events
        onNextStepStarted = (int turnStep) =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            
            if ((TurnStep)turnStep == TurnStep.PLAY_CARD)
            {
                Show(GameSession.Instance.GameFlow.CurrentFaction);
            }
        };
        EventBus.Instance.NextStepStarted += onNextStepStarted;

        onSkipButtonPressed = () => 
        { 
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            OnCardSelected(-1); 
        };
        SkipButton.Pressed += onSkipButtonPressed;

        onDiscardedDeckButtonPressed = () =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            
            List<PresentationItem> presentationItems = (List<PresentationItem>)PresentationItemCard.FromCardIds(DeckState.ForFaction(showingFaction).DiscardedCardIds, false);            
            PresentationModal.Instance.ShowModal(presentationItems, "Your Discarded Cards");
        };
        DiscardedDeckButton.Pressed += onDiscardedDeckButtonPressed;

        onTestButtonPressed = () =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            
            PresentationModal.Instance.ShowModal(PresentationItemImageButton.ForFactions([Faction.GERMANY,Faction.JAPAN]), "Select a faction");
        };
        TestButton.Pressed += onTestButtonPressed;
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
        
        // Only access CardsContainer if it's been initialized (in LoadUI)
        if (CardsContainer != null)
        {
            CardsContainer.MouseFilter = MouseFilterEnum.Ignore;
            DeleteCurrentCards();
        }
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

    private void UnsubscribeFromEvents()
    {
        // Unsubscribe from EventBus events
        if (EventBus.Instance != null && onNextStepStarted != null)
        {
            EventBus.Instance.NextStepStarted -= onNextStepStarted;
        }

        // Unsubscribe from button events
        if (IsInstanceValid(SkipButton) && onSkipButtonPressed != null)
        {
            SkipButton.Pressed -= onSkipButtonPressed;
        }

        if (IsInstanceValid(DiscardedDeckButton) && onDiscardedDeckButtonPressed != null)
        {
            DiscardedDeckButton.Pressed -= onDiscardedDeckButtonPressed;
        }

        if (IsInstanceValid(TestButton) && onTestButtonPressed != null)
        {
            TestButton.Pressed -= onTestButtonPressed;
        }
    }
}
