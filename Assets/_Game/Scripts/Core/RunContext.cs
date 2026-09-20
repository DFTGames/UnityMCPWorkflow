namespace YASS.Core
{
    /// <summary>
    /// What a level needs to know about the run it belongs to, chosen before the level scene loads.
    /// </summary>
    /// <remarks>
    /// This lives in the rules layer so both the flow layer (which sets it) and the level (which reads it) depend
    /// on Core rather than on each other. It is deliberately tiny: the difficulty and whether one was chosen at
    /// all, so a level scene opened directly still falls back to its own setting.
    /// </remarks>
    public static class RunContext
    {
        public static Difficulty Difficulty { get; private set; } = Difficulty.Pilot;

        /// <summary>True once a difficulty has been chosen for this run.</summary>
        public static bool IsConfigured { get; private set; }

        public static void Configure(Difficulty difficulty)
        {
            Difficulty = difficulty;
            IsConfigured = true;
        }

        /// <summary>Forgets the run, as when returning to the menus.</summary>
        public static void Clear()
        {
            Difficulty = Difficulty.Pilot;
            IsConfigured = false;
        }
    }
}
