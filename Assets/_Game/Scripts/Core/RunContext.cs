namespace YASS.Core
{
    /// <summary>Which mode a run is being played in (GDD "Core Loop").</summary>
    public enum GameMode
    {
        Campaign = 0,
        Endless = 1
    }

    /// <summary>
    /// What a level needs to know about the run it belongs to, chosen before the level scene loads.
    /// </summary>
    /// <remarks>
    /// This lives in the rules layer so both the flow layer (which sets it) and the level (which reads it)
    /// depend on Core rather than on each other. It is deliberately small: the difficulty, the campaign run in
    /// progress, and whether either was configured at all, so a level scene opened directly still works.
    /// </remarks>
    public static class RunContext
    {
        public static Difficulty Difficulty { get; private set; } = Difficulty.Pilot;

        /// <summary>True once a difficulty has been chosen for this run.</summary>
        public static bool IsConfigured { get; private set; }

        /// <summary>Which mode this run is; a scene opened on its own is a campaign level.</summary>
        public static GameMode Mode { get; private set; } = GameMode.Campaign;

        /// <summary>The campaign in progress, or null when a level is being played on its own.</summary>
        public static CampaignRun Campaign { get; private set; }

        /// <summary>
        /// What the player last chose to play. Deliberately survives <see cref="Clear"/>, which forgets the
        /// run: this is a fact about the player, not about the run, and it is how the leaderboard screen
        /// opens on the board they were just on (GDD "Scoring", Leaderboards). False until something has
        /// been played, which is what a cold start looks like.
        /// </summary>
        public static bool HasPlayed { get; private set; }

        public static GameMode LastMode { get; private set; } = GameMode.Campaign;

        public static Difficulty LastDifficulty { get; private set; } = Difficulty.Pilot;

        public static void Configure(Difficulty difficulty, GameMode mode = GameMode.Campaign)
        {
            Difficulty = difficulty;
            Mode = mode;
            IsConfigured = true;

            HasPlayed = true;
            LastMode = mode;
            LastDifficulty = difficulty;
        }

        /// <summary>Starts a campaign: the difficulty comes from the run itself.</summary>
        public static void Begin(CampaignRun campaign)
        {
            Campaign = campaign;
            Configure(campaign.Difficulty);
        }

        /// <summary>Forgets the run, as when returning to the menus. What was last played is kept.</summary>
        public static void Clear()
        {
            Difficulty = Difficulty.Pilot;
            Mode = GameMode.Campaign;
            IsConfigured = false;
            Campaign = null;
        }

        /// <summary>
        /// Forgets everything, including what was last played. This project runs with domain reloading off,
        /// so a new play session has to be told to start blank; <see cref="Clear"/> alone would carry the
        /// previous session's choices into it.
        /// </summary>
        public static void Reset()
        {
            Clear();
            HasPlayed = false;
            LastMode = GameMode.Campaign;
            LastDifficulty = Difficulty.Pilot;
        }
    }
}
