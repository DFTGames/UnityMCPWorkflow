using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>Something the player's projectiles can damage (enemies, meteors and the boss).</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Armour: even piercing shots stop here.</summary>
        bool BlocksPiercing { get; }

        /// <summary>
        /// Applies damage from a projectile fired by player <paramref name="playerIndex"/>.
        /// <paramref name="hitPoint"/> is where the shot landed, which directional armour (the Frigate's front
        /// shield) needs; targets without any ignore it.
        /// </summary>
        void TakeHit(float damage, int playerIndex, Vector2 hitPoint);
    }
}
