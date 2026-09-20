using System;
using System.Numerics;

namespace YASS.Core
{
    public enum MotionPattern
    {
        Straight = 0,
        SineWave = 1,

        /// <summary>Flies in and stops to shoot (Gunship, Sniper).</summary>
        HoldPosition = 2,

        /// <summary>Enters, locks onto the player, then charges (Diver).</summary>
        Dive = 3
    }

    /// <summary>Enemy flight paths as functions of time since spawn (GDD "Enemies and Hazards"). Enemies fly left.</summary>
    public static class EnemyMotion
    {
        public static Vector2 Evaluate(MotionPattern pattern, Vector2 start, float speed, float amplitude,
            float frequencyHz, float time)
        {
            switch (pattern)
            {
                case MotionPattern.Straight: return Straight(start, speed, time);
                case MotionPattern.SineWave: return SineWave(start, speed, amplitude, frequencyHz, time);
                default: throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null);
            }
        }

        public static Vector2 Straight(Vector2 start, float speed, float time) =>
            new Vector2(start.X - speed * time, start.Y);

        public static Vector2 SineWave(Vector2 start, float speed, float amplitude, float frequencyHz, float time) =>
            new Vector2(start.X - speed * time, start.Y + amplitude * MathF.Sin(2f * MathF.PI * frequencyHz * time));

        /// <summary>Unit vector from <paramref name="from"/> to <paramref name="to"/>, or the fallback if they coincide.</summary>
        public static Vector2 AimAt(Vector2 from, Vector2 to, Vector2 fallback)
        {
            var delta = to - from;
            return delta.LengthSquared() > 1e-6f ? Vector2.Normalize(delta) : fallback;
        }
    }

    /// <summary>
    /// A gunship's weapon: three shots close together, then a long wait (GDD "Enemies and Hazards"). Built on
    /// the same one-shot-per-tick rule as <see cref="FireTimer"/>, so a long frame cannot empty the burst at once.
    /// </summary>
    public sealed class BurstFire
    {
        readonly int _shotsPerBurst;
        readonly float _shotInterval;
        readonly float _burstInterval;

        float _remaining;
        int _firedInBurst;

        public BurstFire(int shotsPerBurst, float shotInterval, float burstInterval, float initialDelay)
        {
            if (shotsPerBurst < 1) throw new ArgumentOutOfRangeException(nameof(shotsPerBurst));
            if (shotInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(shotInterval));
            if (burstInterval <= shotInterval) throw new ArgumentOutOfRangeException(nameof(burstInterval));
            if (initialDelay < 0f) throw new ArgumentOutOfRangeException(nameof(initialDelay));

            _shotsPerBurst = shotsPerBurst;
            _shotInterval = shotInterval;
            _burstInterval = burstInterval;
            _remaining = initialDelay;
        }

        /// <summary>How many shots of the current burst have gone out.</summary>
        public int FiredInBurst => _firedInBurst;

        public bool Tick(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _remaining -= deltaTime;
            if (_remaining > 0f) return false;

            _firedInBurst++;

            // Within a burst the shots are close together; after the last one it waits.
            var wait = _firedInBurst >= _shotsPerBurst ? _burstInterval : _shotInterval;
            if (_firedInBurst >= _shotsPerBurst) _firedInBurst = 0;

            _remaining += wait;
            if (_remaining <= 0f) _remaining = wait;
            return true;
        }
    }

    /// <summary>Repeating timer for enemy weapons. Fires at most once per tick, so a long tick cannot burst.</summary>
    public sealed class FireTimer
    {
        readonly float _interval;
        float _remaining;

        public float Interval => _interval;

        public FireTimer(float interval, float initialDelay)
        {
            if (interval <= 0f) throw new ArgumentOutOfRangeException(nameof(interval));
            if (initialDelay < 0f) throw new ArgumentOutOfRangeException(nameof(initialDelay));

            _interval = interval;
            _remaining = initialDelay;
        }

        public bool Tick(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _remaining -= deltaTime;
            if (_remaining > 0f) return false;

            _remaining += _interval;
            if (_remaining <= 0f) _remaining = _interval;
            return true;
        }
    }
}
