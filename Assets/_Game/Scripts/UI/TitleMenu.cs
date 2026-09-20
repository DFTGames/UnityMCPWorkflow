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

        public void Play() => router.Open(MenuScreen.Difficulty);

        public void OpenSettings() => router.Open(MenuScreen.Settings);

        public void OpenCredits() => router.Open(MenuScreen.Credits);

        public void Quit() => GameFlow.Quit();
    }
}
