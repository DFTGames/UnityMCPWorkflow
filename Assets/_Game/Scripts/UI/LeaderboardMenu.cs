using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The leaderboard screen (GDD "Scoring", Leaderboards): any of the six boards, chosen by mode and
    /// difficulty. Score chasing is one of the game's three pillars, so a board is reachable without playing.
    /// </summary>
    /// <remarks>
    /// The rows are made once, at the size of the longest board the screen will show, and then filled or
    /// hidden. A board that changes every time it is opened must not allocate a row set every time.
    ///
    /// A board that cannot be reached says so in one line and leaves the screen usable: the player can still
    /// change mode, go back, and start a run.
    /// </remarks>
    public sealed class LeaderboardMenu : MonoBehaviour
    {
        /// <summary>How many places a board shows. A screenful; the rest is not what the player came for.</summary>
        public const int Places = 10;

        [SerializeField] MenuRouter router;

        [SerializeField, Tooltip("One row per place, in order. Filled or hidden as the board needs.")]
        LeaderboardRow[] rows = System.Array.Empty<LeaderboardRow>();

        [SerializeField, Tooltip("Which board is showing, and what went wrong when one will not load.")]
        TMP_Text headingText;

        [SerializeField] TMP_Text messageText;

        [SerializeField, Tooltip("Cycles the mode: Campaign or Endless.")]
        Button modeButton;

        [SerializeField, Tooltip("Cycles the difficulty.")]
        Button difficultyButton;

        [SerializeField] TMP_Text modeLabel;
        [SerializeField] TMP_Text difficultyLabel;

        GameMode _mode = GameMode.Campaign;
        Difficulty _difficulty = Difficulty.Pilot;

        /// <summary>Which board the screen is showing, for tests.</summary>
        internal string ShowingBoard => Leaderboards.IdFor(_mode, _difficulty);

        void OnEnable()
        {
            // The board the player most likely wants: the one they last played.
            if (RunContext.IsConfigured)
            {
                _mode = RunContext.Mode;
                _difficulty = RunContext.Difficulty;
            }

            Refresh();
        }

        public void NextMode()
        {
            _mode = _mode == GameMode.Campaign ? GameMode.Endless : GameMode.Campaign;
            Refresh();
        }

        public void NextDifficulty()
        {
            _difficulty = _difficulty switch
            {
                Difficulty.Cadet => Difficulty.Pilot,
                Difficulty.Pilot => Difficulty.Ace,
                _ => Difficulty.Cadet
            };

            Refresh();
        }

        public void Back() => router.Back();

        void Refresh()
        {
            if (modeLabel != null) modeLabel.text = _mode.ToString();
            if (difficultyLabel != null) difficultyLabel.text = _difficulty.ToString();
            if (headingText != null) headingText.text = $"{_mode} - {_difficulty}";

            Say("Loading...");
            foreach (var row in rows) row.Hide();

            var boards = GameFlow.Leaderboards;
            if (boards.IsReady) boards.Top(_mode, _difficulty, Places, Show);
            else GameFlow.LocalBoards.Top(_mode, _difficulty, Places, ShowLocal);
        }

        void Show(LeaderboardResult result)
        {
            if (this == null) return; // the player left while it was loading

            if (result.Status != LeaderboardStatus.Succeeded)
            {
                // Falling back rather than showing nothing: the player's own runs are better than an error.
                GameFlow.LocalBoards.Top(_mode, _difficulty, Places, ShowLocal);
                return;
            }

            Fill(result, result.Entries.Count == 0 ? "No scores yet. Be the first." : null);
        }

        void ShowLocal(LeaderboardResult result)
        {
            if (this == null) return;

            Fill(result, result.Entries.Count == 0
                ? "No scores yet, and the online boards cannot be reached."
                : "Your runs on this machine. The online boards cannot be reached.");
        }

        void Fill(LeaderboardResult result, string message)
        {
            Say(message);

            for (var i = 0; i < rows.Length; i++)
            {
                if (i < result.Entries.Count) rows[i].Show(result.Entries[i]);
                else rows[i].Hide();
            }
        }

        void Say(string message)
        {
            if (messageText == null) return;

            messageText.text = message ?? string.Empty;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}
