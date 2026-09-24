using System;
using System.Text;

namespace YASS.Core
{
    /// <summary>One line of a leaderboard (GDD "Scoring", Leaderboards).</summary>
    public readonly struct LeaderboardEntry
    {
        /// <summary>Counting from one, as the player reads it.</summary>
        public readonly int Rank;

        public readonly string Name;
        public readonly long Score;

        /// <summary>True for the entry belonging to the player looking at the board.</summary>
        public readonly bool IsYou;

        public LeaderboardEntry(int rank, string name, long score, bool isYou = false)
        {
            if (rank < 1) throw new ArgumentOutOfRangeException(nameof(rank));

            Rank = rank;
            Name = name;
            Score = score;
            IsYou = isYou;
        }
    }

    /// <summary>
    /// Which board a run belongs to, and what may be written on it. The boards themselves are Unity Gaming
    /// Services (GDD "Scoring": six of them, one per mode and difficulty); this is only the part that can be
    /// decided without a network.
    /// </summary>
    public static class Leaderboards
    {
        /// <summary>How many boards there are: two modes by three difficulties.</summary>
        public const int Count = 6;

        /// <summary>The longest name a player may put on a board.</summary>
        public const int MaxNameLength = 12;

        /// <summary>What an entry is called before the player has chosen anything.</summary>
        public const string DefaultName = "Pilot";

        /// <summary>
        /// The board for a mode and difficulty, as the service knows it. Lower case with an underscore so the
        /// same string can be typed into the dashboard without ambiguity, and stable: renaming one of these
        /// orphans every score already on it.
        /// </summary>
        public static string IdFor(GameMode mode, Difficulty difficulty) =>
            $"{mode.ToString().ToLowerInvariant()}_{difficulty.ToString().ToLowerInvariant()}";

        /// <summary>Every board the game uses, in the order the menus show them.</summary>
        public static string[] AllIds()
        {
            var ids = new string[Count];
            var next = 0;

            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
                ids[next++] = IdFor(mode, difficulty);

            return ids;
        }

        /// <summary>
        /// A name fit to go on a public board: no spaces, cut to length, and never empty. Deliberately not a
        /// judgement about what the name says, which is the service's moderation to make, not this game's.
        /// </summary>
        /// <remarks>
        /// Every space goes, not just the ones at the ends. Unity Authentication rejects a player name
        /// containing any whitespace at all, and that rejection arrives in the middle of submitting a score
        /// and takes the score down with it: "Red Baron" would have meant a run that silently never reached
        /// any board, indistinguishable from having no network. The name is written back into the box as it
        /// is cleaned, so the player sees what their board entry will say.
        /// </remarks>
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return DefaultName;

            var kept = new StringBuilder(MaxNameLength);
            foreach (var letter in name)
            {
                if (char.IsWhiteSpace(letter)) continue;
                if (kept.Length == MaxNameLength) break;

                kept.Append(letter);
            }

            return kept.Length == 0 ? DefaultName : kept.ToString();
        }

        /// <summary>
        /// Whether a finished run is worth sending. A score of nothing says only that somebody opened the
        /// game, and a board full of zeroes buries the runs that meant something.
        /// </summary>
        public static bool WorthSubmitting(long score) => score > 0;
    }
}
