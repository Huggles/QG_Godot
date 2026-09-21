/// <summary>
/// Static locator for the input seam, mirroring <see cref="PresentationServices"/>. Resolved once,
/// lazily, so it is safe to touch before the scene tree exists.
///
/// <see cref="Override"/> exists because the CLI provider is installed by <c>CliSession</c> once its
/// transport is up, which is later than first resolution — and because an auto-pass run wants to
/// swap the provider mid-game.
/// </summary>
public static class InputServices
{
    private static IInputProvider _provider;
    private static IInputProvider _override;

    public static IInputProvider Provider
        => _override ?? (_provider ??= GameContext.HasScriptedInput
            ? new AutoPassInputProvider()   // replaced by CliSession once the transport is up
            : new GodotInputProvider());

    /// <summary>Install a provider, replacing whatever was resolved. Pass null to fall back.</summary>
    public static void Override(IInputProvider provider) => _override = provider;

    private static System.Func<Faction, object> _promptGroupKey;

    /// <summary>
    /// Which factions may share one prompt. <c>CardPlayRound.TakeTeamTurn</c> groups a reacting team's
    /// factions by this key and raises ONE input request per group, so a player holding several factions
    /// answers once with everything all of them can react with.
    ///
    /// The default is the controlling seat, which is the whole point: one seat, one person, one prompt.
    /// Deliberately the registry id (<c>GetPeerIdForFaction</c>) and not the answering peer — the latter
    /// folds an AI seat onto the host and would merge a bot's faction into the host player's prompt.
    ///
    /// It is a seam because a seat is not always one decision-maker. A tutorial runs single-player with
    /// all six factions on peer 1 and routes on TargetFaction, so the learner must not share a prompt
    /// with the scripted factions on their own team — <see cref="OverridePromptGroupKey"/> is how
    /// TutorialRuntime keeps them apart. It also serves as the kill switch: return the faction itself and
    /// every prompt goes back to being per faction.
    /// </summary>
    public static System.Func<Faction, object> PromptGroupKey
        => _promptGroupKey ?? (faction => PlayerFactionRegistry.GetPeerIdForFaction(faction));

    /// <summary>Install a grouping key, or pass null to go back to grouping by seat.</summary>
    public static void OverridePromptGroupKey(System.Func<Faction, object> key) => _promptGroupKey = key;
}
