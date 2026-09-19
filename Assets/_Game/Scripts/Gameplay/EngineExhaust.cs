using System;
using UnityEngine;
using YASS.Core;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>
    /// Engine exhaust particles for a ship (GDD "Art Direction", Engines). The particle system simulates in local
    /// space, so the flame stays attached to the ship. Throttle sets emission rate, exhaust speed and particle size.
    /// Enemies hold <see cref="idleThrottle"/> as their steady level; the player's throttle follows how fast the
    /// ship is actually moving, through <see cref="Drive"/>.
    /// The particle system needs constant emission and velocity-over-lifetime values; this component owns them.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class EngineExhaust : MonoBehaviour
    {
        /// <summary>A tuning value at zero throttle and at full throttle.</summary>
        [Serializable]
        public struct ThrottleRange
        {
            public float atZero;
            public float atFull;

            public ThrottleRange(float atZero, float atFull)
            {
                this.atZero = atZero;
                this.atFull = atFull;
            }

            public float At(float throttle) => Mathf.Lerp(atZero, atFull, throttle);
        }

        /// <summary>Changes smaller than this are not written to the particle system.</summary>
        const float ApplyThreshold = 0.001f;

        [SerializeField] ParticleSystem particles;
        [SerializeField, Tooltip("The ship faces left (enemies), so the exhaust flows towards +X.")]
        bool shipFacesLeft;
        [SerializeField, Range(0f, 1f), Tooltip("Throttle at rest. Enemies hold this level permanently.")]
        float idleThrottle = 0.35f;
        [SerializeField, Min(0f), Tooltip("How quickly the throttle follows the ship's movement, per second.")]
        float responsiveness = 8f;
        [SerializeField, Tooltip("Particles per second.")]
        ThrottleRange emissionRate = new ThrottleRange(20f, 90f);
        [SerializeField, Tooltip("Exhaust speed in units per second.")]
        ThrottleRange exhaustSpeed = new ThrottleRange(1.5f, 4.5f);
        [SerializeField, Tooltip("Scale applied to the particle system's authored start size range.")]
        ThrottleRange sizeScale = new ThrottleRange(0.6f, 1.3f);

        bool _initialised;
        float _baseSizeMin;
        float _baseSizeMax;
        float _appliedThrottle = -1f;

        public float Throttle { get; private set; }
        public float IdleThrottle => idleThrottle;

        void Reset() => particles = GetComponent<ParticleSystem>();

        void Awake() => Initialise();

        /// <summary>Caches the authored start size range. Lazy so edit-mode code (no Awake) can drive it too.</summary>
        void Initialise()
        {
            if (_initialised) return;
            _initialised = true;
            if (particles == null) particles = GetComponent<ParticleSystem>();

            var startSize = particles.main.startSize;
            if (startSize.mode == ParticleSystemCurveMode.TwoConstants)
            {
                _baseSizeMin = startSize.constantMin;
                _baseSizeMax = startSize.constantMax;
            }
            else
            {
                _baseSizeMin = _baseSizeMax = startSize.constant;
            }
        }

        void OnEnable()
        {
            // Pooled ships are re-enabled at a new position: start from idle with no leftover particles.
            Throttle = idleThrottle;
            Apply(true);
            particles.Clear();
            particles.Play();
        }

        /// <summary>
        /// Eases the throttle towards the level for <paramref name="movement"/>: the ship's displacement this step
        /// as a fraction of its top speed (length 0 at rest, 1 at full speed).
        /// </summary>
        public void Drive(NVector2 movement, float deltaTime)
        {
            var target = EngineThrust.Target(movement, idleThrottle);
            Throttle = EngineThrust.Smooth(Throttle, target, responsiveness, deltaTime);
            Apply(false);
        }

        /// <summary>Test seam: sets the throttle directly and applies it.</summary>
        internal void SetThrottle(float throttle)
        {
            Throttle = Mathf.Clamp01(throttle);
            Apply(true);
        }

        void Apply(bool force)
        {
            Initialise();
            if (!force && Mathf.Abs(Throttle - _appliedThrottle) < ApplyThreshold) return;
            _appliedThrottle = Throttle;

            var emission = particles.emission;
            emission.rateOverTimeMultiplier = emissionRate.At(Throttle);

            var velocity = particles.velocityOverLifetime;
            velocity.xMultiplier = (shipFacesLeft ? 1f : -1f) * exhaustSpeed.At(Throttle);

            // startSizeMultiplier would only overwrite the upper bound of a two-constant range, so set both ends.
            var scale = sizeScale.At(Throttle);
            var main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(_baseSizeMin * scale, _baseSizeMax * scale);
        }
    }
}
