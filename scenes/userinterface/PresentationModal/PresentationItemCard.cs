using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class PresentationItemCard : PresentationItem
{
    public Vector2 CardSize = new Vector2(250, 350);
    private int cardId = -1;

    public CardScene CardSceneInstance {
        get {
            return Control as CardScene;
        }
    }

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
    }

    public static List<PresentationItem> FromCardIds(List<int> cardIds, bool selectable)
    {
        return cardIds.Map(cardId => (PresentationItem)new PresentationItemCard(cardId,selectable));
    }

}
