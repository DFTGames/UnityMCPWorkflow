using System;
using TMPro;
using UnityEngine;
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
        LeaderboardRow[] rows = Array.Empty<LeaderboardRow>();

        [SerializeField, Tooltip("Which board is showing, and what went wrong when one will not load.")]
        TMP_Text headingText;

        [SerializeField] TMP_Text messageText;

        [SerializeField, Tooltip("What the mode button says: Campaign or Endless.")]
        TMP_Text modeLabel;

        [SerializeField, Tooltip("What the difficulty button says.")]
        TMP_Text difficultyLabel;

        GameMode _mode = GameMode.Campaign;
        Difficulty _difficulty = Difficulty.Pilot;

        /// <summary>
        /// Which load the screen is waiting for. Every answer arrives later than the question, and cycling the
        /// mode twice quickly asks three questions: without this, the first board back wins and the screen ends
        /// up showing something other than what its heading says.
        /// </summary>
        int _asked;

        /// <summary>Which board the screen is showing, for tests.</summary>
        internal string ShowingBoard => Leaderboards.IdFor(_mode, _difficulty);

        void OnEnable()
        {
            // The board the player most likely wants: the one they last played. Read from what the player
            // has done rather than from the run in progress, because there is never a run in progress here:
            // every route back to the title clears it before the scene loads.
            if (RunContext.HasPlayed)
            {
                _mode = RunContext.LastMode;
                _difficulty = RunContext.LastDifficulty;
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

        public void Back()
        {
            if (router != null) router.Back();
        }

        void Refresh()
        {
            if (modeLabel != null) modeLabel.text = _mode.ToString();
            if (difficultyLabel != null) difficultyLabel.text = _difficulty.ToString();
            if (headingText != null) headingText.text = $"{_mode} - {_difficulty}";

            Say("Loading...");
            foreach (var row in rows)
                if (row != null) row.Hide();

            // Opening the screen is what signs the player in, so the answer can arrive a frame or more later;
            // the screen stays usable in the meantime and the player may have left by then.
            var asked = ++_asked;
            GameFlow.WhenReady(ready =>
            {
                if (Stale(asked)) return;

                if (ready) GameFlow.Leaderboards.Top(_mode, _difficulty, Places, result => Show(asked, result));
                else ShowLocal(asked);
            });
        }

        void Show(int asked, LeaderboardResult result)
        {
            if (Stale(asked)) return;

            if (result.Status != LeaderboardStatus.Succeeded)
            {
                // Falling back rather than showing nothing: the player's own runs are better than an error.
                ShowLocal(asked);
                return;
            }

            Fill(result, result.Entries.Count == 0 ? "No scores yet. Be the first." : null);
        }

        void ShowLocal(int asked) =>
            GameFlow.LocalBoards.Top(_mode, _difficulty, Places, result =>
            {
                if (Stale(asked)) return;

                Fill(result, result.Entries.Count == 0
                    ? "No scores yet, and the online boards cannot be reached."
                    : "Your runs on this machine. The online boards cannot be reached.");
            });

        /// <summary>True once the player has left the screen or asked for a different board.</summary>
        bool Stale(int asked) => this == null || asked != _asked;

        /// <summary>
        /// Leaving cancels whatever was in flight. Back only deactivates the panel, so without this a board
        /// that arrived afterwards would fill and re-show rows on a screen nobody is looking at.
        /// </summary>
        void OnDisable() => _asked++;

        void Fill(LeaderboardResult result, string message)
        {
            Say(message);

            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;

                if (i < result.Entries.Count) rows[i].Show(result.Entries[i]);
                else rows[i].Hide();
            }
        }

        /// <summary>
        /// Writes the message line, leaving it in place when it is empty. Deactivating it would take it out
        /// of the column's layout, so the heading, every row and the Back button would all jump a line the
        /// moment a board finished loading, and jump back on the next press.
        /// </summary>
        void Say(string message)
        {
            if (messageText != null) messageText.text = message ?? string.Empty;
        }
    }
}
