using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// Presentation of one player's ship. Moves where the runner tells it, shows blink and shield state, and
    /// forwards every collision to the runner, which applies the rules.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerShipView : MonoBehaviour
    {
        [SerializeField] Rigidbody2D body;
        [SerializeField] SpriteRenderer shipRenderer;
        [SerializeField] SpriteRenderer shieldRenderer;
        [SerializeField] EngineExhaust engine;
        [SerializeField, Min(0f), Tooltip("Shots spawn this far from the ship's centre, in the aim direction.")]
        float muzzleDistance = 0.55f;
        [SerializeField, Min(0.1f)] float blinksPerSecond = 10f;

        GameRunner _runner;

        public int PlayerIndex { get; private set; }
        public EngineExhaust Engine => engine;
        public Vector2 Position => body.position;

        void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            shipRenderer = GetComponent<SpriteRenderer>();
        }

        public void Init(GameRunner runner, int playerIndex)
        {
            _runner = runner;
            PlayerIndex = playerIndex;
        }

        public Vector2 MuzzleAt(Vector2 shipPosition, Vector2 aimDirection) => shipPosition + aimDirection * muzzleDistance;

        public void MoveTo(Vector2 position) => body.MovePosition(position);

        /// <summary>Updates visuals from the rules state: blinking while invulnerable, shield bubble (flickering near expiry).</summary>
        public void Present(PlayerShip ship)
        {
            shipRenderer.enabled = VisualCues.IsBlinkVisible(ship.Vitals.InvulnerabilityRemaining, blinksPerSecond);

            var shield = ship.Shield;
            shieldRenderer.enabled = shield.IsActive &&
                                     (!shield.IsFlickering || VisualCues.IsBlinkVisible(shield.TimeRemaining, blinksPerSecond));
        }

        /// <summary>Engine exhaust grows with how fast the ship is moving (fraction of top speed).</summary>
        public void DriveEngine(System.Numerics.Vector2 movement, float deltaTime)
        {
            if (engine != null) engine.Drive(movement, deltaTime);
        }

        public void SetAlive(bool alive) => gameObject.SetActive(alive);

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_runner == null) return;

            if (other.TryGetComponent(out Projectile projectile))
            {
                if (projectile.IsActive && _runner.OnPlayerHitByProjectile(this, projectile)) projectile.Release();
            }
            else if (other.TryGetComponent(out PickupView pickup))
            {
                _runner.OnPickupCollected(this, pickup);
            }
            else
            {
                HandleHazardContact(other);
            }
        }

        // Hazards are re-checked every step: a ship still overlapping a slow meteor when its invulnerability ends
        // must take the hit even though no new Enter event fires. Rams while invulnerable are ignored by the rules.
        void OnTriggerStay2D(Collider2D other)
        {
            if (_runner != null) HandleHazardContact(other);
        }

        void HandleHazardContact(Collider2D other)
        {
            if (other.TryGetComponent(out EnemyView enemy)) _runner.OnPlayerRammedEnemy(this, enemy);
            else if (other.TryGetComponent(out MeteorView meteor)) _runner.OnPlayerRammedMeteor(this, meteor);
        }
    }
}
