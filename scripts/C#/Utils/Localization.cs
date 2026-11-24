using Godot;
using System.Linq;

/// <summary>
/// The languages the game can be played in, and switching between them.
///
/// Every piece of display text is a translation key (e.g. <c>MENU_BTN_SKIRMISH</c>), never the English
/// text itself. Each language has its own gettext file, <c>res://localization/&lt;code&gt;.po</c>,
/// registered under Project Settings → Localization → Translations. Scene text (Label, Button,
/// OptionButton items, tab titles) is looked up by Godot automatically; text set from code goes
/// through <c>Tr("KEY")</c>. See <c>docs/LOCALIZATION.md</c>.
///
/// The player's choice is stored by <see cref="GameSettings"/>; this class only knows what exists and
/// how to make it current.
/// </summary>
public static class Localization
{
    public const string FallbackLanguage = "en";

    /// <summary>
    /// Every language with a translation file, in picker order. Each name is written in its own
    /// language and never translated, so a player who switched by mistake can still find their own.
    /// Adding a language: add its .po file, register it in project.godot, add it here.
    /// </summary>
    public static readonly (string Code, string NativeName)[] SupportedLanguages =
    {
        ("en", "English"),
        ("nl", "Nederlands"),
    };

    public static bool IsSupported(string code)
        => !string.IsNullOrEmpty(code) && SupportedLanguages.Any(language => language.Code == code);

    /// <summary>
    /// The language a first launch starts in: the operating system's, when the game has it.
    /// </summary>
    public static string DefaultLanguage()
    {
        string system = OS.GetLocaleLanguage();
        return IsSupported(system) ? system : FallbackLanguage;
    }

    /// <summary>
    /// True when the launch already picked a locale: <c>--language xx</c> on the command line or the
    /// <c>internationalization/locale/test</c> project setting. Godot strips its own flags from
    /// <see cref="OS.GetCmdlineArgs"/>, so this is read off the result instead: left alone, the
    /// TranslationServer starts on the operating system's locale. Call before anything sets the locale.
    /// </summary>
    public static bool IsForcedAtLaunch()
        => TranslationServer.GetLocale() != TranslationServer.StandardizeLocale(OS.GetLocale());

    /// <summary>
    /// Makes <paramref name="code"/> the current language. Every node is notified
    /// (<c>NotificationTranslationChanged</c>), so open screens re-translate in place.
    /// </summary>
    public static void Apply(string code)
    {
        TranslationServer.SetLocale(IsSupported(code) ? code : FallbackLanguage);
    }
}
