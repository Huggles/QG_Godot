using Godot;

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
        new(cardState.FrontTexture, cardState.CardData.Label, cardState.CardData.Text);

    public static CardFace Bulletin(string label, string text) =>
        new(BulletinArt.Front, label, text);
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
