using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The in-level pause menu's buttons (GDD "UI Flow and Screens", Pause). Opening and closing is the
    /// <see cref="MenuRouter"/>'s job, including stopping game time; this only answers the buttons.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] MenuRouter router;

        void Awake()
        {
            if (router == null)
                Debug.LogError($"{nameof(PauseMenu)}: no router assigned; Resume and Settings will do nothing.", this);
        }

        public void Resume() => router.Back();

        public void OpenSettings() => router.Open(MenuScreen.Settings);

        public void Restart()
        {
            // The router restores time on destroy, but the reload starts before that: do it here too so a paused
            // restart cannot drop into a frozen level.
            Time.timeScale = 1f;
            GameFlow.RestartLevel();
        }

        public void QuitToTitle()
        {
            Time.timeScale = 1f;
            GameFlow.GoToTitle();
        }
    }
}
