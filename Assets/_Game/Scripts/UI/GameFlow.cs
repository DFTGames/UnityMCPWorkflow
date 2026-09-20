using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Core;

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
        public const string FirstLevelScene = "Level01";

        static SettingsService _settings;

        /// <summary>The difficulty chosen on the difficulty screen; the default until one is chosen.</summary>
        public static Difficulty SelectedDifficulty => RunContext.Difficulty;

        /// <summary>True once the player has picked a difficulty, so a level can tell a real run from a scene opened in the Editor.</summary>
        public static bool HasChosenDifficulty => RunContext.IsConfigured;

        /// <summary>Settings, loaded on first use and shared by every screen.</summary>
        public static SettingsService Settings => _settings ??= new SettingsService(new PlayerPrefsSettingsStore());

        /// <summary>
        /// This project runs with domain reloading off (Enter Play Mode Options), so statics survive from one
        /// play session to the next. Without this reset the second run in the Editor would start with the
        /// difficulty and the settings service left over from the first.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _settings = null;
            RunContext.Clear();
        }

        /// <summary>Test seam: replaces the settings, including their store. Null restores the saved ones.</summary>
        internal static void UseSettings(SettingsService settings) => _settings = settings;

        /// <summary>Test seam: forgets the chosen difficulty, as if the game had just started.</summary>
        internal static void ResetForTests() => RunContext.Clear();

        public static void StartCampaign(Difficulty difficulty)
        {
            RunContext.Configure(difficulty);
            Settings.Flush(); // last chance to save before the level takes over
            SceneManager.LoadScene(FirstLevelScene);
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
