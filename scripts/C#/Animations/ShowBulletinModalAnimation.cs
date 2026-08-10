using System.Threading.Tasks;

/// <summary>
/// Announces a firing scenario mutator as a Bulletin card in an auto-fading modal. Mirrors
/// ShowDiscardModalAnimation, except ForFactions is left at its default (every playable faction), so
/// ForMyFaction is true on every GUI peer and all players see it — not just a target.
///
/// Reaching every peer needs no RPC: the owning ShowBulletinPresentationEvent is replicated, so each
/// peer replays it and runs its own copy of this animation.
/// </summary>
public class ShowBulletinModalAnimation : ChangeEventAnimation
{
    private readonly string _label;
    private readonly string _text;

    public ShowBulletinModalAnimation(string label, string text)
    {
        _label = label;
        _text = text;
    }

    protected override async Task AnimateForTargetFaction()
    {
        // Through the notification sink rather than PresentationModal.Current directly, so a headless
        // server no-ops instead of dereferencing a UI singleton it never created. Routes to
        // ModalConfig.Display(...).WithAutoDismiss(): fade in, GameSettings.DurationLong, fade out.
        await PresentationServices.Notification.ShowModal(
            PresentationItemBulletin.Single(CardFace.Bulletin(_label, _text)), "Bulletin");
    }
}
