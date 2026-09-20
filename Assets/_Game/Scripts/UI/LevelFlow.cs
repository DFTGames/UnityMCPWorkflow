using UnityEngine;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.UI
{
    /// <summary>
    /// Connects a level to the menus: shows Game Over or Sector Clear once the run has ended, after a short beat
    /// so the last explosion is not hidden by a panel (GDD "UI Flow and Screens").
    /// </summary>
    public sealed class LevelFlow : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] MenuRouter router;
        [SerializeField] ResultsMenu gameOver;
        [SerializeField] ResultsMenu sectorClear;

        [SerializeField, Min(0f), Tooltip("Seconds between the run ending and its results appearing.")]
        float resultDelay = 1.5f;

        bool _shown;

        void Update()
        {
            if (_shown || runner == null || router == null) return;

            var ended = runner.SecondsSinceRunEnded;
            if (ended < 0f) return;

            // Lock immediately, not when the panel appears: the run is already over during the delay, so pausing
            // it would show a Resume button for a run that cannot resume (and would stop the delay itself, since
            // it runs on scaled time).
            router.LockForResult();
            if (ended < resultDelay) return;

            Show(runner.IsSectorClear ? MenuScreen.SectorClear : MenuScreen.GameOver);
        }

        void Show(MenuScreen screen)
        {
            _shown = true;

            var panel = screen == MenuScreen.SectorClear ? sectorClear : gameOver;
            if (panel != null) panel.Fill(runner.Session.Score);

            router.ShowResult(screen);
        }
    }
}
