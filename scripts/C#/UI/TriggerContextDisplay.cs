using Godot;

/// <summary>
/// The side panel that answers "why am I being asked this?" — it shows the thing that caused the
/// current prompt, as a card, with a one-line summary underneath.
///
/// Lives in its own scene rather than inside FactionHandDisplay, where it used to be. That parent is
/// hidden except during a card-selection prompt and its Hide() cleared this panel outright, so it
/// could never appear beside a unit- or country-selection prompt — which is exactly what a scenario
/// mutator raises. Standing on its own, it serves both card reactions and Bulletins.
/// </summary>
public partial class TriggerContextDisplay : Control, LoadableUI
{
    public static TriggerContextDisplay Current;

    private Panel Panel => GetNode<Panel>("%TriggerContextPanel");
    private CardScene CardSceneNode => GetNode<CardScene>("%TriggerCard");
    private RichTextLabel Label => GetNode<RichTextLabel>("%TriggerLabel");

    public override void _Ready()
    {
        if (GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            Current = this;
            LoadUI();
        }
    }

    public void LoadUI()
    {
        CardSceneNode.TriggersEmphasis(false);
        CardSceneNode.SetClickable(false);
        Panel.Visible = false;
    }

    /// <summary>Show a real card as the trigger — a played card being reacted to or blocked.</summary>
    public void ShowCard(int cardId, string summaryText)
    {
        if (Panel == null) return;

        if (cardId > -1)
        {
            CardSceneNode.Visible = true;
            CardSceneNode.ShowCard(cardId);
            CardSceneNode.SetActivatable(true);
        }
        else
        {
            CardSceneNode.Visible = false;
        }

        Show(summaryText);
    }

    /// <summary>Show a Bulletin as the trigger — a scenario mutator asking this player for input.</summary>
    public void ShowBulletin(CardFace face, string summaryText)
    {
        if (Panel == null) return;

        CardSceneNode.Visible = true;
        CardSceneNode.ShowFace(face);
        CardSceneNode.SetActivatable(true);
        Show(summaryText);
    }

    private void Show(string summaryText)
    {
        Label.Text = $"[b]Reacting to:[/b]\n{summaryText ?? string.Empty}";
        Panel.Visible = true;
    }

    public new void Hide()
    {
        if (Panel != null)
            Panel.Visible = false;
    }
}
