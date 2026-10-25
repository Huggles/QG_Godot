using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class PresentationItemCard : PresentationItem
{
    // The 250x350 card at the player's Pop-up Card Size (70% by default), so a modal of draw or discard
    // choices can leave more of the board in view. Read per item, so a change shows on the next modal.
    public Vector2 CardSize = (new Vector2(250, 350) * GameSettings.ModalCardScale).Round();
    private int cardId = -1;

    public CardScene CardSceneInstance {
        get {
            return Control as CardScene;
        }
    }

    /// <summary>Set before the control loads: the card is drawn under the unavailable scrim with this line across it (an ignored Status card).</summary>
    public string DimCaption { get; set; }
    private Label _dimCaption;

    public PresentationItemCard(int cardId, bool selectable) : base(cardId, selectable)
    {
        this.cardId = cardId;
    }

    public PresentationItemCard(int identifier, int cardId, bool selectable) : base(identifier, selectable)
    {
        this.cardId = cardId;
    }

    public override Control InitializeControl()
    {
        CardScene cardSceneInstance = CardScene.CardScenePackedPath.Instantiate<CardScene>();
        cardSceneInstance.Size = CardSize;
        cardSceneInstance.CustomMinimumSize = CardSize;
        Control = cardSceneInstance;
        _orderBadge = new Label();
        _orderBadge.Visible = false;
        _orderBadge.Position = new Vector2(8, 8);
        _orderBadge.ZIndex = 100;
        Control.AddChild(_orderBadge);
        _dimCaption = new Label
        {
            Visible = false,
            Position = new Vector2(0, CardSize.Y / 2 - 24),
            Size = new Vector2(CardSize.X, 48),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ZIndex = 100,
            LabelSettings = new LabelSettings { FontSize = 18, OutlineSize = 6, OutlineColor = Colors.Black },
        };
        Control.AddChild(_dimCaption);
        return cardSceneInstance;
    }
    public override void LoadControl()
    {
        CardSceneInstance.Size = CardSize;
        CardSceneInstance.CustomMinimumSize = CardSize;
        CardSceneInstance.ShowCard(cardId);
        CardSceneInstance.SetClickable(Selectable);
        CardSceneInstance.TriggersEmphasis(false);
        CardSceneInstance.Selected += (cardId) => { EmitSignal(SignalName.ItemClicked, Identifier); };
        if (DimCaption != null)
        {
            CardSceneInstance.SetAvailability(CardScene.CardAvailability.Unavailable);
            _dimCaption.Text = DimCaption;
            _dimCaption.Visible = true;
        }
    }

    public static List<PresentationItem> FromCardIds(List<int> cardIds, bool selectable)
    {
        return cardIds.Map(cardId => (PresentationItem)new PresentationItemCard(cardId,selectable));
    }

}
