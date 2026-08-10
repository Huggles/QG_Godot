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
}
