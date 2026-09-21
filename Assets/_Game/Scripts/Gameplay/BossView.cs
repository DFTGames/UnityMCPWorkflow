using System.Collections.Generic;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>
    /// Any campaign boss (GDD "Levels"). What it does is data in its <see cref="BossDefinition"/>; this class only
    /// carries the actions out. The root collider is the armoured hull: player shots hitting it are absorbed with
    /// no damage. Only <see cref="BossCoreView"/> (a child collider) can be damaged, and only while the brain has
    /// the core open.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BossView : MonoBehaviour, IDamageable
    {
        [SerializeField] BossDefinition definition;
        [SerializeField] Rigidbody2D body;
        [SerializeField] BossCoreView core;
        [SerializeField, Tooltip("Local positions launched enemies appear at.")]
        Vector2[] launchBays = { new Vector2(-0.6f, 0.9f), new Vector2(-0.6f, -0.9f), new Vector2(-1.2f, 0f) };
        [SerializeField, Tooltip("Local position bullets, meteors and beams leave from.")]
        Vector2 muzzle = new Vector2(-1.4f, 0f);
        [SerializeField, Tooltip("Local positions a turret volley fires from.")]
        Vector2[] turretMuzzles = { new Vector2(-1f, 1.2f), new Vector2(-1f, -1.2f) };
        [SerializeField, Tooltip("Sweeping beam, for the bosses that have one.")] BeamView beam;

        readonly List<BossAction> _actions = new List<BossAction>();
        readonly List<NVector2> _directions = new List<NVector2>();

        GameRunner _runner;

        HitFlash _flash;
        BossBrain _brain;
        BossDash _dash;
        BeamSweep _sweep;
        NVector2 _start;
        NVector2 _position;
        float _holdX;
        float _age;
        bool _arrived;
        float _pullRemaining;
        float _speedMultiplier;

        public bool IsAlive { get; private set; }

        /// <summary>The hull is armour: every shot that hits it stops, piercing or not.</summary>
        public bool BlocksPiercing => true;

        /// <summary>Test seam: number of shots that have landed on the hull.</summary>
        internal int ArmourHits { get; private set; }

        /// <summary>Test seam: whether it is mid-dash (null when this boss never dashes).</summary>
        internal BossDash.DashPhase? DashPhase => _dash?.Phase;

        /// <summary>Test seam: whether its beam is sweeping now.</summary>
        internal bool IsSweeping => _sweep != null;

        public BossBrain Brain => _brain;
        public BossDefinition Definition => definition;
        public float ContactDamage => definition != null ? definition.ContactDamage : 40f;
        public Vector2 Position => body.position;

        void Reset() => body = GetComponent<Rigidbody2D>();

        void Awake() => _flash = GetComponent<HitFlash>();

        public void Init(GameRunner runner, DifficultySettings settings, Vector2 position)
        {
            _runner = runner;
            _brain = new BossBrain(definition.ToSpec());
            _dash = null;
            _sweep = null;
            _pullRemaining = 0f;
            _start = position.ToNumerics();
            _position = _start;
            _arrived = false;
            _holdX = runner.Playfield.MaxX - definition.HoldInset;
            _age = 0f;
            ArmourHits = 0;
            _speedMultiplier = settings.EnemySpeedMultiplier;

            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            if (beam != null) beam.Hide();

            if (core == null) Debug.LogError($"{name} has no core, so it can never be damaged.", this);
            else core.Init(this);

            IsAlive = true;
        }

        void FixedUpdate()
        {
            if (!IsAlive) return;

            var deltaTime = Time.fixedDeltaTime;
            Move(deltaTime);
            var position = _position.ToUnity();

            // Nothing starts until the boss is on station. Its entrance takes a few seconds, and a fight that
            // began while it was still a shape at the edge of the screen would spend its first attacks unseen.
            if (!_arrived) return;

            _actions.Clear();
            _brain.Tick(deltaTime, _actions);
            foreach (var action in _actions) Perform(action, position);

            TickSweep(deltaTime, position);
            TickPull(deltaTime, position);
        }

        /// <summary>
        /// Everything the player sees, on the frame rate rather than the fixed step. The hull is an interpolated
        /// body, so a beam drawn from the physics position would lag the boss it is coming out of.
        /// </summary>
        void Update()
        {
            if (_brain == null) return;

            if (core != null) core.ShowOpen(_brain.IsCoreOpen);
            if (beam == null) return;

            if (IsAlive && _sweep != null)
                beam.Show(Position + muzzle, _sweep.Direction.ToUnity(), definition.BeamLength,
                    definition.BeamHalfWidth, true, 1f);
            else
                beam.Hide();
        }

        /// <summary>
        /// Hull hits. The armour takes a fraction of the damage (its definition decides how much), so shooting a
        /// boss anywhere is worth doing and the fight is not a wait for the core to open. The projectile is used
        /// up either way.
        /// </summary>
        public void TakeHit(float damage, int playerIndex, Vector2 hitPoint)
        {
            if (!IsAlive) return;

            ArmourHits++;
            if (_brain.TakeHullHit(damage))
            {
                Defeated(playerIndex);
                return;
            }

            if (_flash != null) _flash.Flash(); // the armour holding is still a hit, and has to read as one
        }

        public void TakeCoreHit(float damage, int playerIndex)
        {
            if (!IsAlive) return;

            // A closed core is shutters over the core: the shot counts as a hull hit, so it is worth the same as
            // hitting the boss anywhere else and no more. Showing a damage explosion here would teach the player
            // the opposite of the boss's one mechanic.
            if (!_brain.IsCoreOpen)
            {
                TakeHit(damage, playerIndex, Position);
                return;
            }

            if (!_brain.TakeCoreHit(damage))
            {
                if (_flash != null) _flash.Flash();
                Cue.Spawn(Effect.SmallExplosion, Position, Sfx.SmallExplosion);
                return;
            }

            Defeated(playerIndex);
        }

        void Defeated(int playerIndex)
        {
            IsAlive = false;
            _runner.OnBossDefeated(this, playerIndex);
        }

        public void Despawn()
        {
            IsAlive = false;
            if (beam != null) beam.Hide();
            Destroy(gameObject);
        }

        // ---- Internals ---------------------------------------------------------------------------------------

        /// <summary>
        /// Its station is a slow drift; a dash takes it out of that and back. The drift clock stops while it
        /// dashes, so the boss returns to the station it left rather than jumping to where the pattern has moved on to.
        /// </summary>
        void Move(float deltaTime)
        {
            if (_dash != null)
            {
                _position = _dash.Step(deltaTime, _position);
                if (_dash.IsFinished) _dash = null;
            }
            else
            {
                // Read every step rather than cached: resizing the window moves the edge the boss holds off.
                _holdX = _runner.Playfield.MaxX - definition.HoldInset;
                _age += deltaTime;
                _position = BossPatterns.Position(_start, _holdX, definition.EntrySpeed, definition.DriftAmplitude,
                    definition.DriftPeriod, _age);
                if (_position.X <= _holdX + 0.01f) _arrived = true;
            }

            body.MovePosition(_position.ToUnity());
        }

        void Perform(BossAction action, Vector2 position)
        {
            switch (action.Type)
            {
                case BossActionType.LaunchDarts:
                    LaunchMinions(position, action.Count);
                    break;
                case BossActionType.FireSpread:
                    FireSpread(position, action.Count);
                    break;
                case BossActionType.HurlMeteor:
                    HurlMeteors(position, action.Count);
                    break;
                case BossActionType.BeamSweep:
                    StartSweep();
                    break;
                case BossActionType.Dash:
                    StartDash();
                    break;
                case BossActionType.GravityPull:
                    _pullRemaining = definition.PullSeconds;
                    break;
                case BossActionType.TurretVolley:
                    FireTurrets(position);
                    break;
            }
        }

        void LaunchMinions(Vector2 position, int count)
        {
            var prefab = definition.MinionPrefab;
            if (prefab == null || launchBays.Length == 0) return;

            for (var i = 0; i < count; i++)
                _runner.LaunchBossMinion(prefab, position + launchBays[i % launchBays.Length]);
        }

        void FireSpread(Vector2 position, int count)
        {
            var origin = position + muzzle;
            _directions.Clear();
            BossPatterns.Spread(_runner.AimAtNearestPlayer(origin).ToNumerics(), count, definition.SpreadAngle,
                _directions);
            foreach (var direction in _directions)
                _runner.FireEnemyProjectile(origin, direction.ToUnity(), definition.BulletSpeed * _speedMultiplier,
                    definition.BulletDamage);
        }

        /// <summary>Throws rocks at the player, fanned when there is more than one so they cannot all be dodged alike.</summary>
        void HurlMeteors(Vector2 position, int count)
        {
            if (definition.Meteor == null) return;

            var origin = position + muzzle;
            _directions.Clear();
            BossPatterns.Spread(_runner.AimAtNearestPlayer(origin).ToNumerics(), count, definition.SpreadAngle,
                _directions);
            foreach (var direction in _directions)
                _runner.HurlBossMeteor(definition.Meteor, origin,
                    direction.ToUnity() * (definition.MeteorSpeed * _speedMultiplier));
        }

        void FireTurrets(Vector2 position)
        {
            foreach (var turret in turretMuzzles)
            {
                var origin = position + turret;
                _runner.FireEnemyProjectile(origin, _runner.AimAtNearestPlayer(origin),
                    definition.BulletSpeed * _speedMultiplier, definition.BulletDamage);
            }
        }

        void StartDash()
        {
            if (_dash != null)
            {
                // Its table asked for a dash while the last one was still running: that attack is lost, and the
                // boss quietly stops escalating. Worth saying out loud rather than leaving to playtesting.
                Debug.LogWarning($"{definition.DisplayName} was asked to dash while already dashing; " +
                                 "its attack table does not fit its cycle.", this);
                return;
            }

            var target = _runner.Playfield.MinX - definition.DashOvershoot;
            _dash = new BossDash(definition.DashWindUpSeconds, definition.DashSpeed * _speedMultiplier,
                definition.DashReturnSpeed, target, _position);
            Cue.Play(Sfx.BossWarning); // the wind-up needs a sound of its own; this is the closest we have
        }

        /// <summary>
        /// Starts a sweep whether or not this boss has a beam object to draw it with: presentation must never be
        /// able to cancel a rules action, and an invisible beam that hurts is a bug worth seeing in a test.
        /// </summary>
        void StartSweep()
        {
            if (_sweep != null)
            {
                Debug.LogWarning($"{definition.DisplayName} was asked to sweep while already sweeping; " +
                                 "its attack table does not fit its cycle.", this);
                return;
            }

            _sweep = new BeamSweep(definition.SweepFromDegrees, definition.SweepToDegrees, definition.SweepSeconds,
                definition.BeamDamageInterval);
        }

        void TickSweep(float deltaTime, Vector2 position)
        {
            if (_sweep == null) return;

            _sweep.Tick(deltaTime);

            // Tested every step, burned at most every interval: the beam sweeps past a distant ship far faster
            // than its damage clock, so waiting for the clock would miss almost everyone it crossed.
            if (_sweep.CanBurn &&
                _runner.FireBeam(position + muzzle, _sweep.Direction.ToUnity(), definition.BeamLength,
                    definition.BeamHalfWidth, definition.BeamDamage) > 0)
                _sweep.Burned();

            if (_sweep.IsFinished) _sweep = null; // Update puts the beam out on the next frame
        }

        void TickPull(float deltaTime, Vector2 position)
        {
            if (_pullRemaining <= 0f) return;

            _pullRemaining -= deltaTime;
            _runner.PullPlayers(position, definition.PullStrength, definition.PullRadius);
        }
    }
}
