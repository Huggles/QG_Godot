using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// The single audio service: one music track at a time, any number of overlapping sound
/// effects, and the three volume levels persisted in <see cref="GameSettings"/>.
///
/// Clips are discovered by scanning <c>res://assets/audio/music</c> and
/// <c>res://assets/audio/sfx</c> once on startup, keyed by filename without extension, so
/// dropping <c>card_flip.ogg</c> into the sfx folder is all it takes to make
/// <c>AudioManager.PlaySfx("card_flip")</c> work. Nothing has to be registered in code and
/// nothing auto-plays — music starts only when something calls <see cref="PlayMusic"/>.
///
/// On top of that, <c>res://assets/audio/sfx/settings.json</c> declares *configurable cues*: a cue
/// has a stable name, a display label, and a list of sounds the player may choose between. Game code
/// raises a cue by name through <see cref="PlaySfxSetting"/> and never names a file, so which sound
/// a cue makes is the player's choice — persisted per cue by <see cref="GameSettings"/> and picked
/// in the sound panel. A cue with no saved choice uses the first sound the data lists.
///
/// Playback is routed through the <c>Music</c> and <c>SFX</c> buses of
/// <c>res://default_bus_layout.tres</c>, both of which send to <c>Master</c>. That is what makes
/// the master level a multiplier over the other two rather than a fourth independent slider.
///
/// Every entry point is a no-op when <see cref="GameContext.IsHeadless"/>: a dedicated server and
/// a CLI/test run have no audio device, so they must never build players or load streams.
/// A name that does not exist logs and returns — a missing sound must never break a game.
/// </summary>
public partial class AudioManager : SingletonNode<AudioManager>
{
    private const string MusicDir = "res://assets/audio/music";
    private const string SfxDir   = "res://assets/audio/sfx";

    public const string MasterBus = "Master";
    public const string MusicBus  = "Music";
    public const string SfxBus    = "SFX";

    /// <summary>Music track names referenced from code, named after the files in the music folder.</summary>
    public const string MenuMusicTrack = "MainMenuMusic";

    /// <summary>
    /// Names of the configurable effects in <see cref="SfxSettingsPath"/>. Call sites name the
    /// *setting*, not a clip — which sound it maps to is the player's choice, so these are the keys
    /// passed to <see cref="PlaySfxSetting"/> and to <c>GameSettings.GetSfxChoice</c>.
    /// </summary>
    public const string MenuButtonClickSetting = "menuButtonclick";
    public const string InputRequestSetting    = "inputRequest";

    private const string SfxSettingsPath = "res://assets/audio/sfx/settings.json";

    /// <summary>Effects playable at the same instant before the pool has to grow.</summary>
    private const int InitialSfxPlayers = 8;

    /// <summary>
    /// Ceiling on simultaneous effects. Reached only by a runaway caller, and dropping the
    /// newest effect is far better than letting the pool grow without bound.
    /// </summary>
    private const int MaxSfxPlayers = 32;

    private static readonly string[] AudioExtensions = { ".ogg", ".wav", ".mp3" };

    /// <summary>
    /// Suffixes Godot appends to resource names inside an exported PCK. A directory listing there
    /// yields <c>track.ogg.import</c> rather than <c>track.ogg</c>, so stripping these is what keeps
    /// discovery working outside the editor.
    /// </summary>
    private static readonly string[] ImportSuffixes = { ".import", ".remap" };

    /// <summary>
    /// Music is discovered as name → resource path and loaded on first play, then cached. A music
    /// file is a whole song and can run to tens of megabytes, all of which an AudioStream holds in
    /// memory — eager-loading the folder would put that on the boot path for tracks that may never
    /// be played.
    /// </summary>
    private readonly Dictionary<string, string> _musicPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AudioStream> _musicCache =
        new Dictionary<string, AudioStream>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Effects are loaded up front, unlike music: they are small, and the first play of one happens
    /// at a moment that matters, so a load hitch there would be heard.
    /// </summary>
    private readonly Dictionary<string, AudioStream> _sfx =
        new Dictionary<string, AudioStream>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The configurable effects, in the order <c>settings.json</c> lists them, so the sound panel
    /// renders them in the order the data author chose rather than an arbitrary one.
    /// </summary>
    private readonly List<SfxSettingData> _sfxSettings = new List<SfxSettingData>();

    private AudioStreamPlayer _musicPlayer;
    private readonly List<AudioStreamPlayer> _sfxPlayers = new List<AudioStreamPlayer>();

    private bool _loopCurrentMusic;
    private bool _enabled;

    /// <summary>Name of the track currently playing, or null when nothing is.</summary>
    public string CurrentMusic { get; private set; }

    public IReadOnlyCollection<string> MusicNames => _musicPaths.Keys.ToList();
    public IReadOnlyCollection<string> SfxNames   => _sfx.Keys.ToList();

    /// <summary>The configurable effects, for the sound panel to build a row per entry.</summary>
    public IReadOnlyList<SfxSettingData> SfxSettings => _sfxSettings;

    public override void _Ready()
    {
        base._Ready();

        // No audio device in a dedicated server or CLI run. Bail out before loading a single
        // stream: the dictionaries stay empty and every Play* below early-returns on _enabled.
        if (GameContext.IsHeadless)
        {
            DebugUtilities.PrintPeerFinest("AudioManager: headless run, audio disabled.");
            return;
        }

        _enabled = true;

        ScanDirectory(MusicDir, _musicPaths);

        Dictionary<string, string> sfxPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ScanDirectory(SfxDir, sfxPaths);
        foreach (KeyValuePair<string, string> sfx in sfxPaths)
        {
            AudioStream stream = LoadStream(sfx.Value);
            if (stream != null)
            {
                _sfx[sfx.Key] = stream;
            }
        }

        _musicPlayer = new AudioStreamPlayer { Name = "MusicPlayer", Bus = MusicBus };
        AddChild(_musicPlayer);
        _musicPlayer.Finished += OnMusicFinished;

        for (int i = 0; i < InitialSfxPlayers; i++)
        {
            AddSfxPlayer();
        }

        LoadSfxSettings();

        ApplyVolumes();

        DebugUtilities.PrintPeer($"AudioManager ready: {_musicPaths.Count} music track(s), "
            + $"{_sfx.Count} sound effect(s), {_sfxSettings.Count} configurable cue(s).");
    }

    // ---------------------------------------------------------------- discovery

    /// <summary>
    /// Maps every audio file under <paramref name="path"/> as clip name → resource path, recursing
    /// into subfolders. Nothing is loaded here. A missing folder is not an error: Godot omits empty
    /// directories from exports, and these folders start out empty.
    /// </summary>
    private static void ScanDirectory(string path, Dictionary<string, string> target)
    {
        using DirAccess dir = DirAccess.Open(path);
        if (dir == null)
        {
            DebugUtilities.PrintPeerFinest($"AudioManager: no audio folder at {path} (nothing to load).");
            return;
        }

        foreach (string subDirectory in dir.GetDirectories())
        {
            ScanDirectory($"{path}/{subDirectory}", target);
        }

        // A dev filesystem lists both `track.ogg` and `track.ogg.import`, while an exported PCK lists
        // only the latter. Stripping the suffix collapses that pair, so the distinct names have to be
        // gathered before loading — otherwise every clip in the editor reads as a duplicate of itself.
        HashSet<string> fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in dir.GetFiles())
        {
            string candidate = StripImportSuffix(entry);
            string candidateExtension = $".{candidate.GetExtension()}".ToLowerInvariant();
            if (AudioExtensions.Contains(candidateExtension))
            {
                fileNames.Add(candidate);
            }
        }

        foreach (string fileName in fileNames)
        {
            string clipName = fileName.GetBaseName();
            string resourcePath = $"{path}/{fileName}";

            if (target.ContainsKey(clipName))
            {
                DebugUtilities.PrintPeerError(
                    $"AudioManager: duplicate clip name {clipName} at {resourcePath}; keeping the first one found.");
                continue;
            }

            target[clipName] = resourcePath;
        }
    }

    private static AudioStream LoadStream(string resourcePath)
    {
        AudioStream stream = GD.Load<AudioStream>(resourcePath);
        if (stream == null)
        {
            DebugUtilities.PrintPeerError($"AudioManager: failed to load audio stream {resourcePath}.");
        }
        return stream;
    }

    /// <summary>
    /// Loads a music track on first use and caches it, so a track is read from disk once per run
    /// however many times it is played. Null when the name is unknown or the file will not load.
    /// </summary>
    private AudioStream ResolveMusic(string name)
    {
        if (_musicCache.TryGetValue(name, out AudioStream cached))
        {
            return cached;
        }

        if (!_musicPaths.TryGetValue(name, out string resourcePath))
        {
            return null;
        }

        AudioStream stream = LoadStream(resourcePath);
        if (stream != null)
        {
            _musicCache[name] = stream;
        }
        return stream;
    }

    /// <summary>
    /// Reads the configurable effects from <see cref="SfxSettingsPath"/>. Missing or malformed data
    /// leaves the list empty: the panel then shows only the volume sliders and every cue falls back
    /// to nothing, which is a far better failure than a startup crash over a sound.
    /// </summary>
    private void LoadSfxSettings()
    {
        if (!Godot.FileAccess.FileExists(SfxSettingsPath))
        {
            DebugUtilities.PrintPeerFinest($"AudioManager: no sfx settings at {SfxSettingsPath}.");
            return;
        }

        try
        {
            using Godot.FileAccess file = Godot.FileAccess.Open(SfxSettingsPath, Godot.FileAccess.ModeFlags.Read);
            SfxSettingsFileData parsed = JsonSerializer.Deserialize<SfxSettingsFileData>(file.GetAsText());

            foreach (SfxSettingData setting in parsed?.Settings ?? new List<SfxSettingData>())
            {
                if (string.IsNullOrEmpty(setting.Name))
                {
                    DebugUtilities.PrintPeerError("AudioManager: an sfx setting has no name; skipped.");
                    continue;
                }

                // Options whose file is not among the scanned clips are dropped here rather than at
                // play time, so a typo in the data shows up once at startup instead of as a cue that
                // silently does nothing, and never reaches the picklist.
                List<SfxSoundFileData> playable = setting.SoundFiles
                    .Where(option => ResolveSfxOption(setting, option) != null)
                    .ToList();

                if (playable.Count == 0)
                {
                    DebugUtilities.PrintPeerError($"AudioManager: sfx setting {setting.Name} has no playable sound; skipped.");
                    continue;
                }

                setting.SoundFiles = playable;
                _sfxSettings.Add(setting);
            }
        }
        catch (Exception e)
        {
            DebugUtilities.PrintPeerError($"AudioManager: could not read {SfxSettingsPath}: {e.Message}");
        }
    }

    /// <summary>
    /// The clip name an option points at, or null when it does not resolve. The data carries a path
    /// without an extension; only its filename is used, matched against the scanned sfx folder, so
    /// the scan stays the single authority on what can actually be played.
    /// </summary>
    private string ResolveSfxOption(SfxSettingData setting, SfxSoundFileData option)
    {
        if (string.IsNullOrEmpty(option?.File))
        {
            DebugUtilities.PrintPeerError($"AudioManager: option {option?.Name} of {setting.Name} has no file.");
            return null;
        }

        string clipName = option.File.GetFile();
        if (!_sfx.ContainsKey(clipName))
        {
            DebugUtilities.PrintPeerError(
                $"AudioManager: option {option.Name} of {setting.Name} points at {option.File}, "
                + $"which is not in the sfx folder. Known effects: {DescribeKnown(_sfx.Keys)}.");
            return null;
        }

        return clipName;
    }

    /// <summary>Removes a trailing <c>.import</c> / <c>.remap</c> so exported builds see real filenames.</summary>
    private static string StripImportSuffix(string fileName)
    {
        foreach (string suffix in ImportSuffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return fileName.Substring(0, fileName.Length - suffix.Length);
            }
        }
        return fileName;
    }

    // ---------------------------------------------------------------- music

    /// <summary>
    /// Ensures <paramref name="name"/> is the track playing, replacing whatever was — there is only
    /// ever one music player, so music can never stack.
    ///
    /// Asking for the track that is already playing is a no-op rather than a restart. That is what
    /// lets every menu screen call this in its own right without the music jumping back to the top
    /// on each navigation. Use <see cref="StopMusic"/> first to deliberately restart a track.
    /// </summary>
    public static void PlayMusic(string name, bool loop = true) => Instance?.PlayMusicInternal(name, loop);

    /// <summary>Starts a random track from the music folder. No-op when the folder is empty.</summary>
    public static void PlayRandomMusic(bool loop = true) => Instance?.PlayRandomMusicInternal(loop);

    public static void StopMusic() => Instance?.StopMusicInternal();

    private void PlayMusicInternal(string name, bool loop)
    {
        if (!_enabled)
        {
            return;
        }

        // Already the running track: keep playing it rather than restarting from the top.
        if (CurrentMusic == name && _musicPlayer.Playing)
        {
            _loopCurrentMusic = loop;
            return;
        }

        AudioStream stream = ResolveMusic(name);
        if (stream == null)
        {
            DebugUtilities.PrintPeerError($"AudioManager: no music track named {name}. Known tracks: {DescribeKnown(_musicPaths.Keys)}.");
            return;
        }

        _loopCurrentMusic = loop;
        CurrentMusic = name;
        _musicPlayer.Stream = stream;
        _musicPlayer.Play();
    }

    private void PlayRandomMusicInternal(bool loop)
    {
        if (!_enabled || _musicPaths.Count == 0)
        {
            return;
        }

        List<string> names = _musicPaths.Keys.ToList();
        PlayMusicInternal(names[GD.RandRange(0, names.Count - 1)], loop);
    }

    private void StopMusicInternal()
    {
        if (!_enabled)
        {
            return;
        }

        // Cleared before Stop() so the Finished handler cannot restart the track we just stopped.
        _loopCurrentMusic = false;
        CurrentMusic = null;
        _musicPlayer.Stop();
    }

    /// <summary>
    /// Loops by replaying rather than by setting <c>Loop</c> on the stream: that flag lives on the
    /// imported resource, which is shared, so writing it would silently change the clip for every
    /// other caller.
    /// </summary>
    private void OnMusicFinished()
    {
        if (_loopCurrentMusic && _musicPlayer.Stream != null)
        {
            _musicPlayer.Play();
            return;
        }

        CurrentMusic = null;
    }

    // ---------------------------------------------------------------- sound effects

    /// <summary>
    /// Plays <paramref name="name"/> on a free pooled player, so any number of effects overlap.
    /// </summary>
    public static void PlaySfx(string name, float pitchScale = 1f) => Instance?.PlaySfxInternal(name, pitchScale);

    /// <summary>
    /// Plays a configurable cue by its *setting* name (e.g. <see cref="InputRequestSetting"/>) using
    /// whichever sound the player chose for it. This is what game code calls: it must not care which
    /// file is currently selected, only which cue it is raising.
    /// </summary>
    public static void PlaySfxSetting(string settingName) => Instance?.PlaySfxSettingInternal(settingName);

    private void PlaySfxSettingInternal(string settingName)
    {
        if (!_enabled)
        {
            return;
        }

        string clipName = SelectedClipFor(settingName);
        if (clipName == null)
        {
            DebugUtilities.PrintPeerError($"AudioManager: no sfx setting named {settingName} in settings.json.");
            return;
        }

        PlaySfxInternal(clipName, 1f);
    }

    /// <summary>
    /// The option currently selected for a setting, falling back to the first one the data lists when
    /// the player has never chosen — so a cue is audible before anybody opens the sound panel, and a
    /// saved choice that no longer exists in the data degrades to the default instead of going silent.
    /// </summary>
    public SfxSoundFileData SelectedOptionFor(string settingName)
    {
        SfxSettingData setting = _sfxSettings.FirstOrDefault(entry => entry.Name == settingName);
        if (setting == null || setting.SoundFiles.Count == 0)
        {
            return null;
        }

        string savedChoice = GameSettings.Instance?.GetSfxChoice(settingName);
        return setting.SoundFiles.FirstOrDefault(option => option.Name == savedChoice)
               ?? setting.SoundFiles[0];
    }

    /// <summary>The clip name a setting currently resolves to, or null when the setting is unknown.</summary>
    private string SelectedClipFor(string settingName)
    {
        SfxSoundFileData option = SelectedOptionFor(settingName);
        return option == null ? null : option.File.GetFile();
    }

    private void PlaySfxInternal(string name, float pitchScale)
    {
        if (!_enabled)
        {
            return;
        }

        if (!_sfx.TryGetValue(name, out AudioStream stream))
        {
            DebugUtilities.PrintPeerError($"AudioManager: no sound effect named {name}. Known effects: {DescribeKnown(_sfx.Keys)}.");
            return;
        }

        AudioStreamPlayer player = TakeFreeSfxPlayer();
        if (player == null)
        {
            DebugUtilities.PrintPeerFinest($"AudioManager: all {MaxSfxPlayers} sfx players busy, dropping {name}.");
            return;
        }

        player.Stream = stream;
        player.PitchScale = pitchScale;
        player.Play();
    }

    private AudioStreamPlayer TakeFreeSfxPlayer()
    {
        foreach (AudioStreamPlayer player in _sfxPlayers)
        {
            if (!player.Playing)
            {
                return player;
            }
        }

        return _sfxPlayers.Count < MaxSfxPlayers ? AddSfxPlayer() : null;
    }

    private AudioStreamPlayer AddSfxPlayer()
    {
        AudioStreamPlayer player = new AudioStreamPlayer
        {
            Name = $"SfxPlayer{_sfxPlayers.Count}",
            Bus  = SfxBus,
        };
        AddChild(player);
        _sfxPlayers.Add(player);
        return player;
    }

    // ---------------------------------------------------------------- volume

    /// <summary>Pushes the persisted <see cref="GameSettings"/> levels onto the three buses.</summary>
    public void ApplyVolumes()
    {
        GameSettings settings = GameSettings.Instance;
        if (settings == null)
        {
            return;
        }

        SetBusVolume(MasterBus, settings.MasterVolume);
        SetBusVolume(MusicBus,  settings.MusicVolume);
        SetBusVolume(SfxBus,    settings.SfxVolume);
    }

    /// <summary>
    /// Applies a 0..1 level to a bus without persisting it — this is what a slider calls while it is
    /// being dragged, so the player hears the change without rewriting settings.cfg every frame.
    /// </summary>
    public static void SetBusVolume(string bus, float linear)
    {
        int index = AudioServer.GetBusIndex(bus);
        if (index < 0)
        {
            DebugUtilities.PrintPeerError($"AudioManager: no audio bus named {bus}. Is default_bus_layout.tres set in project settings?");
            return;
        }

        float clamped = Mathf.Clamp(linear, 0f, 1f);
        // Mute rather than lean on LinearToDb(0), which is negative infinity.
        AudioServer.SetBusMute(index, clamped <= 0.001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(clamped, 0.001f)));
    }

    private static string DescribeKnown(IEnumerable<string> clipNames)
    {
        List<string> names = clipNames.OrderBy(name => name).ToList();
        return names.Count == 0 ? "(none found)" : string.Join(", ", names);
    }
}
