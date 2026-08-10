using Godot;
using System.Collections.Generic;

/// <summary>
/// A Bulletin rendered in a PresentationModal. Mirrors PresentationItemCard, but takes a CardFace
/// instead of a card id — an automatic step mutator has no CardState to look up.
///
/// Always non-selectable: a Bulletin modal announces that a scenario rule fired, it is not a choice.
/// </summary>
public partial class PresentationItemBulletin : PresentationItem
{
    public Vector2 CardSize = new Vector2(250, 350);
    private readonly CardFace _face;

    public CardScene CardSceneInstance => Control as CardScene;

    public PresentationItemBulletin(CardFace face) : base(-1, false)
    {
        _face = face;
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
        CardSceneInstance.ShowFace(_face);
        CardSceneInstance.SetClickable(false);
        CardSceneInstance.SetActivatable(true); // clears the red "cannot use this" scrim
        CardSceneInstance.TriggersEmphasis(false);
    }

    public static List<PresentationItem> Single(CardFace face) =>
        new() { new PresentationItemBulletin(face) };
}
