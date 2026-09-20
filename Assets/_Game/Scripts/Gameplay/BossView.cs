using System.Collections.Generic;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>
    /// The Hive Carrier (GDD "Hive Carrier"). The root collider is the armoured hull: player shots hitting it are
    /// absorbed with no damage. Only <see cref="BossCoreView"/> (a child collider) can be damaged, and only while
    /// the brain has the core open.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BossView : MonoBehaviour, IDamageable
    {
        [SerializeField] Rigidbody2D body;
        [SerializeField] BossCoreView core;
        [SerializeField, Tooltip("Local positions the Darts launch from.")]
        Vector2[] launchBays = { new Vector2(-0.6f, 0.9f), new Vector2(-0.6f, -0.9f), new Vector2(-1.2f, 0f) };
        [SerializeField] Vector2 spreadMuzzle = new Vector2(-1.4f, 0f);
        [SerializeField, Min(0f)] float contactDamage = 40f;
        [SerializeField, Min(0.1f)] float entrySpeed = 2f;
        [SerializeField, Tooltip("How far in from the right edge the boss holds.")] float holdInset = 3f;
        [SerializeField, Min(0f)] float driftAmplitude = 2.2f;
        [SerializeField, Min(0.1f)] float driftPeriod = 6f;
        [SerializeField, Range(0f, 180f)] float spreadAngle = 60f;
        [SerializeField, Min(0f)] float spreadBulletSpeed = 6f;
        [SerializeField, Min(0f)] float spreadBulletDamage = 10f;

        readonly List<BossAction> _actions = new List<BossAction>();
        readonly List<NVector2> _directions = new List<NVector2>();

        GameRunner _runner;

        HitFlash _flash;
        HiveCarrierBrain _brain;
        NVector2 _start;
        float _holdX;
        float _age;
        float _bulletSpeedMultiplier;

        public bool IsAlive { get; private set; }

        /// <summary>The hull is armour: every shot that hits it stops, piercing or not.</summary>
        public bool BlocksPiercing => true;

        /// <summary>Test seam: number of shots the armour has absorbed.</summary>
        internal int ArmourHits { get; private set; }
        public HiveCarrierBrain Brain => _brain;
        public float ContactDamage => contactDamage;
        public Vector2 Position => body.position;

        void Reset() => body = GetComponent<Rigidbody2D>();

        void Awake() => _flash = GetComponent<HitFlash>();

        public void Init(GameRunner runner, DifficultySettings settings, Vector2 position, HiveCarrierSpec spec)
        {
            _runner = runner;
            _brain = new HiveCarrierBrain(spec);
            _start = position.ToNumerics();
            _holdX = runner.Playfield.MaxX - holdInset;
            _age = 0f;
            _bulletSpeedMultiplier = settings.EnemySpeedMultiplier;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            core.Init(this);
            IsAlive = true;
        }

        void FixedUpdate()
        {
            if (!IsAlive) return;

            var deltaTime = Time.fixedDeltaTime;
            _age += deltaTime;
            var position = BossPatterns.Position(_start, _holdX, entrySpeed, driftAmplitude, driftPeriod, _age).ToUnity();
            body.MovePosition(position);

            _actions.Clear();
            _brain.Tick(deltaTime, _actions);
            foreach (var action in _actions)
            {
                if (action.Type == BossActionType.LaunchDarts) LaunchDarts(position, action.Count);
                else FireSpread(position, action.Count);
            }

            core.ShowOpen(_brain.IsCoreOpen);
        }

        /// <summary>Hull hits: the armour absorbs the shot with no damage (the projectile is used up).</summary>
        public void TakeHit(float damage, int playerIndex)
        {
            ArmourHits++;
            if (_flash != null) _flash.Flash(); // armour absorbs the shot, but the hit still reads
        }

        public void TakeCoreHit(float damage, int playerIndex)
        {
            if (!IsAlive) return;

            // A closed core absorbs the shot like armour. Showing a damage explosion here would teach the player
            // the opposite of the boss's one mechanic.
            if (!_brain.IsCoreOpen)
            {
                TakeHit(damage, playerIndex);
                return;
            }

            if (!_brain.TakeCoreHit(damage))
            {
                if (_flash != null) _flash.Flash();
                Cue.Spawn(Effect.SmallExplosion, Position, Sfx.SmallExplosion);
                return;
            }

            IsAlive = false;
            _runner.OnBossDefeated(this, playerIndex);
        }

        public void Despawn()
        {
            IsAlive = false;
            Destroy(gameObject);
        }

        void LaunchDarts(Vector2 position, int count)
        {
            for (var i = 0; i < count && launchBays.Length > 0; i++)
                _runner.LaunchBossDart(position + launchBays[i % launchBays.Length]);
        }

        void FireSpread(Vector2 position, int count)
        {
            var origin = position + spreadMuzzle;
            _directions.Clear();
            BossPatterns.Spread(_runner.AimAtNearestPlayer(origin).ToNumerics(), count, spreadAngle, _directions);
            foreach (var direction in _directions)
                _runner.FireEnemyProjectile(origin, direction.ToUnity(), spreadBulletSpeed * _bulletSpeedMultiplier,
                    spreadBulletDamage);
        }
    }
}
