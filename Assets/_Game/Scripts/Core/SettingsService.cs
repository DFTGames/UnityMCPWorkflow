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

        readonly ISettingsStore _store;

        bool _dirty;

        public SettingsService(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Settings = Load();
        }

        public GameSettings Settings { get; private set; }

        /// <summary>Raised after <see cref="Settings"/> changes. The initial load does not raise it; <see cref="Apply"/> does.</summary>
        public event Action<GameSettings> Changed;

        /// <summary>Re-announces the current settings, so a listener that just woke up can apply them.</summary>
        public void Apply() => Changed?.Invoke(Settings);

        public void SetMusicVolume(float value) => Set(Settings.WithMusicVolume(value));
        public void SetSfxVolume(float value) => Set(Settings.WithSfxVolume(value));
        public void SetScreenShake(bool value) => Set(Settings.WithScreenShake(value));
        public void SetFullscreen(bool value) => Set(Settings.WithFullscreen(value));

        public void ResetToDefaults() => Set(GameSettings.Default);

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
