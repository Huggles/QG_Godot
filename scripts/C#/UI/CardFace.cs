using Godot;
using System.Collections.Generic;

/// <summary>
/// Everything CardScene needs to draw a card front: art, title, body. Exists so the scene can render
/// something that is not a CardState — a Bulletin for an automatic step mutator has no CardState and
/// should not get one, but must still be presented as a card.
/// </summary>
public readonly struct CardFace
{
    public Texture2D Front { get; }
    public string Title { get; }
    public string Text { get; }

    public CardFace(Texture2D front, string title, string text)
    {
        Front = front;
        Title = title;
        Text = text;
    }

    public static CardFace ForCard(CardState cardState) =>
        cardState.IsFaceVisibleToLocalPlayer
            ? new(cardState.FrontTexture, cardState.CardData.Label, cardState.DisplayText)
            : Back(cardState.Faction);

    /// <summary>
    /// The owning faction's card back — what a face-down Response card looks like to a player who
    /// does not control it. Title and Text are empty so CardScene.ShowFace hides the text block
    /// entirely: a face-down card is art only.
    /// </summary>
    public static CardFace Back(Faction faction) =>
        new(FactionState.ForEnum(faction).FactionData.CardBackTexture, "", "");

    public static CardFace Bulletin(string label, string text) =>
        new(BulletinArt.Front, label, text);

    /// <summary>An option drawn as a card: <paramref name="faction"/>'s front for the option's card type, or the Bulletin front.</summary>
    public static CardFace ForOption(InputRequest.OptionCard option, Faction faction)
    {
        Texture2D front = option.ArtCardType == CardType.NONE ? null
            : FactionState.ForEnum(faction)?.FactionData.CardFrontTextures.GetValueOrDefault(option.ArtCardType);
        return new(front ?? BulletinArt.Front, option.Title, option.Text);
    }
}

/// <summary>
/// The shared Bulletin card front, used both by BulletinCardState (an activatable mutator, which is a
/// CardState) and by CardFace.Bulletin (an automatic step mutator, which is not). Loaded lazily rather
/// than in a static initializer so a headless server — which skips card texture loading entirely, see
/// FactionData.LoadTextures — never touches it.
/// </summary>
public static class BulletinArt
{
    private static Texture2D _front;
    public static Texture2D Front =>
        _front ??= GD.Load<Texture2D>("res://assets/textures/Other/Bulletin_Front.png");
}
