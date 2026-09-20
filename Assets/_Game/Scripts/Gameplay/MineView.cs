using System;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// A proximity mine dropped by a Mine Layer. It drifts with the field, arms after a moment, then goes off when
    /// a player comes within <see cref="MineSpec.TriggerRadius"/> (GDD "Enemies and Hazards"). Proximity is read
    /// here rather than with a trigger collider so the blast radius and the mine's small hitbox stay independent:
    /// the collider is only what the player's shots have to hit.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MineView : MonoBehaviour, IDamageable
    {
        [SerializeField] Rigidbody2D body;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField, Min(0f), Tooltip("How fast it drifts left with the field.")] float driftSpeed = 1f;
        [SerializeField, Tooltip("Shown until it arms, so a mine that cannot hurt anyone looks inert.")]
        Color unarmedColour = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] Color armedColour = Color.white;

        GameRunner _runner;
        Action<MineView> _release;
        ProximityMine _mine;

        public bool BlocksPiercing => false;
        public bool IsAlive { get; private set; }
        public Vector2 Position => body.position;
        public int Points => PointValues.Mine;

        void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Arms a mine at <paramref name="position"/>, resetting all state so pooled instances can be reused.
        /// <paramref name="release"/> returns it to its pool; when null, <see cref="Despawn"/> destroys it instead.
        /// </summary>
        public void Init(GameRunner runner, Vector2 position, Action<MineView> release)
        {
            _runner = runner;
            _release = release;
            _mine = MineSpec.Create();
            ShowArmed(false);

            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            IsAlive = true;
        }

        void FixedUpdate()
        {
            if (!IsAlive) return;

            var deltaTime = Time.fixedDeltaTime;
            _mine.Tick(deltaTime);
            ShowArmed(_mine.IsArmed);

            var next = body.position + Vector2.left * (driftSpeed * deltaTime);
            body.MovePosition(next);

            if (_mine.HasExpired || next.x < _runner.Playfield.MinX - GameRunner.DespawnMargin)
            {
                Despawn();
                return;
            }

            if (!_runner.TryGetNearestPlayer(next, out var player)) return;
            if (_mine.ShouldDetonate(Vector2.Distance(player, next))) _runner.OnMineDetonated(this, -1);
        }

        /// <summary>Shooting a mine sets it off wherever it is: safe at a distance, not from close up.</summary>
        public void TakeHit(float damage, int playerIndex, Vector2 hitPoint)
        {
            if (IsAlive) _runner.OnMineDetonated(this, playerIndex);
        }

        /// <summary>Removes the mine (back to its pool, or destroyed). Safe to call more than once.</summary>
        public void Despawn()
        {
            if (!IsAlive) return;

            IsAlive = false;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }

        /// <summary>
        /// Written every step rather than only on a change, for the same reason the boss's core is: whether a
        /// mine is armed is the player's one tell, and nothing else that touches the renderer may leave it wrong.
        /// </summary>
        void ShowArmed(bool armed)
        {
            if (spriteRenderer != null) spriteRenderer.color = armed ? armedColour : unarmedColour;
        }
    }
}
