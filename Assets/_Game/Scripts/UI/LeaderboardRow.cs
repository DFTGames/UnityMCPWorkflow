using System.Globalization;
using TMPro;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>One line of the leaderboard screen: place, name and score.</summary>
    /// <remarks>
    /// A MonoBehaviour rather than three loose labels, because the player's own row is picked out in the
    /// game's highlight colour and something has to own that decision (GDD "Art Direction", Menu palette).
    /// </remarks>
    public sealed class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] TMP_Text rankText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text scoreText;

        [SerializeField, Tooltip("The colour of everyone else's runs.")]
        Color normal = Color.white;

        [SerializeField, Tooltip("The colour of the player's own run, so they can find themselves.")]
        Color yours = new Color(1f, 0.82f, 0.3f);

        /// <summary>What this row is showing, for tests. Empty when the row is hidden.</summary>
        internal string Line => gameObject.activeSelf && rankText != null && nameText != null && scoreText != null
            ? $"{rankText.text} {nameText.text} {scoreText.text}"
            : string.Empty;

        public void Show(LeaderboardEntry entry)
        {
            gameObject.SetActive(true);

            var colour = entry.IsYou ? yours : normal;
            Write(rankText, entry.Rank.ToString(), colour);
            // The whole name, however long. The label is 420 px of 28 pt, which holds about 29 characters,
            // and its overflow mode is Ellipsis, so TMP shortens at the pixel where a name stops fitting.
            // Cutting to a character count here was worse at the same job: it shortened names that fit.
            Write(nameText, entry.Name, colour);

            // Thousands separated: a six figure score is unreadable otherwise, and reading the board is the
            // whole point of it. Invariant, so one board does not read differently on each player's machine.
            Write(scoreText, entry.Score.ToString("N0", CultureInfo.InvariantCulture), colour);
        }

        public void Hide() => gameObject.SetActive(false);

        static void Write(TMP_Text label, string text, Color colour)
        {
            if (label == null) return;

            label.text = text;
            label.color = colour;
        }
    }
}
