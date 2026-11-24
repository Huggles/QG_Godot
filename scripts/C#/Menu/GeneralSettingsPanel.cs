using Godot;

/// <summary>
/// The General tab of <see cref="SettingsDialog"/>: the language the game is shown in.
///
/// Applied live, like <see cref="GameplaySettingsPanel"/>: switching the locale re-translates this
/// dialog and the menu behind it in place, and the picker shows every language in its own name, so a
/// wrong pick can always be undone from the same control.
/// </summary>
public partial class GeneralSettingsPanel : VBoxContainer
{
    private OptionButton _language;

    public override void _Ready() => Guard.Try(ReadyInternal, "GeneralSettingsPanel._Ready");

    private void ReadyInternal()
    {
        _language = GetNode<OptionButton>("%LanguageOption");

        // Native names ("Nederlands"), never translated: the scene switches auto-translation off on
        // the picker so a name can never be mistaken for a key.
        _language.Clear();
        string current = GameSettings.Instance.Language;
        for (int i = 0; i < Localization.SupportedLanguages.Length; i++)
        {
            (string code, string nativeName) = Localization.SupportedLanguages[i];
            _language.AddItem(nativeName, i);
            if (code == current) _language.Selected = i;
        }

        // Wired after seeding, so opening the dialog does not write the value straight back.
        _language.ItemSelected += index =>
            GameSettings.Instance.SetLanguage(Localization.SupportedLanguages[_language.GetItemId((int)index)].Code);
    }
}
