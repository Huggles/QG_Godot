using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The game's side of chat: names and teams read live from <see cref="PlayerFactionRegistry"/>.
/// Two peers are teammates when they play a faction on the same side. Chat speaks in connection ids,
/// the registry in seat ids, which differ for a player who rejoined (<see cref="RejoinService"/>).
/// </summary>
public sealed class PlayerRegistryChatRoster : IChatRoster
{
    public string NameOf(int peerId)
    {
        string name = PlayerFactionRegistry.GetFactionsForPeerId(RejoinService.ToSeat(peerId))
            .Select(PlayerFactionRegistry.GetDisplayNameForFaction)
            .FirstOrDefault(n => n != null);
        return name ?? $"Player {peerId}";
    }

    public bool AreTeammates(int sender, int recipient)
        => TeamsOf(sender).Overlaps(TeamsOf(recipient));

    public bool CanHear(int peerId) => RejoinService.IsAdmitted(peerId);

    private static HashSet<FactionTeam> TeamsOf(int peerId)
        => PlayerFactionRegistry.GetFactionsForPeerId(RejoinService.ToSeat(peerId))
            .Select(StaticGameData.FactionTeamForFaction)
            .Where(t => t is FactionTeam.AXIS or FactionTeam.ALLIES)
            .ToHashSet();
}
