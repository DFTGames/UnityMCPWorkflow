using System;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>A collectable pickup drifting left with the scroll (GDD "Items and Pickups").</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PickupView : MonoBehaviour
    {
        [SerializeField] Rigidbody2D body;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Sprite healthSprite;
        [SerializeField] Sprite weaponUpgradeSprite;
        [SerializeField] Sprite shieldSprite;
        [SerializeField] Sprite extraLifeSprite;

        GameRunner _runner;
        Action<PickupView> _release;
        float _driftSpeed;

        public bool IsActive { get; private set; }
        public PickupType Type { get; private set; }

        void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Activates the pickup, resetting all state so pooled instances can be reused. <paramref name="release"/>
        /// returns it to its pool; when null, <see cref="Despawn"/> destroys it instead.
        /// </summary>
        public void Init(GameRunner runner, PickupType type, float driftSpeed, Vector2 position,
            Action<PickupView> release)
        {
            _runner = runner;
            _release = release;
            Type = type;
            _driftSpeed = driftSpeed;
            spriteRenderer.sprite = SpriteFor(type);

            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.position = position;
            IsActive = true;
        }

        void FixedUpdate()
        {
            if (!IsActive) return;

            var next = body.position + Vector2.left * (_driftSpeed * Time.fixedDeltaTime);
            body.MovePosition(next);
            if (next.x < _runner.Playfield.MinX - GameRunner.DespawnMargin) Despawn();
        }

        /// <summary>Removes the pickup (back to its pool, or destroyed). Safe to call more than once.</summary>
        public void Despawn()
        {
            if (!IsActive) return;

            IsActive = false;
            if (_release != null) _release(this);
            else Destroy(gameObject);
        }

        Sprite SpriteFor(PickupType type)
        {
            switch (type)
            {
                case PickupType.Health: return healthSprite;
                case PickupType.WeaponUpgrade: return weaponUpgradeSprite;
                case PickupType.Shield: return shieldSprite;
                case PickupType.ExtraLife: return extraLifeSprite;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
