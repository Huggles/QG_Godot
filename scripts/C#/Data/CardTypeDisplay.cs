/// <summary>
/// The single source of user-facing card type text: "Economic Warfare", never
/// "ECONOMIC_WARFARE". Plays the same role for <see cref="CardType"/> that
/// <see cref="FactionDisplay"/> plays for <see cref="Faction"/>.
///
/// NOT for texture paths: <c>FactionData.LoadCardTextures</c> builds those from the raw enum name
/// on purpose, and the CLI views deliberately print the raw enum because that is what .qgc scripts
/// type.
/// </summary>
public static class CardTypeDisplay
{
    public static string Label(this CardType cardType) => cardType switch
    {
        CardType.NONE              => "",
        CardType.BUILD_ARMY        => "Build Army",
        CardType.BUILD_NAVY        => "Build Navy",
        CardType.LAND_BATTLE       => "Land Battle",
        CardType.SEA_BATTLE        => "Sea Battle",
        CardType.EVENT             => "Event",
        CardType.RESPONSE          => "Response",
        CardType.STATUS            => "Status",
        CardType.ECONOMIC_WARFARE  => "Economic Warfare",
        _                          => cardType.ToString(),
    };
}
