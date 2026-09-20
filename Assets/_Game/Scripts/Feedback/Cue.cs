using UnityEngine;
using YASS.Core;

namespace YASS.Feedback
{
    /// <summary>The particle effects the game plays (GDD "Art Direction", Visual effects).</summary>
    public enum Effect
    {
        None = 0,
        SmallExplosion,
        LargeExplosion,
        BossExplosion,
        MuzzleFlash,
        PickupSparkle,
        ShieldRipple
    }

    /// <summary>
    /// One place for gameplay code to ask for feedback: a sound, an effect, a shake. Every part is optional, so
    /// a scene without the feedback objects (a test fixture, a tools scene) simply stays quiet instead of
    /// throwing. Named <c>Cue</c> rather than <c>Feedback</c>, which would collide with this namespace.
    /// </summary>
    public static class Cue
    {
        public static void Play(Sfx sfx) => AudioDirector.PlayIfPresent(sfx);

        public static void Spawn(Effect effect, Vector2 position) =>
            EffectSpawner.SpawnIfPresent(effect, position);

        /// <summary>Spawns an effect and plays the sound that goes with it.</summary>
        public static void Spawn(Effect effect, Vector2 position, Sfx sfx)
        {
            EffectSpawner.SpawnIfPresent(effect, position);
            AudioDirector.PlayIfPresent(sfx);
        }

        public static void Shake(float trauma) => CameraShaker.ShakeIfPresent(trauma);
    }
}
