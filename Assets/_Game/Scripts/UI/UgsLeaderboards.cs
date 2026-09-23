using System;
using System.Collections.Generic;
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
    /// Signs in anonymously, because a score board is not worth making somebody hold an account for.
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
        readonly List<LeaderboardEntry> _entries = new List<LeaderboardEntry>();

        bool _preparing;

        public bool IsReady { get; private set; }

        public async void Prepare(Action<bool> ready = null)
        {
            if (IsReady || _preparing)
            {
                ready?.Invoke(IsReady);
                return;
            }

            _preparing = true;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsReady = AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception problem)
            {
                // A warning, not an error: no network is an ordinary condition, not a fault in the game.
                Debug.LogWarning($"{nameof(UgsLeaderboards)}: could not sign in, so scores stay on this " +
                                 $"machine. {problem.Message}");
                IsReady = false;
            }
            finally
            {
                _preparing = false;
                ready?.Invoke(IsReady);
            }
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

            try
            {
                // The name is the player's own, so it goes up with every submission: changing it in Settings
                // should change what the board shows, not leave the old one there for ever.
                await AuthenticationService.Instance.UpdatePlayerNameAsync(Leaderboards.CleanName(playerName));

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

        public async void Top(GameMode mode, Difficulty difficulty, int count, Action<LeaderboardResult> done)
        {
            if (!IsReady)
            {
                done?.Invoke(LeaderboardResult.Failure("not signed in"));
                return;
            }

            try
            {
                var id = Leaderboards.IdFor(mode, difficulty);
                var page = await LeaderboardsService.Instance.GetScoresAsync(
                    id, new GetScoresOptions { Limit = count });

                var me = AuthenticationService.Instance.PlayerId;
                _entries.Clear();
                foreach (var score in page.Results)
                    _entries.Add(new LeaderboardEntry(score.Rank + 1, NameOf(score), (long)score.Score,
                        score.PlayerId == me));

                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded, _entries, RankOfMine(), page.Total));
            }
            catch (Exception problem)
            {
                Debug.LogWarning($"{nameof(UgsLeaderboards)}: could not read the board. {problem.Message}");
                done?.Invoke(LeaderboardResult.Failure(problem.Message));
            }
        }

        int RankOfMine()
        {
            foreach (var entry in _entries)
                if (entry.IsYou) return entry.Rank;

            return 0;
        }

        /// <summary>
        /// Unity Authentication appends a number to a player name to keep it unique ("Ace#1234"). The board
        /// shows the name the player typed; the number is the service's business, not theirs.
        /// </summary>
        static string NameOf(ServiceEntry score)
        {
            var name = score.PlayerName;
            if (string.IsNullOrEmpty(name)) return Leaderboards.DefaultName;

            var hash = name.IndexOf('#');
            return hash > 0 ? name.Substring(0, hash) : name;
        }
    }
}
