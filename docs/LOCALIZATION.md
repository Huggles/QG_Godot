# Localization

The game uses Godot's built-in translation system ([Internationalizing games](https://docs.godotengine.org/en/stable/tutorials/i18n/internationalizing_games.html)) with **gettext `.po` files, one per language**.

## Rules

- **Display text is always a key, never the text itself.** Scenes say `text = "MENU_HDR_PLAY"`, and code says `ButtonText = "MENU_BTN_SKIRMISH"` or `Tr("MENU_INVITE_PROMPT")`.
- **Every key lives in every language file.** `localization/en.po` holds the English. Each other language file holds the same keys, with a `#. en:` comment above each entry showing the English source text.
- **Key names** are upper snake case, `SCREEN_KIND_NAME`:
  - `MENU_BTN_QUIT`
  - `SETTINGS_GAMEPLAY_SPEED_HINT`
  - Text shared across screens uses `COMMON_`, e.g. `COMMON_BTN_CLOSE`.
- **Placeholders** use `{0}`, `{1}`, so a translation can reorder them: `string.Format(Tr("MENU_INVITE_PROMPT"), inviter)`.
- **Literal text** that must never be looked up (numbers, percentages, player names, language names) goes on a node with `auto_translate_mode = 2` (Disabled).

## How text gets translated

| Where | How |
|---|---|
| Label / Button / CheckButton `text`, OptionButton items in a `.tscn` | Automatic: Godot looks the key up and re-translates on a language change. |
| TabContainer tab titles | Set to keys with `SetTabTitle` (see `SettingsDialog.cs`); translated automatically. |
| `MenuPanelButton.ButtonText` | Pass a key. The button translates it, and re-translates on `NotificationTranslationChanged`. |
| Text built in code | `Tr("KEY")`. If the screen can be open while the language changes, rebuild it on `NotificationTranslationChanged`. |

A key missing from a language falls back to English (`internationalization/locale/fallback="en"`). A key missing everywhere shows the raw key on screen.

## The language setting

- `GameSettings.Language` stores the language code (`language` in `user://settings.cfg`).
- On first launch the game uses the operating system's language if it is supported, otherwise English.
- The player picks the language under **Settings → General → Language**. The change applies live.
- `Localization.cs` lists the supported languages and switches the locale (`TranslationServer.SetLocale`).

## Adding a language

1. Copy `localization/en.po` to `localization/<code>.po`. Set `"Language: <code>\n"` in the header, translate every `msgstr`, and add `#. en:` comments. Poedit or any gettext editor works.
2. Add the file to `internationalization/locale/translations` in `project.godot` (Project Settings → Localization → Translations).
3. Add `("<code>", "<native name>")` to `Localization.SupportedLanguages`.
4. Check the menu fonts render the language's characters.

## Adding text

1. Add the key and English text to `en.po`.
2. Add the same key, with a `#. en:` comment and the translation, to every other `.po`.
3. Use the key in the scene or code.

## Testing

- Run with `--language nl` (Godot's own switch). It overrides the saved setting for that launch.
- In the editor, **View → Preview Translation** shows scenes in a chosen language.
- Turn on **Pseudolocalization** in the advanced Project Settings, without committing it, to find untranslated text and layouts that clip longer strings.
- Check that both files hold the same keys and are valid gettext: `msgfmt --check -o /dev/null localization/nl.po`.

## Status

Translated:
- all menu screens: main menu, Settings, Skirmish, Lobby, Host/Join, Friends' games, Load Game, Leaderboard, Invite, Tutorials list, Victory, Disclaimer, the in-game Esc menu, notices and toasts
- the fixed text on in-game screens: chat, round track, prompts, reaction controls, connection and error dialogs, score history, card distribution, turn badge
- scenario titles and descriptions (`SCENARIO_<NAME>_TITLE`, falling back to the scenario file)
- sound-effect names (`SFX_<NAME>`, falling back to `settings.json`)

Data-driven text uses `Localization.TranslateOr(key, english)` or `Localization.KeyOr(key, english)`, so the data file keeps its English and a translation is optional per item.

Still English (see `docs/LOCALIZATION_INVENTORY.md`):
- text the host sends to other players: lobby chat lines, end reasons, prompts, action labels
- faction, country, card and card-type names
- card text, tutorials, history and card art
