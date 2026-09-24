using System;

namespace YASS.Core
{
    /// <summary>Where settings are kept between sessions. The Unity layer backs this with PlayerPrefs.</summary>
    public interface ISettingsStore
    {
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        bool GetBool(string key, bool fallback);
        void SetBool(string key, bool value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);

        /// <summary>Flushes pending writes to disk. Costly: a synchronous write on most platforms.</summary>
        void Save();
    }

    /// <summary>
    /// Holds the current settings, loads them at start-up and writes every change back to the store
    /// (GDD "UI Flow and Screens": settings are saved between sessions). Changes are announced so the audio mixer,
    /// the screen and the menus all follow one source of truth.
    ///
    /// Values reach the store as they change, but the store is only flushed to disk by <see cref="Flush"/>:
    /// dragging a volume slider changes it every frame, and flushing each time would hitch.
    /// </summary>
    public sealed class SettingsService
    {
        public const string MusicVolumeKey = "yass.settings.musicVolume";
        public const string SfxVolumeKey = "yass.settings.sfxVolume";
        public const string ScreenShakeKey = "yass.settings.screenShake";
        public const string FullscreenKey = "yass.settings.fullscreen";

        /// <summary>The name that goes on the leaderboards, which is also the account (GDD "Scoring").</summary>
        public const string PlayerNameKey = "yass.settings.playerName";

        /// <summary>Set once the player has said they would rather play without an account.</summary>
        public const string PlaysOfflineKey = "yass.settings.playsOffline";

        readonly ISettingsStore _store;

        bool _dirty;

        public SettingsService(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Settings = Load();
            PlayerName = _store.GetString(PlayerNameKey, string.Empty);
            PlaysOffline = _store.GetBool(PlaysOfflineKey, false);
        }

        public GameSettings Settings { get; private set; }

        /// <summary>
        /// What the player is called on a board. Empty until they have been asked, which is how the game
        /// knows to ask: a name is the one setting that cannot have a sensible default chosen for it.
        /// </summary>
        public string PlayerName { get; private set; } = string.Empty;

        /// <summary>True once a name has been chosen, so the game only asks the once.</summary>
        public bool HasPlayerName => !string.IsNullOrEmpty(PlayerName);

        /// <summary>
        /// True once the player has chosen to play without an account. Remembered, because being asked to
        /// sign up at the start of every session is exactly the nagging a leaderboard is not worth.
        /// </summary>
        /// <remarks>
        /// Nothing sets this back to false yet, so the choice is final on a machine. That is a hole rather
        /// than a decision: see Open Questions, "Account management".
        /// </remarks>
        public bool PlaysOffline { get; private set; }

        /// <summary>Records that the player would rather not have an account, so they are not asked again.</summary>
        public void ChooseOffline()
        {
            if (PlaysOffline) return;

            PlaysOffline = true;
            _store.SetBool(PlaysOfflineKey, true);
            _dirty = true;
        }

        /// <summary>Raised after <see cref="Settings"/> changes. The initial load does not raise it; <see cref="Apply"/> does.</summary>
        public event Action<GameSettings> Changed;

        /// <summary>Re-announces the current settings, so a listener that just woke up can apply them.</summary>
        public void Apply() => Changed?.Invoke(Settings);

        /// <summary>
        /// Whether the window's current state should be taken as the player's wish, rather than the saved
        /// setting being applied to it. Once per session the saved setting wins; after that the window is the
        /// player's, who may have used Alt+Enter or the window chrome. Never in the editor, where the window
        /// belongs to the Editor rather than to the game and adopting it would overwrite the saved choice
        /// with the Game view's state.
        /// </summary>
        public static bool ShouldAdoptTheWindow(bool appliedThisSession, bool inEditor) =>
            appliedThisSession && !inEditor;

        public void SetMusicVolume(float value) => Set(Settings.WithMusicVolume(value));
        public void SetSfxVolume(float value) => Set(Settings.WithSfxVolume(value));
        public void SetScreenShake(bool value) => Set(Settings.WithScreenShake(value));
        public void SetFullscreen(bool value) => Set(Settings.WithFullscreen(value));

        public void ResetToDefaults() => Set(GameSettings.Default);

        /// <summary>
        /// Names the player. The name is their account as well as their board entry, so it is stored as
        /// typed: cleaning it, as this used to, would hand them a name they could no longer sign in with.
        /// <see cref="Credentials.CheckName"/> is what decides whether it may be used at all.
        /// </summary>
        public void SetPlayerName(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed == PlayerName) return;

            PlayerName = trimmed;
            _store.SetString(PlayerNameKey, trimmed);
            _dirty = true;
            Changed?.Invoke(Settings);
        }

        /// <summary>Writes pending changes to disk. Call it when leaving the settings screen or quitting.</summary>
        public void Flush()
        {
            if (!_dirty) return;

            _dirty = false;
            _store.Save();
        }

        void Set(GameSettings next)
        {
            if (next == Settings) return; // nothing changed: no write, no event

            Settings = next;
            Write(next);
            _dirty = true;
            Changed?.Invoke(next);
        }

        GameSettings Load() =>
            new GameSettings(
                _store.GetFloat(MusicVolumeKey, GameSettings.DefaultMusicVolume),
                _store.GetFloat(SfxVolumeKey, GameSettings.DefaultSfxVolume),
                _store.GetBool(ScreenShakeKey, GameSettings.DefaultScreenShake),
                _store.GetBool(FullscreenKey, GameSettings.DefaultFullscreen));

        void Write(GameSettings settings)
        {
            _store.SetFloat(MusicVolumeKey, settings.MusicVolume);
            _store.SetFloat(SfxVolumeKey, settings.SfxVolume);
            _store.SetBool(ScreenShakeKey, settings.ScreenShake);
            _store.SetBool(FullscreenKey, settings.Fullscreen);
        }
    }
}
