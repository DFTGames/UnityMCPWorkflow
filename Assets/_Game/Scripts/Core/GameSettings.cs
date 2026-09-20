using System;

namespace YASS.Core
{
    /// <summary>
    /// The player's saved preferences (GDD "UI Flow and Screens", Settings). Immutable: every change produces a
    /// new value, so a screen can show pending settings without touching what is saved.
    /// </summary>
    public readonly struct GameSettings : IEquatable<GameSettings>
    {
        public const float DefaultMusicVolume = 0.7f;
        public const float DefaultSfxVolume = 0.9f;
        public const bool DefaultScreenShake = true;
        public const bool DefaultFullscreen = true;

        public GameSettings(float musicVolume, float sfxVolume, bool screenShake, bool fullscreen)
        {
            MusicVolume = Sane(musicVolume, DefaultMusicVolume);
            SfxVolume = Sane(sfxVolume, DefaultSfxVolume);
            ScreenShake = screenShake;
            Fullscreen = fullscreen;
        }

        /// <summary>0 to 1.</summary>
        public float MusicVolume { get; }

        /// <summary>0 to 1.</summary>
        public float SfxVolume { get; }

        public bool ScreenShake { get; }

        /// <summary>Desktop only; ignored where the platform owns the window.</summary>
        public bool Fullscreen { get; }

        public static GameSettings Default =>
            new GameSettings(DefaultMusicVolume, DefaultSfxVolume, DefaultScreenShake, DefaultFullscreen);

        public GameSettings WithMusicVolume(float value) => new GameSettings(value, SfxVolume, ScreenShake, Fullscreen);
        public GameSettings WithSfxVolume(float value) => new GameSettings(MusicVolume, value, ScreenShake, Fullscreen);
        public GameSettings WithScreenShake(bool value) => new GameSettings(MusicVolume, SfxVolume, value, Fullscreen);
        public GameSettings WithFullscreen(bool value) => new GameSettings(MusicVolume, SfxVolume, ScreenShake, value);

        /// <summary>
        /// Keeps a volume usable: out-of-range values are clamped, and a corrupt saved value (NaN) falls back to
        /// the default rather than silently muting the game.
        /// </summary>
        static float Sane(float value, float fallback)
        {
            if (float.IsNaN(value)) return fallback;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        public bool Equals(GameSettings other) =>
            MusicVolume.Equals(other.MusicVolume) && SfxVolume.Equals(other.SfxVolume) &&
            ScreenShake == other.ScreenShake && Fullscreen == other.Fullscreen;

        public override bool Equals(object obj) => obj is GameSettings other && Equals(other);

        public override int GetHashCode() =>
            (MusicVolume, SfxVolume, ScreenShake, Fullscreen).GetHashCode();

        public static bool operator ==(GameSettings left, GameSettings right) => left.Equals(right);
        public static bool operator !=(GameSettings left, GameSettings right) => !left.Equals(right);
    }
}
