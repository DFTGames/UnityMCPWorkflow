using System;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>
    /// An enemy ship: follows its motion pattern, uses whatever weapon its definition gives it and reports its
    /// death to the runner. Every behaviour here is a thin shell over a rule in <see cref="YASS.Core"/>
    /// (GDD "Enemies and Hazards").
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyView : MonoBehaviour, IDamageable
    {
        [SerializeField] EnemyDefinition definition;
        [SerializeField] Rigidbody2D body;
        [SerializeField] Vector2 muzzleOffset = new Vector2(-0.4f, 0f);
        [SerializeField, Tooltip("Sniper only: the warning line and beam.")] BeamView beam;

        GameRunner _runner;

        HitFlash _flash;
        Action<EnemyView> _release;
        Health _health;
        FireTimer _fireTimer;
        BurstFire _burst;
        FireTimer _mineTimer;
        DiveAttack _dive;
        SniperShot _sniper;
        NVector2 _start;
        NVector2 _position;
        float _stationX;
        float _age;
        float _speed;
        float _bulletSpeed;
        bool _beamWasFiring;

        public bool IsAlive { get; private set; }

        /// <summary>
        /// A shielded hull stops even a piercing shot (the same rule as the boss's armour). The projectile asks
        /// before it knows where it struck, so a piercing shot is stopped by a Frigate's flank too: a ship that
        /// size is armour enough either way.
        /// </summary>
        public bool BlocksPiercing => definition.HasFrontShield;

        /// <summary>The wave this hazard belongs to, or -1 (boss launches, test spawns). Set by the runner after Init.</summary>
        public int WaveIndex { get; internal set; } = -1;
        public EnemyDefinition Definition => definition;
        public int Points => PointValues.Enemy(definition.Size);

        /// <summary>How big a bang this enemy goes out with (GDD "Art Direction", Visual effects).</summary>
        public EnemySize Size => definition.Size;
        public float ContactDamage => definition.ContactDamage;
        public Vector2 Position => body.position;

        /// <summary>Test seam: what the diver is doing (null for every other enemy).</summary>
        internal DivePhase? DiverPhase => _dive?.Phase;

        /// <summary>Test seam: whether the sniper's beam is live (null for every other enemy).</summary>
        internal bool? BeamIsFiring => _sniper?.IsFiring;

        /// <summary>Test seam: whether the sniper is showing its warning line (null for every other enemy).</summary>
        internal bool? BeamIsWarning => _sniper?.IsWarning;

        /// <summary>Test seam: how many shots this enemy's front shield has turned away (as the boss counts armour hits).</summary>
        internal int ShieldBlocks { get; private set; }

        /// <summary>
        /// Where the nose points. Enemies fly leftwards, so that is rotation zero; a charging diver turns, and a
        /// shield must turn with it.
        /// </summary>
        NVector2 Facing
        {
            get
            {
                var radians = (body.rotation + 180f) * Mathf.Deg2Rad;
                return new NVector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        void Reset() => body = GetComponent<Rigidbody2D>();

        void Awake() => _flash = GetComponent<HitFlash>();

        /// <summary>
        /// Activates the enemy at <paramref name="position"/>, resetting all state so pooled instances can be reused.
        /// <paramref name="release"/> returns it to its pool; when null, <see cref="Despawn"/> destroys it instead.
        /// Until Init is called the enemy is inert.
        /// </summary>
        public void Init(GameRunner runner, DifficultySettings settings, Vector2 position, Action<EnemyView> release)
        {
            _runner = runner;
            _release = release;
            WaveIndex = -1;
            _health = new Health(definition.MaxHealth);
            _start = position.ToNumerics();
            _position = _start;
            _age = 0f;
            _speed = definition.Speed * settings.EnemySpeedMultiplier;
            _bulletSpeed = definition.BulletSpeed * settings.EnemySpeedMultiplier;
            _stationX = HoldingPattern.StationFor(runner.Playfield, definition.StationFromRight);

            // Everything an enemy does on a clock is scaled by the difficulty, not just its gun: a harder run
            // means faster charges, mines laid sooner and a shorter warning (GDD "Difficulty and Balancing").
            var fireRate = settings.EnemyFireRateMultiplier;
            var fireInterval = definition.FireInterval / fireRate;
            _burst = definition.Fires && definition.ShotsPerBurst > 1
                // A burst must still fit inside the gap that follows it, whatever the multiplier does.
                ? new BurstFire(definition.ShotsPerBurst, definition.BurstShotInterval,
                    Mathf.Max(fireInterval, definition.BurstShotInterval * (definition.ShotsPerBurst + 1)),
                    definition.FirstShotDelay)
                : null;
            _fireTimer = definition.Fires && _burst == null
                ? new FireTimer(fireInterval, definition.FirstShotDelay)
                : null;
            _mineTimer = definition.LaysMines
                ? new FireTimer(definition.MineInterval / fireRate, definition.MineInterval / fireRate)
                : null;
            _dive = definition.Pattern == MotionPattern.Dive
                ? new DiveAttack(definition.DiveEntrySeconds, definition.DiveLockSeconds,
                    definition.DiveChargeSpeed * settings.EnemySpeedMultiplier)
                : null;
            _sniper = definition.FiresBeam
                // The beam's own duration is fixed: it is the warning and the wait that a harder run shortens.
                ? new SniperShot(definition.BeamWarningSeconds / fireRate, definition.BeamSeconds,
                    definition.BeamRecoverySeconds / fireRate)
                : null;
            _beamWasFiring = false;
            ShieldBlocks = 0;
            if (beam != null) beam.Hide();

            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            body.rotation = 0f;
            IsAlive = true;
        }

        void FixedUpdate()
        {
            if (!IsAlive) return;

            var deltaTime = Time.fixedDeltaTime;
            _age += deltaTime;

            Move(deltaTime);
            var position = _position.ToUnity();

            if (CanAct(position))
            {
                TickWeapon(deltaTime, position);
                TickMines(deltaTime, position);
                TickBeam(deltaTime, position);
            }
            else if (beam != null)
            {
                // Off screen or still flying in: a line left hanging in the air would read as a threat.
                beam.Hide();
            }

            if (IsGone(position)) Despawn();
        }

        /// <summary>
        /// Applies a shot that landed at <paramref name="hitPoint"/>. A front shield turns away everything that
        /// comes at the nose, so a Frigate has to be hit from the side or behind (GDD "Enemies and Hazards").
        /// The hit point is the projectile's centre on the step it overlapped, which is well inside the half of
        /// the hull it came at: a shot would have to travel most of a ship's width in one step to be mistaken
        /// for one from the far side.
        /// </summary>
        public void TakeHit(float damage, int playerIndex, Vector2 hitPoint)
        {
            if (!IsAlive) return;

            if (definition.HasFrontShield && FrontShield.Blocks(Facing, (hitPoint - Position).ToNumerics()))
            {
                ShieldBlocks++;
                Cue.Spawn(Effect.ShieldRipple, hitPoint, Sfx.ShieldHit);
                return;
            }

            if (_health.TakeDamage(damage)) _runner.OnEnemyDestroyed(this, playerIndex);
            else if (_flash != null) _flash.Flash(); // a hit that does not kill must still read
        }

        /// <summary>Removes the enemy (back to its pool, or destroyed). Safe to call more than once.</summary>
        public void Despawn()
        {
            if (!IsAlive) return;

            IsAlive = false;
            if (beam != null) beam.Hide();
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }

        // ---- Internals ---------------------------------------------------------------------------------------

        void Move(float deltaTime)
        {
            switch (definition.Pattern)
            {
                case MotionPattern.HoldPosition:
                    _position = HoldingPattern.Evaluate(_start, _speed, _stationX, _age);
                    break;
                case MotionPattern.Dive:
                    _dive.Tick(deltaTime, _position, _runner.NearestPlayerPosition(_position.ToUnity()).ToNumerics());
                    _position += _dive.Step(deltaTime, _speed);
                    break;
                default:
                    _position = EnemyMotion.Evaluate(definition.Pattern, _start, _speed, definition.WaveAmplitude,
                        definition.WaveFrequency, _age);
                    break;
            }

            body.MovePosition(_position.ToUnity());
            if (_dive != null && _dive.Phase == DivePhase.Charging) FaceChargeDirection();
        }

        /// <summary>A charging diver points where it is going: the one enemy that does not simply fly left.</summary>
        void FaceChargeDirection()
        {
            var direction = _dive.ChargeDirection;
            if (direction.LengthSquared() < 1e-6f) return;

            // Sprites are drawn facing left, so a heading of 180 degrees is rotation zero.
            body.MoveRotation(Mathf.Atan2(direction.Y, direction.X) * Mathf.Rad2Deg + 180f);
        }

        /// <summary>
        /// Weapons only work on screen, and an enemy that flies in to take up a station holds its fire until it
        /// has arrived (GDD: the Gunship stops in the right third, then shoots).
        /// </summary>
        bool CanAct(Vector2 position)
        {
            if (!_runner.IsInsidePlayfield(position, 0f)) return false;
            return definition.Pattern != MotionPattern.HoldPosition ||
                   HoldingPattern.HasArrived(_start, _speed, _stationX, _age);
        }

        void TickWeapon(float deltaTime, Vector2 position)
        {
            var fired = _burst != null ? _burst.Tick(deltaTime) : _fireTimer != null && _fireTimer.Tick(deltaTime);
            if (fired) _runner.FireEnemyProjectile(position + muzzleOffset, _bulletSpeed, definition.BulletDamage);
        }

        void TickMines(float deltaTime, Vector2 position)
        {
            if (_mineTimer != null && _mineTimer.Tick(deltaTime)) _runner.DropMine(position);
        }

        void TickBeam(float deltaTime, Vector2 position)
        {
            if (_sniper == null) return;

            var origin = position + muzzleOffset;
            _sniper.Tick(deltaTime, origin.ToNumerics(), _runner.NearestPlayerPosition(origin).ToNumerics());

            // Always the line it committed to when the warning began: a warning that followed the ship would be
            // no warning at all. Between shots there is nothing to show, so the line goes out.
            var aim = _sniper.Aim.ToUnity();
            if (beam != null)
            {
                if (_sniper.IsWarning || _sniper.IsFiring)
                    beam.Show(origin, aim, definition.BeamLength, definition.BeamHalfWidth, _sniper.IsFiring,
                        _sniper.WarningProgress);
                else
                    beam.Hide();
            }

            if (_sniper.IsFiring && !_beamWasFiring)
                _runner.FireBeam(origin, aim, definition.BeamLength, definition.BeamHalfWidth, definition.BeamDamage);

            _beamWasFiring = _sniper.IsFiring;
        }

        /// <summary>
        /// Off the left edge for everything that flies left. A charging diver can leave by any edge, so it is
        /// checked against the whole playfield, but only once it charges: while entering it is still outside the
        /// right edge, where a formation's x-offset puts it further out still.
        /// </summary>
        bool IsGone(Vector2 position) =>
            position.x < _runner.Playfield.MinX - GameRunner.DespawnMargin ||
            (_dive != null && _dive.Phase == DivePhase.Charging &&
             !_runner.IsInsidePlayfield(position, GameRunner.DespawnMargin));
    }
}
