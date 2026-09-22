using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using YASS.Gameplay;

namespace YASS.UI
{
    /// <summary>
    /// Connects a level to the menus and to the campaign around it: shows Game Over, Sector Clear or the
    /// campaign ending once the run has ended, after a short beat so the last explosion is not hidden by a
    /// panel (GDD "UI Flow and Screens").
    /// </summary>
    public sealed class LevelFlow : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] MenuRouter router;
        [SerializeField] ResultsMenu gameOver;
        [SerializeField] ResultsMenu sectorClear;

        [SerializeField, Tooltip("Shown instead of Sector Clear after the campaign's last level.")]
        ResultsMenu victory;

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

            _shown = true;
            if (runner.IsSectorClear) ShowClear();
            else ShowGameOver();
        }

        void ShowGameOver()
        {
            if (gameOver != null)
            {
                gameOver.Fill(runner.Session.Score);

                // How far an Endless run got is half its result; the campaign has nothing to add here.
                var run = runner.EndlessRun;
                gameOver.SetNote(run != null ? $"Reached cycle {run.Cycle}" : null);
            }

            Cue.Play(Sfx.GameOver);
            router.ShowResult(MenuScreen.GameOver);
        }

        /// <summary>
        /// A cleared level banks its score into the run and moves it on. Clearing the last level wins the
        /// campaign (GDD "Core Loop", Win conditions), which is also what records it as finished.
        /// </summary>
        void ShowClear()
        {
            var run = GameFlow.Run;
            if (run == null)
            {
                // A level played on its own, outside a campaign: show what that level scored.
                if (sectorClear != null) sectorClear.Fill(runner.Session.Score);

                Cue.Play(Sfx.SectorClear);
                router.ShowResult(MenuScreen.SectorClear);
                return;
            }

            var wasFinalLevel = run.IsFinalLevel;
            var levelNumber = run.LevelNumber;

            run.CompleteLevel(runner.Session);
            GameFlow.Progress.RecordLevelCleared(run.Difficulty, levelNumber, run.LevelCount);

            // The panels show the run's totals, not this level's: a campaign is one score.
            var panel = wasFinalLevel ? victory : sectorClear;
            if (panel != null) panel.Fill(run.CarriedScore, run.CarriedKills, run.BestChainSteps, 0L);

            Cue.Play(Sfx.SectorClear);
            router.ShowResult(wasFinalLevel ? MenuScreen.Victory : MenuScreen.SectorClear);
        }
    }
}
