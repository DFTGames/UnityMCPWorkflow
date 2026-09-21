using System;
using System.Collections.Generic;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>What a boss can do. Which of these a boss uses, and when, is data (GDD "Levels").</summary>
    public enum BossActionType
    {
        /// <summary>Launches small enemies from its bays (Hive Carrier, The Overmind).</summary>
        LaunchDarts = 0,

        /// <summary>An aimed fan of bullets.</summary>
        FireSpread = 1,

        /// <summary>Throws a meteor at the player (Rock Crusher).</summary>
        HurlMeteor = 2,

        /// <summary>Starts a beam that sweeps across an arc (Sunforge, Tempest).</summary>
        BeamSweep = 3,

        /// <summary>Dashes across the screen and back (Frost Lancer).</summary>
        Dash = 4,

        /// <summary>Drags the player towards the boss for a moment (Singularity Engine).</summary>
        GravityPull = 5,

        /// <summary>Aimed shots from each of its turrets at once (Dreadnought).</summary>
        TurretVolley = 6
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

    /// <summary>Which half of the boss's cycle an attack belongs to.</summary>
    public enum BossPhase
    {
        /// <summary>Core closed: the boss cannot be hurt.</summary>
        Armoured = 0,

        /// <summary>Core open: the one window in which the boss can be damaged.</summary>
        Vulnerable = 1,

        /// <summary>Either half.</summary>
        Any = 2
    }

    /// <summary>
    /// One thing a boss does, and the conditions under which it does it. An attack is scoped to a phase and to a
    /// band of the boss's remaining health, which is how a boss changes as it is worn down: give the same attack
    /// twice with different health bands and different counts, and it escalates without any special case.
    /// </summary>
    public sealed class BossAttack
    {
        public BossActionType Type { get; }
        public int Count { get; }
        public BossPhase When { get; }

        /// <summary>Seconds into each matching phase before it first happens.</summary>
        public float FirstDelaySeconds { get; }

        /// <summary>How often it repeats within the phase; zero means once per phase.</summary>
        public float IntervalSeconds { get; }

        /// <summary>Active while the boss's health fraction is above this (exclusive) and at or below <see cref="ActiveAtOrBelow"/>.</summary>
        public float ActiveAbove { get; }
        public float ActiveAtOrBelow { get; }

        public BossAttack(BossActionType type, int count, BossPhase when, float firstDelaySeconds = 0f,
            float intervalSeconds = 0f, float activeAbove = 0f, float activeAtOrBelow = 1f)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (firstDelaySeconds < 0f) throw new ArgumentOutOfRangeException(nameof(firstDelaySeconds));
            if (intervalSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            if (activeAbove < 0f || activeAbove >= activeAtOrBelow || activeAtOrBelow > 1f)
                throw new ArgumentOutOfRangeException(nameof(activeAbove));

            Type = type;
            Count = count;
            When = when;
            FirstDelaySeconds = firstDelaySeconds;
            IntervalSeconds = intervalSeconds;
            ActiveAbove = activeAbove;
            ActiveAtOrBelow = activeAtOrBelow;
        }

        /// <summary>Whether this attack belongs to the phase the boss is in now.</summary>
        public bool Matches(bool coreOpen) =>
            When == BossPhase.Any || (When == BossPhase.Vulnerable) == coreOpen;

        /// <summary>Whether the boss is worn down into this attack's band.</summary>
        public bool IsActiveAt(float healthFraction) =>
            healthFraction > ActiveAbove && healthFraction <= ActiveAtOrBelow;
    }

    /// <summary>
    /// One boss, as data: how tough it is, how long its core stays shut, and what it does (GDD "Levels").
    /// Every boss in the campaign is an instance of this; none of them needs its own class.
    /// </summary>
    public sealed class BossSpec
    {
        public string Name { get; }
        public float MaxHealth { get; }
        public float CycleSeconds { get; }
        public float OpenSeconds { get; }
        public IReadOnlyList<BossAttack> Attacks { get; }

        /// <summary>
        /// What a shot into the hull is worth, as a fraction of its damage. Shooting a boss anywhere has to be
        /// worth doing, or the fight is a long wait for the core to open; hitting the core is what a good player
        /// is rewarded for, not what an average one is required to do.
        /// </summary>
        public float HullDamageMultiplier { get; }

        /// <summary>What a shot into the open core is worth, as a fraction of its damage.</summary>
        public float CoreDamageMultiplier { get; }

        public float ClosedSeconds => CycleSeconds - OpenSeconds;

        public BossSpec(string name, float maxHealth, float cycleSeconds, float openSeconds,
            IReadOnlyList<BossAttack> attacks, float hullDamageMultiplier = 0.5f, float coreDamageMultiplier = 1f)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A boss needs a name.", nameof(name));
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (openSeconds <= 0f || openSeconds >= cycleSeconds) throw new ArgumentOutOfRangeException(nameof(openSeconds));
            if (attacks == null) throw new ArgumentNullException(nameof(attacks));
            if (attacks.Count == 0) throw new ArgumentException("A boss with no attacks is scenery.", nameof(attacks));
            if (hullDamageMultiplier < 0f) throw new ArgumentOutOfRangeException(nameof(hullDamageMultiplier));
            if (coreDamageMultiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(coreDamageMultiplier));
            if (hullDamageMultiplier > coreDamageMultiplier)
                throw new ArgumentOutOfRangeException(nameof(hullDamageMultiplier),
                    "The core has to be the better target, or there is no reason to wait for it to open.");

            foreach (var attack in attacks)
                if (attack == null) throw new ArgumentNullException(nameof(attacks));

            Name = name;
            MaxHealth = maxHealth;
            CycleSeconds = cycleSeconds;
            OpenSeconds = openSeconds;
            Attacks = attacks;
            HullDamageMultiplier = hullDamageMultiplier;
            CoreDamageMultiplier = coreDamageMultiplier;
        }
    }

    /// <summary>
    /// Boss behaviour: a core that opens for part of each cycle and is the only thing that can be damaged, and a
    /// set of attacks that fire on their own schedules within each half of that cycle. Starts armoured, so the
    /// player sees what the boss does before getting a chance to hurt it.
    /// </summary>
    public sealed class BossBrain
    {
        /// <summary>Health fraction at or below which a boss is considered to be in its second half.</summary>
        public const float EnragedAtHealthFraction = 0.5f;

        readonly BossSpec _spec;

        /// <summary>When each attack next fires, measured from the start of the current phase.</summary>
        readonly float[] _nextTime;

        float _phaseTime;

        public Health Health { get; }
        public BossSpec Spec => _spec;
        public bool IsCoreOpen { get; private set; }
        public bool IsDefeated => Health.IsDead;
        public bool IsEnraged => HealthFraction <= EnragedAtHealthFraction;
        public float HealthFraction => Health.Current / Health.Max;

        public BossBrain(BossSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Health = new Health(spec.MaxHealth);
            _nextTime = new float[spec.Attacks.Count];
            BeginPhase();
        }

        public void Tick(float deltaTime, List<BossAction> actions)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (IsDefeated) return;

            _phaseTime += deltaTime;

            var phaseLength = IsCoreOpen ? _spec.OpenSeconds : _spec.ClosedSeconds;
            if (_phaseTime >= phaseLength)
            {
                IsCoreOpen = !IsCoreOpen;
                _phaseTime -= phaseLength;
                BeginPhase();
            }

            var fraction = HealthFraction;
            for (var i = 0; i < _spec.Attacks.Count; i++)
            {
                var attack = _spec.Attacks[i];
                if (!attack.Matches(IsCoreOpen) || !attack.IsActiveAt(fraction)) continue;
                if (_phaseTime < _nextTime[i]) continue;

                actions.Add(new BossAction(attack.Type, attack.Count));

                // A repeating attack keeps its rhythm; a one-shot is done until the phase comes round again.
                if (attack.IntervalSeconds <= 0f)
                {
                    _nextTime[i] = float.PositiveInfinity;
                    continue;
                }

                // An attack that has been waiting (a long step, or a health band that has only just opened)
                // starts its interval from now. Adding to a stale time would put the next shot in the past and
                // fire it again on the following step.
                var onSchedule = _nextTime[i] + attack.IntervalSeconds;
                _nextTime[i] = onSchedule > _phaseTime ? onSchedule : _phaseTime + attack.IntervalSeconds;
            }
        }

        /// <summary>
        /// Damage to the core, which is worth full value while it is open. A shot into a closed core hits the
        /// shutters over it, so it counts as a hull hit rather than being thrown away. Returns true only for the
        /// killing hit.
        /// </summary>
        public bool TakeCoreHit(float damage) =>
            IsCoreOpen ? TakeDamage(damage * _spec.CoreDamageMultiplier) : TakeHullHit(damage);

        /// <summary>
        /// Damage to the armoured hull, which is worth a fraction of its value (GDD "Levels"). Returns true only
        /// for the killing hit.
        /// </summary>
        public bool TakeHullHit(float damage) => TakeDamage(damage * _spec.HullDamageMultiplier);

        bool TakeDamage(float damage)
        {
            if (IsDefeated) return false;
            if (damage <= 0f) return false;

            return Health.TakeDamage(damage);
        }

        void BeginPhase()
        {
            for (var i = 0; i < _nextTime.Length; i++) _nextTime[i] = _spec.Attacks[i].FirstDelaySeconds;
        }
    }

    /// <summary>
    /// A boss dash: it winds up in place (the tell), crosses the playfield, then returns to its station
    /// (GDD "Levels": the Frost Lancer's fast dashes). Like the Diver, it commits to the line it chose.
    /// </summary>
    public sealed class BossDash
    {
        public enum DashPhase
        {
            WindUp,
            Dashing,
            Returning,
            Done
        }

        readonly float _windUpSeconds;
        readonly float _dashSpeed;
        readonly float _returnSpeed;
        readonly float _targetX;

        Vector2 _station;
        float _elapsed;

        public BossDash(float windUpSeconds, float dashSpeed, float returnSpeed, float targetX, Vector2 station)
        {
            if (windUpSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(windUpSeconds));
            if (dashSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(dashSpeed));
            if (returnSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(returnSpeed));

            _windUpSeconds = windUpSeconds;
            _dashSpeed = dashSpeed;
            _returnSpeed = returnSpeed;
            _targetX = targetX;
            _station = station;
        }

        public DashPhase Phase { get; private set; } = DashPhase.WindUp;
        public bool IsFinished => Phase == DashPhase.Done;

        /// <summary>How far through the wind-up it is, 0 to 1: what a telegraph follows.</summary>
        public float WindUpProgress => Phase == DashPhase.WindUp
            ? (_windUpSeconds <= 0f ? 1f : Math.Clamp(_elapsed / _windUpSeconds, 0f, 1f))
            : 1f;

        /// <summary>Advances the dash and returns where the boss should be now.</summary>
        public Vector2 Step(float deltaTime, Vector2 current)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            switch (Phase)
            {
                case DashPhase.WindUp:
                    _elapsed += deltaTime;
                    if (_elapsed >= _windUpSeconds) Phase = DashPhase.Dashing;
                    return current;

                case DashPhase.Dashing:
                    var dashed = new Vector2(current.X - _dashSpeed * deltaTime, current.Y);
                    if (dashed.X > _targetX) return dashed;

                    Phase = DashPhase.Returning;
                    return new Vector2(_targetX, current.Y);

                case DashPhase.Returning:
                    var step = _returnSpeed * deltaTime;
                    var toStation = _station - current;
                    if (toStation.Length() <= step)
                    {
                        Phase = DashPhase.Done;
                        return _station;
                    }

                    return current + Vector2.Normalize(toStation) * step;

                default:
                    return _station;
            }
        }
    }

    /// <summary>
    /// A sweeping beam: it turns steadily from one angle to another while it burns (GDD "Levels": the Sunforge's
    /// flame beams, the Tempest's arcs). Whether it is touching anything is <see cref="SniperShot.HitsPoint"/>;
    /// this only decides where it points and how soon it may hurt the same ship again.
    /// The cooldown is spent on a burn that landed, not on time passing: a beam turning 40 degrees a second
    /// crosses a distant ship in a twentieth of a second, so a damage tick on its own clock would sweep straight
    /// through almost every player it touched.
    /// </summary>
    public sealed class BeamSweep
    {
        readonly float _fromDegrees;
        readonly float _toDegrees;
        readonly float _seconds;
        readonly float _damageIntervalSeconds;

        float _elapsed;
        float _sinceDamage;

        public BeamSweep(float fromDegrees, float toDegrees, float seconds, float damageIntervalSeconds)
        {
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (damageIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(damageIntervalSeconds));

            _fromDegrees = fromDegrees;
            _toDegrees = toDegrees;
            _seconds = seconds;
            _damageIntervalSeconds = damageIntervalSeconds;
            _sinceDamage = damageIntervalSeconds; // the first step of a sweep can already burn
        }

        public bool IsFinished => _elapsed >= _seconds;

        /// <summary>Whether it is allowed to burn whatever it is touching now.</summary>
        public bool CanBurn => _sinceDamage >= _damageIntervalSeconds;

        /// <summary>How far through the sweep it is, 0 to 1.</summary>
        public float Progress => Math.Clamp(_elapsed / _seconds, 0f, 1f);

        /// <summary>The direction the beam points now.</summary>
        public Vector2 Direction
        {
            get
            {
                var radians = (_fromDegrees + (_toDegrees - _fromDegrees) * Progress) * (MathF.PI / 180f);
                return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
            }
        }

        /// <summary>Advances the sweep.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _elapsed += deltaTime;
            _sinceDamage += deltaTime;
        }

        /// <summary>Records a burn that actually landed, which is what starts the cooldown.</summary>
        public void Burned() => _sinceDamage = 0f;
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

        /// <summary>
        /// How hard a gravity well pulls something at <paramref name="distance"/>: full strength close in, fading
        /// to nothing at <paramref name="radius"/>. Linear rather than inverse-square, because a real well would
        /// be unplayable at the centre and unnoticeable at the edge.
        /// </summary>
        public static float PullStrength(float distance, float radius, float strength)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (strength < 0f) throw new ArgumentOutOfRangeException(nameof(strength));
            if (distance >= radius) return 0f;

            return strength * (1f - Math.Max(distance, 0f) / radius);
        }
    }
}
