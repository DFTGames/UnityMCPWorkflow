using System;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>A drifting, spinning meteor. Its definition decides whether it splits when destroyed.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MeteorView : MonoBehaviour, IDamageable
    {
        [SerializeField] Rigidbody2D body;
        [SerializeField] SpriteRenderer spriteRenderer;

        GameRunner _runner;
        Action<MeteorView> _release;
        Health _health;
        float _spin;

        public bool IsAlive { get; private set; }
        public MeteorDefinition Definition { get; private set; }
        public Vector2 Velocity { get; private set; }
        public Vector2 Position => body.position;
        public int Points => MeteorRules.Points(Definition.Splitting, Definition.Size);

        void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Activates the meteor, resetting all state so pooled instances can be reused. <paramref name="release"/>
        /// returns it to its pool; when null, <see cref="Despawn"/> destroys it instead.
        /// </summary>
        public void Init(GameRunner runner, MeteorDefinition definition, Vector2 position, Vector2 velocity,
            float spinDegrees, Action<MeteorView> release)
        {
            _runner = runner;
            _release = release;
            Definition = definition;
            Velocity = velocity;
            _spin = spinDegrees;
            _health = new Health(definition.MaxHealth);
            spriteRenderer.sprite = definition.Sprite;

            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one * definition.Scale;
            body.position = position;
            body.rotation = 0f;
            IsAlive = true;
        }

        void FixedUpdate()
        {
            if (!IsAlive) return;

            var deltaTime = Time.fixedDeltaTime;
            var next = body.position + Velocity * deltaTime;
            body.MovePosition(next);
            body.MoveRotation(body.rotation + _spin * deltaTime);

            var field = _runner.Playfield;
            const float margin = GameRunner.DespawnMargin;
            if (next.x < field.MinX - margin || next.y < field.MinY - margin || next.y > field.MaxY + margin)
                Despawn();
        }

        public void TakeHit(float damage, int playerIndex)
        {
            if (IsAlive && _health.TakeDamage(damage)) _runner.OnMeteorDestroyed(this, playerIndex);
        }

        /// <summary>Removes the meteor (back to its pool, or destroyed). Safe to call more than once.</summary>
        public void Despawn()
        {
            if (!IsAlive) return;

            IsAlive = false;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }
    }
}
