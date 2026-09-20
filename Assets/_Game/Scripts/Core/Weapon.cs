using System;
using System.Collections.Generic;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>One projectile to spawn: who fired it, its travel direction and its spawn offset from the muzzle.</summary>
    public readonly struct ShotSpec
    {
        public readonly int PlayerIndex;
        public readonly Vector2 Direction;
        public readonly Vector2 Offset;
        public readonly bool Piercing;

        public ShotSpec(int playerIndex, Vector2 direction, Vector2 offset, bool piercing)
        {
            PlayerIndex = playerIndex;
            Direction = direction;
            Offset = offset;
            Piercing = piercing;
        }
    }

    /// <summary>The player's weapon: upgrade level, fire rate and shot patterns (GDD "Mechanics", Weapon upgrades).</summary>
    public sealed class Weapon
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        /// <summary>Distance between the two parallel shots at level 2, in world units.</summary>
        public const float ParallelShotSpacing = 0.3f;

        /// <summary>Caps catch-up volleys after a long tick so a hitch cannot unleash a burst.</summary>
        internal const int MaxVolleysPerTick = 2;

        /// <summary>One shot within a volley, relative to the aim direction. Rotation is precomputed.</summary>
        readonly struct ShotPattern
        {
            public readonly float Cos;
            public readonly float Sin;
            public readonly float LateralOffset;
            public readonly bool Piercing;

            public ShotPattern(float angleDegrees, float lateralOffset = 0f, bool piercing = false)
            {
                var radians = angleDegrees * (MathF.PI / 180f);
                Cos = angleDegrees == 0f ? 1f : MathF.Cos(radians);
                Sin = angleDegrees == 0f ? 0f : MathF.Sin(radians);
                LateralOffset = lateralOffset;
                Piercing = piercing;
            }
        }

        sealed class LevelSpec
        {
            public readonly float FireRate;
            public readonly ShotPattern[] Shots;

            public LevelSpec(float fireRate, params ShotPattern[] shots)
            {
                FireRate = fireRate;
                Shots = shots;
            }
        }

        const float HalfSpacing = ParallelShotSpacing * 0.5f;

        static readonly LevelSpec[] Levels =
        {
            new LevelSpec(8f, new ShotPattern(0f)),
            new LevelSpec(8f, new ShotPattern(0f, HalfSpacing), new ShotPattern(0f, -HalfSpacing)),
            new LevelSpec(8f, new ShotPattern(10f), new ShotPattern(0f), new ShotPattern(-10f)),
            new LevelSpec(11f, new ShotPattern(10f), new ShotPattern(0f), new ShotPattern(-10f)),
            new LevelSpec(11f, new ShotPattern(20f), new ShotPattern(10f), new ShotPattern(0f, piercing: true),
                new ShotPattern(-10f), new ShotPattern(-20f)),
        };

        float _cooldown;

        public int Level { get; private set; } = MinLevel;
        public bool IsMaxLevel => Level >= MaxLevel;
        public float FireRate => Spec.FireRate;
        public float FireInterval => 1f / Spec.FireRate;
        public int ShotsPerVolley => Spec.Shots.Length;

        LevelSpec Spec => Levels[Level - 1];

        /// <summary>Raises the level by one. Returns false (no change) when already at max level.</summary>
        internal bool Upgrade()
        {
            if (IsMaxLevel) return false;
            Level++;
            return true;
        }

        /// <summary>Drops the level by one, never below <see cref="MinLevel"/>.</summary>
        /// <summary>Restores the level a player carried out of the previous campaign level.</summary>
        internal void Restore(int level)
        {
            Level = Math.Clamp(level, MinLevel, MaxLevel);
        }

        internal void Downgrade()
        {
            if (Level > MinLevel) Level--;
        }

        /// <summary>
        /// Advances the fire timer and appends any shots fired this tick to <paramref name="shots"/>.
        /// Returns the number of volleys fired.
        /// </summary>
        internal int Tick(float deltaTime, bool firing, Vector2 aimDirection, int playerIndex, List<ShotSpec> shots)
        {
            if (shots == null) throw new ArgumentNullException(nameof(shots));

            _cooldown -= deltaTime;

            if (!firing || aimDirection.LengthSquared() < 1e-8f)
            {
                if (_cooldown < 0f) _cooldown = 0f;
                return 0;
            }

            var direction = Vector2.Normalize(aimDirection);
            var volleys = 0;
            while (_cooldown <= 0f && volleys < MaxVolleysPerTick)
            {
                EmitVolley(direction, playerIndex, shots);
                _cooldown += FireInterval;
                volleys++;
            }

            if (_cooldown < 0f) _cooldown = 0f;
            return volleys;
        }

        void EmitVolley(Vector2 direction, int playerIndex, List<ShotSpec> shots)
        {
            var perpendicular = new Vector2(-direction.Y, direction.X);
            foreach (var shot in Spec.Shots)
            {
                var shotDirection = new Vector2(
                    direction.X * shot.Cos - direction.Y * shot.Sin,
                    direction.X * shot.Sin + direction.Y * shot.Cos);
                shots.Add(new ShotSpec(playerIndex, shotDirection, perpendicular * shot.LateralOffset, shot.Piercing));
            }
        }
    }
}
