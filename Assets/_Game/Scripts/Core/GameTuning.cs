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

        /// <summary>
        /// Side-scroller firing arc: shots (and the ship's tilt) stay within this many degrees of straight ahead,
        /// either way. Aiming further round is clamped, not ignored (GDD "Controls", Aim tilt).
        /// </summary>
        public const float FiringArcDegrees = 35f;

        /// <summary>Provisional: after a boss collision hurts a player, further boss contact is ignored this long.</summary>
        public const float BossContactCooldownSeconds = 1f;

        /// <summary>Provisional: the GDD lists this amount as still to be tuned.</summary>
        public const float HealthPickupAmount = 25f;
    }
}
