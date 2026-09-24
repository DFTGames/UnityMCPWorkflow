using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;
using YASS.Core;

// The service has a LeaderboardEntry of its own, and so do we: one is a row from the network, the other is a
// row on the screen. Aliased rather than renaming ours, which is the shape the rest of the game speaks.
using ServiceEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

namespace YASS.UI
{
    /// <summary>
    /// The online boards (GDD "Scoring": six Unity Gaming Services leaderboards, one per mode and difficulty).
    /// A score belongs to the player's account, which <see cref="IPlayerAccounts"/> owns: the boards are
    /// usable exactly while somebody is signed in.
    /// </summary>
    /// <remarks>
    /// Nothing here is allowed to interrupt a game. Every call is wrapped, every failure becomes a
    /// <see cref="LeaderboardStatus.Failed"/> result, and the caller decides what to say about it: a player
    /// on a train with no signal should still see their run's figures and be able to start another.
    ///
    /// The board ids come from <see cref="Leaderboards.IdFor"/> and must match the ones in the dashboard.
    /// </remarks>
    public sealed class UgsLeaderboards : ILeaderboardService
    {
        readonly IPlayerAccounts _accounts;
        readonly List<Action<bool>> _waiting = new List<Action<bool>>();

        bool _preparing;

        public UgsLeaderboards(IPlayerAccounts accounts = null)
        {
            _accounts = accounts ?? new UgsAccounts();
        }

        /// <summary>The boards are usable exactly while somebody is signed in.</summary>
        public bool IsReady => _accounts.IsSignedIn;

        /// <summary>
        /// Resumes whatever session this machine remembers, once. Callers that arrive while that is in
        /// flight are queued and answered with it, rather than told "not signed in" on the spot: opening the
        /// board screen starts it, and the player pressing a button during it must not be answered by the
        /// fact that the thing they are waiting for has not finished yet.
        /// </summary>
        /// <remarks>
        /// This never asks anybody to sign in. A player without an account simply has no online boards,
        /// which the screens say plainly; signing up is something they choose, on the account screen.
        /// </remarks>
        public void Prepare(Action<bool> ready = null)
        {
            if (IsReady)
            {
                ready?.Invoke(true);
                return;
            }

            if (ready != null) _waiting.Add(ready);
            if (_preparing) return;

            _preparing = true;
            _accounts.Resume(_ =>
            {
                _preparing = false;
                AnswerEveryoneWaiting();
            });
        }

        /// <summary>
        /// Tells everyone who asked. The list is emptied first, because answering one of them can start
        /// another request that arrives back here before this loop has finished.
        /// </summary>
        void AnswerEveryoneWaiting()
        {
            if (_waiting.Count == 0) return;

            var waiting = _waiting.ToArray();
            _waiting.Clear();

            foreach (var callback in waiting) callback(IsReady);
        }

        public async void Submit(GameMode mode, Difficulty difficulty, string playerName, long score,
            Action<LeaderboardResult> done = null)
        {
            if (!Leaderboards.WorthSubmitting(score))
            {
                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded));
                return;
            }

            if (!IsReady)
            {
                done?.Invoke(LeaderboardResult.Failure("not signed in"));
                return;
            }

            // Last gate before a score leaves the machine. A run in the editor is a practice run or a test,
            // and neither belongs on a board real players are ranked on; every one that lands there has to be
            // deleted by hand. Refused rather than assumed safe when the environment cannot be shown.
            if (!UgsSession.MayTalk(out var wrongEnvironment))
            {
                Debug.LogError($"{nameof(UgsLeaderboards)}: score not sent, {wrongEnvironment}.");
                done?.Invoke(LeaderboardResult.Failure("the boards are not reachable from here"));
                return;
            }

            await RenameIfNeeded(playerName);

            try
            {
                var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(
                    Leaderboards.IdFor(mode, difficulty), score);

                // The service counts ranks from zero; the player counts from one.
                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded, Array.Empty<LeaderboardEntry>(),
                    entry.Rank + 1));
            }
            catch (Exception problem)
            {
                Debug.LogWarning($"{nameof(UgsLeaderboards)}: score not sent. {problem.Message}");
                done?.Invoke(LeaderboardResult.Failure(problem.Message));
            }
        }

        /// <summary>
        /// Puts the player's chosen name on their account, if it is not there already.
        /// </summary>
        /// <remarks>
        /// In its own try, and awaited before the score rather than with it: a rename can be refused (the
        /// service rate-limits them) and a refusal must cost the player their new name, not their run. It
        /// used to share the submission's try, so one rejected rename threw the score away.
        ///
        /// Skipped when the name has not changed, which is every run after the first: the service appends
        /// its own disambiguating number, so the comparison is against the part before the '#'.
        /// </remarks>
        static async Task RenameIfNeeded(string playerName)
        {
            var wanted = Leaderboards.CleanName(playerName);
            try
            {
                var current = AuthenticationService.Instance.PlayerName;
                if (!string.IsNullOrEmpty(current) && WithoutTheNumber(current) == wanted) return;

                await AuthenticationService.Instance.UpdatePlayerNameAsync(wanted);
            }
            catch (Exception problem)
            {
                Debug.LogWarning($"{nameof(UgsLeaderboards)}: could not set the player name to '{wanted}', " +
                                 $"so the board will show the previous one. {problem.Message}");
            }
        }

        public async void Top(GameMode mode, Difficulty difficulty, int count, Action<LeaderboardResult> done)
        {
            if (!IsReady)
            {
                done?.Invoke(LeaderboardResult.Failure("not signed in"));
                return;
            }

            // Reading is harmless to the live boards, but a read from an environment nobody can name is how a
            // test ends up asserting against real players' scores and reporting a number it never sent.
            if (!UgsSession.MayTalk(out var wrongEnvironment))
            {
                Debug.LogError($"{nameof(UgsLeaderboards)}: board not read, {wrongEnvironment}.");
                done?.Invoke(LeaderboardResult.Failure("the boards are not reachable from here"));
                return;
            }

            try
            {
                var id = Leaderboards.IdFor(mode, difficulty);
                var page = await LeaderboardsService.Instance.GetScoresAsync(
                    id, new GetScoresOptions { Limit = count });

                // A list per call, not a shared buffer: two boards can be in flight at once (cycling the
                // mode does exactly that), and a result belongs to whoever asked for it.
                // Guarded against being empty: an entry whose player id is also empty would otherwise be
                // marked as the player's own, and the screen would highlight a stranger's run as theirs.
                var me = AuthenticationService.Instance.PlayerId;
                var knowWhoIAm = !string.IsNullOrEmpty(me);
                var entries = new List<LeaderboardEntry>(page.Results.Count);
                foreach (var score in page.Results)
                    entries.Add(new LeaderboardEntry(score.Rank + 1, NameOf(score), (long)score.Score,
                        knowWhoIAm && score.PlayerId == me));

                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded, entries, RankOfMine(entries),
                    page.Total));
            }
            catch (Exception problem)
            {
                Debug.LogWarning($"{nameof(UgsLeaderboards)}: could not read the board. {problem.Message}");
                done?.Invoke(LeaderboardResult.Failure(problem.Message));
            }
        }

        static int RankOfMine(List<LeaderboardEntry> entries)
        {
            foreach (var entry in entries)
                if (entry.IsYou) return entry.Rank;

            return 0;
        }

        /// <summary>
        /// Unity Authentication appends a number to a player name to keep it unique ("Ace#1234"). The board
        /// shows the name the player typed; the number is the service's business, not theirs.
        /// </summary>
        static string NameOf(ServiceEntry score) =>
            string.IsNullOrEmpty(score.PlayerName) ? Leaderboards.DefaultName : WithoutTheNumber(score.PlayerName);

        /// <summary>Drops the "#1234" the service appends to keep player names unique.</summary>
        static string WithoutTheNumber(string name)
        {
            var hash = name.IndexOf('#');
            return hash > 0 ? name.Substring(0, hash) : name;
        }
    }
}
