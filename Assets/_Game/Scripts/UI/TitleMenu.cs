using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The title screen's buttons (GDD "UI Flow and Screens", Title). Quit is hidden where the platform owns the
    /// application's lifetime (WebGL and mobile).
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] MenuRouter router;
        [SerializeField, Tooltip("Hidden on WebGL and mobile.")] GameObject quitButton;

        void Awake()
        {
            if (router == null)
                Debug.LogError($"{nameof(TitleMenu)}: no router assigned; its buttons will do nothing.", this);

            if (quitButton != null) quitButton.SetActive(GameFlow.CanQuit);
        }

        /// <summary>
        /// Straight to the difficulty screen, unless the player has never been asked their name: a board
        /// entry needs one, and the moment they choose to play is when asking makes sense.
        /// </summary>
        public void Play() =>
            router.Open(NameEntryMenu.NeedsAsking ? MenuScreen.NameEntry : MenuScreen.Difficulty);

        public void OpenLeaderboards() => router.Open(MenuScreen.Leaderboards);

        public void OpenSettings() => router.Open(MenuScreen.Settings);

        public void OpenCredits() => router.Open(MenuScreen.Credits);

        public void Quit() => GameFlow.Quit();
    }
}
