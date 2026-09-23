using System;
using System.Collections.Generic;

namespace YASS.Core
{
    /// <summary>How a board request ended, so the UI can say something useful rather than spin.</summary>
    public enum LeaderboardStatus
    {
        /// <summary>The request has not finished.</summary>
        Working = 0,

        Succeeded,

        /// <summary>The service could not be reached, or refused. The game carries on regardless.</summary>
        Failed
    }

    /// <summary>The result of asking for a board, or of sending a score to one.</summary>
    public readonly struct LeaderboardResult
    {
        public readonly LeaderboardStatus Status;
        public readonly IReadOnlyList<LeaderboardEntry> Entries;

        /// <summary>Where the player came, counting from one, or 0 if they are not on this board.</summary>
        public readonly int YourRank;

        /// <summary>How many entries the board has, which is what "12th of 340" needs.</summary>
        public readonly int Total;

        /// <summary>Why it failed, for the log. Never shown to the player as it is.</summary>
        public readonly string Problem;

        public LeaderboardResult(LeaderboardStatus status, IReadOnlyList<LeaderboardEntry> entries = null,
            int yourRank = 0, int total = 0, string problem = null)
        {
            Status = status;
            Entries = entries ?? Array.Empty<LeaderboardEntry>();
            YourRank = yourRank;
            Total = total;
            Problem = problem;
        }

        public static LeaderboardResult Failure(string problem) =>
            new LeaderboardResult(LeaderboardStatus.Failed, problem: problem);
    }

    /// <summary>
    /// Somewhere to send a score and read a board back (GDD "Technical Design": behind an interface so the
    /// Unity Gaming Services implementation can be swapped or mocked in tests).
    /// </summary>
    /// <remarks>
    /// Callback-based rather than returning a task, because every caller is a MonoBehaviour that wants the
    /// answer on the main thread and may have been destroyed before it arrives. An implementation must call
    /// the callback exactly once, on the main thread, and must never throw at the caller: a leaderboard is
    /// not allowed to interrupt a game, so a failure is a <see cref="LeaderboardStatus.Failed"/> result.
    /// </remarks>
    public interface ILeaderboardService
    {
        /// <summary>True once scores can actually be sent. Until then, submissions are dropped.</summary>
        bool IsReady { get; }

        /// <summary>Signs in and makes the service usable. Safe to call more than once.</summary>
        void Prepare(Action<bool> ready = null);

        /// <summary>
        /// Sends a finished run's score to the board for its mode and difficulty.
        /// </summary>
        void Submit(GameMode mode, Difficulty difficulty, string playerName, long score,
            Action<LeaderboardResult> done = null);

        /// <summary>Reads the top of a board, and where the player sits on it.</summary>
        void Top(GameMode mode, Difficulty difficulty, int count, Action<LeaderboardResult> done);
    }
}
