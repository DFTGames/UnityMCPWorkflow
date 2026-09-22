using System;

namespace YASS.Core
{
    /// <summary>Sound effects the game plays (GDD "Audio Direction", Sound effects).</summary>
    public enum Sfx
    {
        None = 0,
        PlayerShot,
        EnemyShot,
        SmallExplosion,
        LargeExplosion,
        MeteorBreak,
        PlayerHit,
        ShieldHit,
        LifeLost,
        Pickup,
        WeaponUpgrade,
        BossWarning,
        BossExplosion,
        UiMove,
        UiConfirm,
        GameOver,
        SectorClear
    }

    /// <summary>
    /// Rules for how sounds are mixed and how often they may repeat, kept engine-free so they can be tested
    /// (GDD "Audio Direction", Mixing).
    /// </summary>
    public static class AudioRules
    {
        /// <summary>The level a muted mixer group is set to, in decibels.</summary>
        public const float MutedDecibels = -80f;

        /// <summary>
        /// How far a group may read back from the level it was asked for and still count as set. Wide enough
        /// for rounding, narrow enough that a level left over from somewhere else is never mistaken for ours.
        /// </summary>
        public const float DecibelTolerance = 0.01f;

        /// <summary>The mixer groups the Settings screen controls (GDD "Audio Direction", Mixing).</summary>
        public const string MusicGroup = "Music";
        public const string SfxGroup = "SFX";

        /// <summary>The exposed mixer parameters the game writes the volumes to.</summary>
        public const string MusicVolumeParameter = "MusicVolume";
        public const string SfxVolumeParameter = "SfxVolume";

        /// <summary>
        /// Converts a 0..1 volume slider into mixer decibels. Loudness is logarithmic, so a linear slider set to
        /// half must not simply halve the decibels or the top of its travel does almost nothing.
        /// </summary>
        public static float ToDecibels(float volume)
        {
            if (float.IsNaN(volume) || volume <= 0.0001f) return MutedDecibels;

            var clamped = MathF.Min(volume, 1f);
            return MathF.Max(MutedDecibels, 20f * MathF.Log10(clamped));
        }

        /// <summary>
        /// Whether a group is at the level it was asked for, within <see cref="DecibelTolerance"/>. A level
        /// comes back as it was set, so a wider gap than that means the request never landed.
        /// </summary>
        public static bool IsSetTo(float requestedDecibels, float actualDecibels) =>
            MathF.Abs(requestedDecibels - actualDecibels) <= DecibelTolerance;

        /// <summary>
        /// How soon the same sound may play again. Shots fire many times a second and would stack into noise,
        /// so they are spaced out; one-off sounds are not held back at all.
        /// </summary>
        public static float MinimumInterval(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.PlayerShot:
                case Sfx.EnemyShot:
                    return 0.06f;
                case Sfx.SmallExplosion:
                case Sfx.MeteorBreak:
                    return 0.04f;
                case Sfx.PlayerHit:
                case Sfx.ShieldHit:
                    return 0.1f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Whether a sound may play now, given when it last played. Time is the caller's clock; passing an
        /// unscaled clock keeps menu sounds working while the game is paused.
        /// </summary>
        public static bool CanPlay(Sfx sfx, float lastPlayedTime, float now)
        {
            if (sfx == Sfx.None) return false;
            if (lastPlayedTime < 0f) return true; // never played

            return now - lastPlayedTime >= MinimumInterval(sfx);
        }

        /// <summary>
        /// Slight pitch variation for sounds that repeat, so a stream of shots does not sound like one note held
        /// down. Deterministic in <paramref name="index"/> so tests and replays agree.
        /// </summary>
        public static float Pitch(Sfx sfx, int index)
        {
            var spread = MinimumInterval(sfx) > 0f ? 0.06f : 0f;
            if (spread <= 0f) return 1f;

            // A cheap repeating pattern rather than randomness: no allocation, no shared random state.
            var step = (index % 5) / 4f; // 0, 0.25, 0.5, 0.75, 1
            return 1f + (step * 2f - 1f) * spread;
        }
    }
}
