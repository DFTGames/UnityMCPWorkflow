using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.UI
{
    /// <summary>
    /// What survives a scene change: the difficulty the player chose and their settings (GDD "UI Flow and
    /// Screens"). Static because it outlives every scene; kept deliberately thin, with the rules it needs living
    /// in <see cref="SettingsService"/>, <see cref="MenuStack"/> and <see cref="RunContext"/>.
    /// </summary>
    public static class GameFlow
    {
        public const string TitleScene = "Title";

        /// <summary>
        /// The one scene the game is played in. Every campaign level and an Endless run all play here: which
        /// one it is comes from <see cref="RunContext"/> and the campaign's data, not from the scene.
        /// </summary>
        public const string LevelScene = "Level";

        const string CampaignPath = "Campaign"; // in Resources, so the flow needs no scene reference

        static SettingsService _settings;
        static ILeaderboardService _leaderboards;
        static CampaignProgress _progress;
        static Campaign _campaign;

        /// <summary>The difficulty chosen on the difficulty screen; the default until one is chosen.</summary>
        public static Difficulty SelectedDifficulty => RunContext.Difficulty;

        /// <summary>True once the player has picked a difficulty, so a level can tell a real run from a scene opened in the Editor.</summary>
        public static bool HasChosenDifficulty => RunContext.IsConfigured;

        /// <summary>Settings, loaded on first use and shared by every screen.</summary>
        public static SettingsService Settings => _settings ??= new SettingsService(new PlayerPrefsSettingsStore());

        /// <summary>
        /// Where scores go (GDD "Scoring", Leaderboards). The online boards when they can be reached, and the
        /// ones on this machine when they cannot: a player with no network still sees their own best runs,
        /// and nothing about a leaderboard is allowed to stop them playing.
        /// </summary>
        public static ILeaderboardService Leaderboards => _leaderboards ??= new UgsLeaderboards();

        /// <summary>The board to fall back to when the service cannot be reached.</summary>
        public static ILeaderboardService LocalBoards { get; } = new LocalLeaderboards(new PlayerPrefsSettingsStore());

        /// <summary>
        /// Sends a finished run to the board for its mode and difficulty, and to the local one either way so
        /// the player's own history is kept whatever the network did.
        /// </summary>
        public static void SubmitRun(long score, Action<LeaderboardResult> done = null)
        {
            var mode = RunContext.Mode;
            var difficulty = RunContext.Difficulty;
            var name = Settings.PlayerName;

            LocalBoards.Submit(mode, difficulty, name, score);

            if (Leaderboards.IsReady) Leaderboards.Submit(mode, difficulty, name, score, done);
            else done?.Invoke(LeaderboardResult.Failure("the boards could not be reached"));
        }

        /// <summary>What the player has finished before, across sessions.</summary>
        public static CampaignProgress Progress => _progress ??= new CampaignProgress(new PlayerPrefsSettingsStore());

        /// <summary>The campaign's levels, in order.</summary>
        public static Campaign Campaign
        {
            get
            {
                if (_campaign == null) _campaign = Resources.Load<Campaign>(CampaignPath);
                if (_campaign == null) Debug.LogError($"No campaign asset at Resources/{CampaignPath}.");

                return _campaign;
            }
        }

        /// <summary>The campaign run in progress, or null outside one.</summary>
        public static CampaignRun Run => RunContext.Campaign;

        /// <summary>
        /// This project runs with domain reloading off (Enter Play Mode Options), so statics survive from one
        /// play session to the next. Without this reset the second run in the Editor would start with the
        /// difficulty and the settings service left over from the first.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _settings = null;
            _leaderboards = null;
            _progress = null;
            _campaign = null;
            RunContext.Clear();
        }

        /// <summary>Test seam: replaces the settings, including their store. Null restores the saved ones.</summary>
        internal static void UseSettings(SettingsService settings) => _settings = settings;

        /// <summary>Test seam: replaces the boards, so a test never talks to a real service.</summary>
        internal static void UseLeaderboards(ILeaderboardService boards) => _leaderboards = boards;

        /// <summary>Test seam: replaces saved progress, so a test never reads or writes the player's own.</summary>
        internal static void UseProgress(CampaignProgress progress) => _progress = progress;

        /// <summary>Test seam: forgets the chosen difficulty, as if the game had just started.</summary>
        internal static void ResetForTests() => RunContext.Clear();

        public static void StartCampaign(Difficulty difficulty)
        {
            var campaign = Campaign;
            if (campaign == null) return;

            RunContext.Begin(campaign.NewRun(difficulty));
            Settings.Flush(); // last chance to save before the level takes over

            SceneManager.LoadScene(LevelScene);
        }

        /// <summary>
        /// Starts an Endless run (GDD "Core Loop", Endless mode). It has no campaign behind it: there is one
        /// scene, and it keeps going until the player does not.
        /// </summary>
        public static void StartEndless(Difficulty difficulty)
        {
            RunContext.Clear();
            RunContext.Configure(difficulty, GameMode.Endless);
            Settings.Flush();

            SceneManager.LoadScene(LevelScene);
        }

        /// <summary>
        /// Moves on to the next level of the campaign, or reports that there is none left to play, which is
        /// how the flow knows the run has been won (GDD "Core Loop", Win conditions).
        /// </summary>
        public static bool TryAdvanceToNextLevel()
        {
            var run = RunContext.Campaign;
            var campaign = Campaign;
            if (run == null || campaign == null || run.IsComplete) return false;
            if (campaign.LevelFor(run.LevelIndex) == null) return false;

            // The same scene again: the run has moved on, so it loads the next level's data.
            SceneManager.LoadScene(LevelScene);
            return true;
        }

        /// <summary>Plays the current campaign level again after a game over, keeping the run's difficulty.</summary>
        public static void RestartCampaignLevel()
        {
            var run = RunContext.Campaign;
            if (run == null)
            {
                RestartLevel();
                return;
            }

            // A retry starts the level fresh but keeps what the run has banked from earlier levels.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Plays the current level again, keeping the chosen difficulty.</summary>
        public static void RestartLevel() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        public static void GoToTitle()
        {
            RunContext.Clear(); // back at the menus, no run is configured
            SceneManager.LoadScene(TitleScene);
        }

        /// <summary>
        /// True where quitting is the player's job rather than the platform's: the Quit button is hidden on
        /// WebGL and mobile (GDD "UI Flow and Screens", Title).
        /// </summary>
        public static bool CanQuit =>
            Application.platform != RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform;

        /// <summary>Whether the window is the game's to control, which is where the Fullscreen setting applies.</summary>
        public static bool SupportsFullscreen => CanQuit;

        public static void Quit()
        {
            Settings.Flush();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
