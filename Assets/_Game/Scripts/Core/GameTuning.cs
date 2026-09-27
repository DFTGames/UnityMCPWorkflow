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
        /// How far a thumb must push the right virtual stick before it aims, as a fraction of the stick's
        /// reach. **Deliberately much larger than the gamepad's**, and the reason is arithmetic rather than
        /// taste.
        /// </summary>
        /// <remarks>
        /// The aim takes the *angle* of the thumb's offset from where the stick centred; its length is not
        /// used at all. So the only thing deciding how precisely a player can aim is how far out the thumb
        /// is when the aim is first believed, and that distance is the reach times this fraction.
        ///
        /// At the gamepad's 0.3, on a stick reaching 12% of a 1080-pixel screen, that is 39 pixels. A thumb
        /// resting on glass moves about 8 pixels without its owner meaning anything by it, and 8 pixels at
        /// 39 is **12 degrees** of swing, inside a firing arc only 35 degrees wide either way. The ship's
        /// nose wandered and the player was blamed.
        ///
        /// At 0.6 the same wobble is about 6 degrees, and every further increase halves it again. Raise this
        /// if aiming still feels loose: it costs only a longer push before the aim takes over from firing
        /// straight ahead, and nothing else, because the stick's remaining travel was never read.
        ///
        /// A gamepad stick has none of this problem: it is mechanically centred, it springs back, and it is
        /// read as a true analogue axis rather than as a finger's guess.
        /// </remarks>
        public const float TouchAimDeadZone = 0.6f;

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
