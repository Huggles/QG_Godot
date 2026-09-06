using System.Threading.Tasks;

/// <summary>
/// Fades the turn badge in over the middle of the screen, holds it, and fades it out.
/// </summary>
public class ShowTurnBadgeAnimation : ChangeEventAnimation
{
    private readonly Faction _faction;

    public ShowTurnBadgeAnimation(Faction faction)
    {
        // The badge only announces. Making the queue wait on it would add its whole run to every
        // turn boundary — six times a round — and buy nothing, since it overlays the board rather
        // than competing with it for the same space.
        BlockQueue = false;
        _faction = faction;
    }

    protected override Task AnimateForTargetFaction() => TurnBadge.Announce(_faction);

    /// <summary>
    /// Whose turn it is, is news to everyone. Overridden because the default enemy-side animation is
    /// to do nothing, which would leave a peer that controls no playable faction — a spectator —
    /// with no announcement at all.
    /// </summary>
    protected override Task AnimateForEnemyFaction() => TurnBadge.Announce(_faction);
}
