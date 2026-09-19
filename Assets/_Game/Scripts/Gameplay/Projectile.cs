using UnityEngine;
using UnityEngine.Pool;

namespace YASS.Gameplay
{
    /// <summary>
    /// A pooled bullet. Player bullets damage <see cref="IDamageable"/> targets; enemy bullets are handled by
    /// <see cref="PlayerShipView"/>. Physics layers decide what each kind can touch.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] Rigidbody2D body;

        GameRunner _runner;
        IObjectPool<Projectile> _pool;
        Vector2 _velocity;

        public bool IsActive { get; private set; }
        public float Damage { get; private set; }
        public int OwnerIndex { get; private set; }
        public bool Piercing { get; private set; }

        void Reset() => body = GetComponent<Rigidbody2D>();

        public void Launch(GameRunner runner, IObjectPool<Projectile> pool, Vector2 position, Vector2 direction,
            float speed, float damage, bool piercing, int ownerIndex)
        {
            _runner = runner;
            _pool = pool;
            _velocity = direction * speed;
            Damage = damage;
            Piercing = piercing;
            OwnerIndex = ownerIndex;

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            body.position = position;
            body.rotation = angle;
            IsActive = true;
        }

        void FixedUpdate()
        {
            if (!IsActive) return;

            var next = body.position + _velocity * Time.fixedDeltaTime;
            body.MovePosition(next);
            if (!_runner.IsInsidePlayfield(next, GameRunner.DespawnMargin)) Release();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive || !other.TryGetComponent(out IDamageable target) || !target.IsAlive) return;

            target.TakeHit(Damage, OwnerIndex);
            if (!Piercing) Release();
        }

        /// <summary>Returns the projectile to its pool. Safe to call more than once.</summary>
        public void Release()
        {
            if (!IsActive) return;

            IsActive = false;
            _pool.Release(this);
        }
    }
}
