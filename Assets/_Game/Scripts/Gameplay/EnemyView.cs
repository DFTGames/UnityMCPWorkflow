using System;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>An enemy ship: follows its motion pattern, fires at the player and reports its death to the runner.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyView : MonoBehaviour, IDamageable
    {
        [SerializeField] EnemyDefinition definition;
        [SerializeField] Rigidbody2D body;
        [SerializeField] Vector2 muzzleOffset = new Vector2(-0.4f, 0f);

        GameRunner _runner;

        HitFlash _flash;
        Action<EnemyView> _release;
        Health _health;
        FireTimer _fireTimer;
        NVector2 _start;
        float _age;
        float _speed;
        float _bulletSpeed;

        public bool BlocksPiercing => false;
        public bool IsAlive { get; private set; }

        /// <summary>The wave this hazard belongs to, or -1 (boss launches, test spawns). Set by the runner after Init.</summary>
        public int WaveIndex { get; internal set; } = -1;
        public EnemyDefinition Definition => definition;
        public int Points => PointValues.Enemy(definition.Size);

        /// <summary>How big a bang this enemy goes out with (GDD "Art Direction", Visual effects).</summary>
        public EnemySize Size => definition.Size;
        public float ContactDamage => definition.ContactDamage;
        public Vector2 Position => body.position;

        void Reset() => body = GetComponent<Rigidbody2D>();

        /// <summary>
        /// Activates the enemy at <paramref name="position"/>, resetting all state so pooled instances can be reused.
        /// <paramref name="release"/> returns it to its pool; when null, <see cref="Despawn"/> destroys it instead.
        /// Until Init is called the enemy is inert.
        /// </summary>
        void Awake() => _flash = GetComponent<HitFlash>();

        public void Init(GameRunner runner, DifficultySettings settings, Vector2 position, Action<EnemyView> release)
        {
            _runner = runner;
            _release = release;
            WaveIndex = -1;
            _health = new Health(definition.MaxHealth);
            _start = position.ToNumerics();
            _age = 0f;
            _speed = definition.Speed * settings.EnemySpeedMultiplier;
            _bulletSpeed = definition.BulletSpeed * settings.EnemySpeedMultiplier;
            _fireTimer = definition.Fires
                ? new FireTimer(definition.FireInterval / settings.EnemyFireRateMultiplier, definition.FirstShotDelay)
                : null;

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
            var position = EnemyMotion.Evaluate(definition.Pattern, _start, _speed, definition.WaveAmplitude,
                definition.WaveFrequency, _age).ToUnity();
            body.MovePosition(position);

            if (_fireTimer != null && _fireTimer.Tick(deltaTime) && _runner.IsInsidePlayfield(position, 0f))
                _runner.FireEnemyProjectile(position + muzzleOffset, _bulletSpeed, definition.BulletDamage);

            if (position.x < _runner.Playfield.MinX - GameRunner.DespawnMargin) Despawn();
        }

        public void TakeHit(float damage, int playerIndex)
        {
            if (!IsAlive) return;

            if (_health.TakeDamage(damage)) _runner.OnEnemyDestroyed(this, playerIndex);
            else if (_flash != null) _flash.Flash(); // a hit that does not kill must still read
        }

        /// <summary>Removes the enemy (back to its pool, or destroyed). Safe to call more than once.</summary>
        public void Despawn()
        {
            if (!IsAlive) return;

            IsAlive = false;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
