using Godot;
using System.Collections.Generic;

/// <summary>
/// One of the extra skip buttons shown alongside <see cref="SelectionSkipButton"/> while a reaction
/// window is open. Plain Skip passes on this window only; these pass and additionally tell the host
/// to stop opening the information-hiding reaction windows for the rest of the turn step or round.
///
/// Only shown for reaction prompts (<see cref="InputRequest.IsReactionWindow"/>) — a board selection
/// or the faction's own reaction-depth-0 play has no scope to skip.
///
/// Follows <see cref="SelectionSkipButton"/>'s pattern: extends <see cref="MenuPanelButton"/> for the
/// standard look, and only the locally-authoritative instance registers itself.
/// </summary>
public partial class ReactionSkipScopeButton : MenuPanelButton
{
    /// <summary>The locally-authoritative instances, in scene order.</summary>
    private static readonly List<ReactionSkipScopeButton> Local = new();

    [Export] public ReactionSkipScope Scope { get; set; } = ReactionSkipScope.TURN_STEP;

    public static void ShowAll() => Local.ForEach(button => button.Show());
    public static void HideAll() => Local.ForEach(button => button.Hide());

    private void OnPressed()
    {
        EventBus.Emit("ReactionSkipScoped", (int)Scope);
    }

    public override void _Ready()
    {
        base._Ready();

        ButtonText = Scope == ReactionSkipScope.ROUND
            ? "Skip rest of round"
            : "Skip rest of turn step";

        // Spell the limit out: the button does not sign away the reactions everyone can already see
        // you holding, only the empty prompts that exist to cover your face-down cards.
        TooltipText = "Stop asking me for reactions. You will still be asked when you have a face-up card that can react.";

        if (GetMultiplayerAuthority() == Multiplayer.GetUniqueId())
        {
            Local.Add(this);
        }

        Visible = false;
        Pressed += OnPressed;
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
        Local.Remove(this);
    }
}
