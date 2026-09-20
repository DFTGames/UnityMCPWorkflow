using TMPro;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;

namespace YASS.UI
{
    /// <summary>
    /// The Game Over and Sector Clear panels (GDD "UI Flow and Screens"): the run's figures plus the buttons that
    /// leave them. Filled once, when the run ends, by <see cref="Fill"/>, so plain strings are cheap enough here
    /// (unlike the HUD, which rewrites them every frame and formats into a reused buffer).
    /// </summary>
    public sealed class ResultsMenu : MonoBehaviour
    {
        [Header("Figures (optional per panel)")]
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text killsText;
        [SerializeField] TMP_Text chainText;
        [SerializeField] TMP_Text clearBonusText;

        [Header("Labels")]
        [SerializeField] string scorePrefix = "Score ";
        [SerializeField] string killsPrefix = "Kills ";
        [SerializeField] string chainPrefix = "Best chain ";
        [SerializeField] string clearBonusPrefix = "Sector bonus ";

        /// <summary>
        /// Shows one run's figures: total score and kills, the best chain (Game Over) and the bonus points that
        /// made up the total (Sector Clear). Panels leave out whatever they do not show.
        /// </summary>
        public void Fill(ScoreKeeper score)
        {
            Write(scoreText, scorePrefix, score.Score.ToString());
            Write(killsText, killsPrefix, score.Kills.ToString());
            Write(clearBonusText, clearBonusPrefix, score.BonusPoints.ToString());
            Write(chainText, chainPrefix, Multiplier(score.BestChainMultiplier));
        }

        public void Retry()
        {
            Cue.Play(Sfx.UiConfirm);
            Time.timeScale = 1f;
            GameFlow.RestartLevel();
        }

        public void QuitToTitle()
        {
            Cue.Play(Sfx.UiConfirm);
            Time.timeScale = 1f;
            GameFlow.GoToTitle();
        }

        /// <summary>Written the way the HUD writes a live chain, so the two read alike: "x2.1".</summary>
        static string Multiplier(float value)
        {
            var buffer = new char[NumberFormatter.MaxMultiplierLength];
            return new string(buffer, 0, NumberFormatter.WriteMultiplier(value, buffer));
        }

        static void Write(TMP_Text text, string prefix, string value)
        {
            if (text != null) text.text = prefix + value;
        }
    }
}
