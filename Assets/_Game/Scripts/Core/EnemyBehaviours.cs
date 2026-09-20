using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// A gunship's approach: it flies in from the right and stops in the right third of the screen to shoot
    /// (GDD "Enemies and Hazards"). Written as a position rather than a velocity so it matches the other
    /// patterns and stays deterministic.
    /// </summary>
    public static class HoldingPattern
    {
        /// <summary>Where the enemy is at <paramref name="time"/>, stopping once it reaches its station.</summary>
        public static Vector2 Evaluate(Vector2 start, float speed, float stationX, float time)
        {
            if (time < 0f) throw new ArgumentOutOfRangeException(nameof(time));

            var x = start.X - speed * time;
            return new Vector2(MathF.Max(x, stationX), start.Y);
        }

        /// <summary>True once it has arrived and may open fire.</summary>
        public static bool HasArrived(Vector2 start, float speed, float stationX, float time) =>
            speed <= 0f || start.X - speed * time <= stationX;

        /// <summary>The station for a playfield: a fraction in from the right edge (0.33 is the right third).</summary>
        public static float StationFor(Playfield field, float fractionFromRight)
        {
            var clamped = Math.Clamp(fractionFromRight, 0f, 1f);
            return field.MaxX - (field.MaxX - field.MinX) * clamped;
        }
    }

    /// <summary>What a diver is doing right now.</summary>
    public enum DivePhase
    {
        /// <summary>Flying in from the right at its cruising speed.</summary>
        Entering,
        /// <summary>Stopped, locked on, telegraphing the charge.</summary>
        Locking,
        /// <summary>Charging along the direction it locked in.</summary>
        Charging
    }

    /// <summary>
    /// A diver enters, stops to lock onto where the player is, then charges along that line
    /// (GDD "Enemies and Hazards"). The lock is a fixed direction: it commits, so the player can dodge it.
    /// </summary>
    public sealed class DiveAttack
    {
        readonly float _entrySeconds;
        readonly float _lockSeconds;
        readonly float _chargeSpeed;

        float _elapsed;

        public DiveAttack(float entrySeconds, float lockSeconds, float chargeSpeed)
        {
            if (entrySeconds < 0f) throw new ArgumentOutOfRangeException(nameof(entrySeconds));
            if (lockSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(lockSeconds));
            if (chargeSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(chargeSpeed));

            _entrySeconds = entrySeconds;
            _lockSeconds = lockSeconds;
            _chargeSpeed = chargeSpeed;
        }

        public DivePhase Phase { get; private set; } = DivePhase.Entering;

        /// <summary>The direction committed to when the lock ended; zero until then.</summary>
        public Vector2 ChargeDirection { get; private set; }

        /// <summary>How far through the lock it is, 0 to 1: what a telegraph effect follows.</summary>
        public float LockProgress =>
            Phase == DivePhase.Locking ? Math.Clamp((_elapsed - _entrySeconds) / _lockSeconds, 0f, 1f) : 0f;

        /// <summary>
        /// Advances the attack. <paramref name="self"/> and <paramref name="target"/> are only read while
        /// locking, which is when the direction is chosen.
        /// </summary>
        public void Tick(float deltaTime, Vector2 self, Vector2 target)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _elapsed += deltaTime;

            switch (Phase)
            {
                case DivePhase.Entering when _elapsed >= _entrySeconds:
                    Phase = DivePhase.Locking;
                    break;
                case DivePhase.Locking when _elapsed >= _entrySeconds + _lockSeconds:
                    ChargeDirection = EnemyMotion.AimAt(self, target, -Vector2.UnitX);
                    Phase = DivePhase.Charging;
                    break;
            }
        }

        /// <summary>How far it moves this step: its cruise while entering, nothing while locking, its charge after.</summary>
        public Vector2 Step(float deltaTime, float cruiseSpeed)
        {
            switch (Phase)
            {
                case DivePhase.Entering: return new Vector2(-cruiseSpeed * deltaTime, 0f);
                case DivePhase.Charging: return ChargeDirection * (_chargeSpeed * deltaTime);
                default: return Vector2.Zero; // holding still is the tell
            }
        }
    }

    /// <summary>
    /// A frigate's front shield: shots that land on its nose are turned away, so the player has to get past it
    /// (GDD "Enemies and Hazards"; this is what the twin-stick aiming is for).
    /// </summary>
    public static class FrontShield
    {
        /// <summary>How wide the shielded arc is, either side of straight ahead.</summary>
        public const float ArcDegrees = 70f;

        /// <summary>
        /// True when a shot that landed at <paramref name="hitOffset"/> (measured from the enemy's centre) is
        /// stopped by a shield facing <paramref name="facing"/>: it is blocked by where it struck, not by the
        /// heading it arrived on.
        /// </summary>
        public static bool Blocks(Vector2 facing, Vector2 hitOffset, float arcDegrees = ArcDegrees)
        {
            if (arcDegrees < 0f || arcDegrees > 180f) throw new ArgumentOutOfRangeException(nameof(arcDegrees));
            if (facing.LengthSquared() < 1e-6f || hitOffset.LengthSquared() < 1e-6f) return false;

            var toHit = Vector2.Normalize(hitOffset);
            var nose = Vector2.Normalize(facing);
            var cosine = Vector2.Dot(nose, toHit);

            return cosine >= MathF.Cos(arcDegrees * MathF.PI / 180f);
        }
    }

    /// <summary>What a sniper is doing: the warning, then the shot (GDD "Enemies and Hazards").</summary>
    public sealed class SniperShot
    {
        readonly float _warningSeconds;
        readonly float _beamSeconds;
        readonly float _recoverySeconds;

        float _elapsed;
        bool _aimTaken;

        public SniperShot(float warningSeconds, float beamSeconds, float recoverySeconds)
        {
            if (warningSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(warningSeconds));
            if (beamSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(beamSeconds));
            if (recoverySeconds < 0f) throw new ArgumentOutOfRangeException(nameof(recoverySeconds));

            _warningSeconds = warningSeconds;
            _beamSeconds = beamSeconds;
            _recoverySeconds = recoverySeconds;
        }

        /// <summary>The line the shot will take, chosen when the warning starts and never adjusted after.</summary>
        public Vector2 Aim { get; private set; } = -Vector2.UnitX;

        /// <summary>True while the warning line is showing: the player's chance to move.</summary>
        public bool IsWarning { get; private set; } = true;

        /// <summary>True while the beam is live and can hurt.</summary>
        public bool IsFiring { get; private set; }

        /// <summary>How far through the warning it is, 0 to 1, for a line that grows or brightens.</summary>
        public float WarningProgress => IsWarning ? Math.Clamp(_elapsed / _warningSeconds, 0f, 1f) : 1f;

        /// <summary>
        /// Advances the cycle: warn, fire, recover, warn again. The aim is taken once, at the moment the warning
        /// begins, and never adjusted after: the warning line is a promise, and moving out of it is the answer
        /// to this enemy (GDD "Enemies and Hazards").
        /// </summary>
        public void Tick(float deltaTime, Vector2 self, Vector2 target)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            if (IsWarning && !_aimTaken)
            {
                Aim = EnemyMotion.AimAt(self, target, -Vector2.UnitX);
                _aimTaken = true;
            }

            _elapsed += deltaTime;

            if (IsWarning)
            {
                if (_elapsed < _warningSeconds) return;

                IsWarning = false;
                IsFiring = true;
                _elapsed = 0f;
                return;
            }

            if (IsFiring)
            {
                if (_elapsed < _beamSeconds) return;

                IsFiring = false;
                _elapsed = 0f;
                return;
            }

            if (_elapsed < _recoverySeconds) return;

            IsWarning = true;
            _aimTaken = false; // the next warning takes a fresh line
            _elapsed = 0f;
        }

        /// <summary>
        /// Whether a point lies on the beam: within <paramref name="halfWidth"/> of the line from the sniper,
        /// and in front of it rather than behind.
        /// </summary>
        public static bool HitsPoint(Vector2 origin, Vector2 aim, Vector2 point, float halfWidth, float length)
        {
            if (halfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (length <= 0f) throw new ArgumentOutOfRangeException(nameof(length));
            if (aim.LengthSquared() < 1e-6f) return false;

            var direction = Vector2.Normalize(aim);
            var offset = point - origin;
            var along = Vector2.Dot(offset, direction);
            if (along < 0f || along > length) return false;

            var across = offset - direction * along;
            return across.Length() <= halfWidth;
        }
    }

    /// <summary>Proximity mine tuning (GDD "Enemies and Hazards", the new enemies' rules).</summary>
    public static class MineSpec
    {
        /// <summary>The Mine Layer must not be able to kill whatever is chasing it the instant it drops one.</summary>
        public const float ArmSeconds = 0.6f;

        /// <summary>How close a player has to come, and how far the blast reaches.</summary>
        public const float TriggerRadius = 1.5f;

        public const float Damage = 25f;

        /// <summary>A mine nobody goes near clears itself, so the playfield cannot silt up.</summary>
        public const float LifetimeSeconds = 12f;

        public static ProximityMine Create() => new ProximityMine(ArmSeconds, TriggerRadius, LifetimeSeconds);
    }

    /// <summary>
    /// A proximity mine, dropped by a Mine Layer: it arms after a moment (so it cannot kill the ship that flew
    /// past as it dropped), then detonates when the player comes close, and expires by itself.
    /// </summary>
    public sealed class ProximityMine
    {
        readonly float _armSeconds;
        readonly float _triggerRadius;
        readonly float _lifetimeSeconds;

        float _elapsed;

        public ProximityMine(float armSeconds, float triggerRadius, float lifetimeSeconds)
        {
            if (armSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(armSeconds));
            if (triggerRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(triggerRadius));
            if (lifetimeSeconds <= armSeconds) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));

            _armSeconds = armSeconds;
            _triggerRadius = triggerRadius;
            _lifetimeSeconds = lifetimeSeconds;
        }

        public bool IsArmed => _elapsed >= _armSeconds;

        /// <summary>True once it has sat unused for its whole life and should be removed.</summary>
        public bool HasExpired => _elapsed >= _lifetimeSeconds;

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _elapsed += deltaTime;
        }

        /// <summary>True when an armed mine should go off for a player at <paramref name="distance"/>.</summary>
        public bool ShouldDetonate(float distance) => IsArmed && !HasExpired && distance <= _triggerRadius;
    }
}
