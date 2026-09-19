namespace YASS.Core
{
    /// <summary>
    /// Fixed tuning values from the GDD (Mechanics, Items and Pickups, Scoring, Controls). Tables that belong to a
    /// single rule (difficulty, weapon levels, point values, drop weights) live next to that rule instead.
    /// Until data assets exist, the code is the implementation of record: change the GDD first, then these values.
    /// </summary>
    public static class GameTuning
    {
        public const float RespawnInvulnerabilitySeconds = 2f;

        public const int ShieldHits = 3;
        public const float ShieldDurationSeconds = 10f;
        public const float ShieldFlickerSeconds = 2f;

        public const float ChainWindowSeconds = 1.5f;
        public const int ChainMaxSteps = 20; // Each step adds x0.1, so the chain tops out at x3.0.

        public const float WeaponPityDelaySeconds = 45f;
        public const int WeaponPityBelowLevel = 3;

        public const float GamepadAimDeadZone = 0.3f;

        /// <summary>Provisional: the GDD lists this amount as still to be tuned.</summary>
        public const float HealthPickupAmount = 25f;
    }
}
