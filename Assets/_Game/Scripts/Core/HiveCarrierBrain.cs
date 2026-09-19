using System;
using System.Collections.Generic;
using System.Numerics;

namespace YASS.Core
{
    public enum BossActionType
    {
        LaunchDarts = 0,
        FireSpread = 1
    }

    public readonly struct BossAction
    {
        public readonly BossActionType Type;
        public readonly int Count;

        public BossAction(BossActionType type, int count)
        {
            Type = type;
            Count = count;
        }
    }

    /// <summary>Hive Carrier tuning (GDD "Hive Carrier").</summary>
    public sealed class HiveCarrierSpec
    {
        public static readonly HiveCarrierSpec Default = new HiveCarrierSpec();

        public float MaxHealth { get; }
        public float CycleSeconds { get; }
        public float OpenSeconds { get; }
        public int DartsPerLaunch { get; }
        public float SpreadIntervalSeconds { get; }
        public int SpreadBullets { get; }
        public int EnragedSpreadBullets { get; }
        public float EnragedAtHealthFraction { get; }

        public float ClosedSeconds => CycleSeconds - OpenSeconds;

        public HiveCarrierSpec(float maxHealth = 200f, float cycleSeconds = 8f, float openSeconds = 4f,
            int dartsPerLaunch = 3, float spreadIntervalSeconds = 1.5f, int spreadBullets = 5,
            int enragedSpreadBullets = 7, float enragedAtHealthFraction = 0.5f)
        {
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (openSeconds <= 0f || openSeconds >= cycleSeconds) throw new ArgumentOutOfRangeException(nameof(openSeconds));
            if (dartsPerLaunch < 0) throw new ArgumentOutOfRangeException(nameof(dartsPerLaunch));
            if (spreadIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(spreadIntervalSeconds));
            if (spreadBullets < 1 || enragedSpreadBullets < 1) throw new ArgumentOutOfRangeException(nameof(spreadBullets));

            MaxHealth = maxHealth;
            CycleSeconds = cycleSeconds;
            OpenSeconds = openSeconds;
            DartsPerLaunch = dartsPerLaunch;
            SpreadIntervalSeconds = spreadIntervalSeconds;
            SpreadBullets = spreadBullets;
            EnragedSpreadBullets = enragedSpreadBullets;
            EnragedAtHealthFraction = enragedAtHealthFraction;
        }
    }

    /// <summary>
    /// Hive Carrier behaviour (GDD "Hive Carrier"): a core that opens for part of each cycle and is the only thing
    /// that can be damaged; Dart launches while closed, aimed spreads while open, and an enraged phase at half health.
    /// Starts closed with a launch.
    /// </summary>
    public sealed class HiveCarrierBrain
    {
        readonly HiveCarrierSpec _spec;
        float _phaseTime;
        float _spreadCooldown;
        bool _launchedThisPhase;
        bool _secondLaunchThisPhase;

        public Health Health { get; }
        public bool IsCoreOpen { get; private set; }
        public bool IsDefeated => Health.IsDead;
        public bool IsEnraged => Health.Current <= Health.Max * _spec.EnragedAtHealthFraction;
        public HiveCarrierSpec Spec => _spec;

        public HiveCarrierBrain(HiveCarrierSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Health = new Health(spec.MaxHealth);
        }

        public void Tick(float deltaTime, List<BossAction> actions)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (IsDefeated) return;

            _phaseTime += deltaTime;
            if (!IsCoreOpen) TickClosed(actions);
            else TickOpen(deltaTime, actions);
        }

        /// <summary>Damage to the core. Ignored while closed. Returns true only for the killing hit.</summary>
        public bool TakeCoreHit(float damage) => IsCoreOpen && Health.TakeDamage(damage);

        void TickClosed(List<BossAction> actions)
        {
            if (!_launchedThisPhase)
            {
                _launchedThisPhase = true;
                Launch(actions);
            }

            if (IsEnraged && !_secondLaunchThisPhase && _phaseTime >= _spec.ClosedSeconds * 0.5f)
            {
                _secondLaunchThisPhase = true;
                Launch(actions);
            }

            if (_phaseTime < _spec.ClosedSeconds) return;

            IsCoreOpen = true;
            _phaseTime -= _spec.ClosedSeconds;
            _spreadCooldown = 0f;
            TickOpen(0f, actions);
        }

        void TickOpen(float deltaTime, List<BossAction> actions)
        {
            _spreadCooldown -= deltaTime;
            if (_spreadCooldown <= 0f && _phaseTime < _spec.OpenSeconds)
            {
                actions.Add(new BossAction(BossActionType.FireSpread,
                    IsEnraged ? _spec.EnragedSpreadBullets : _spec.SpreadBullets));
                _spreadCooldown += _spec.SpreadIntervalSeconds;
                if (_spreadCooldown <= 0f) _spreadCooldown = _spec.SpreadIntervalSeconds;
            }

            if (_phaseTime < _spec.OpenSeconds) return;

            IsCoreOpen = false;
            _phaseTime -= _spec.OpenSeconds;
            _launchedThisPhase = false;
            _secondLaunchThisPhase = false;
        }

        void Launch(List<BossAction> actions)
        {
            if (_spec.DartsPerLaunch > 0) actions.Add(new BossAction(BossActionType.LaunchDarts, _spec.DartsPerLaunch));
        }
    }

    /// <summary>Boss movement and fire patterns.</summary>
    public static class BossPatterns
    {
        /// <summary>
        /// Flies left from <paramref name="start"/> at <paramref name="entrySpeed"/> until reaching
        /// <paramref name="holdX"/>, then drifts up and down around the start height.
        /// </summary>
        public static Vector2 Position(Vector2 start, float holdX, float entrySpeed, float driftAmplitude,
            float driftPeriod, float time)
        {
            if (entrySpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(entrySpeed));
            if (driftPeriod <= 0f) throw new ArgumentOutOfRangeException(nameof(driftPeriod));

            var arrival = Math.Max(0f, (start.X - holdX) / entrySpeed);
            if (time < arrival) return new Vector2(start.X - entrySpeed * time, start.Y);

            var drift = MathF.Sin(2f * MathF.PI * (time - arrival) / driftPeriod);
            return new Vector2(Math.Min(start.X, holdX), start.Y + driftAmplitude * drift);
        }

        /// <summary>
        /// Appends <paramref name="count"/> unit directions fanned evenly across <paramref name="totalAngleDegrees"/>,
        /// centred on <paramref name="aim"/>.
        /// </summary>
        public static void Spread(Vector2 aim, int count, float totalAngleDegrees, List<Vector2> directions)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (directions == null) throw new ArgumentNullException(nameof(directions));

            var centre = aim.LengthSquared() > 1e-8f ? Vector2.Normalize(aim) : -Vector2.UnitX;
            var step = count > 1 ? totalAngleDegrees / (count - 1) : 0f;
            var first = count > 1 ? -totalAngleDegrees * 0.5f : 0f;
            for (var i = 0; i < count; i++)
            {
                var radians = (first + step * i) * (MathF.PI / 180f);
                var cos = MathF.Cos(radians);
                var sin = MathF.Sin(radians);
                directions.Add(new Vector2(centre.X * cos - centre.Y * sin, centre.X * sin + centre.Y * cos));
            }
        }
    }
}
