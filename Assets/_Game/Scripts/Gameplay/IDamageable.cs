namespace YASS.Gameplay
{
    /// <summary>Something the player's projectiles can damage (enemies and meteors).</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Armour: even piercing shots stop here.</summary>
        bool BlocksPiercing { get; }

        /// <summary>Applies damage from a projectile fired by player <paramref name="playerIndex"/>.</summary>
        void TakeHit(float damage, int playerIndex);
    }
}
