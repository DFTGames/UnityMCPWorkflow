using System;
using System.Collections.Generic;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Leaderboards kept on this machine, through the same store as the settings. Used when the online
    /// service cannot be reached, and by the tests, which must not touch a real service.
    /// </summary>
    /// <remarks>
    /// A local board is not a substitute for the online one the GDD asks for: nobody else is on it. It exists
    /// so that a player with no network still sees their own best runs rather than an error, and so that the
    /// screens can be built and tested without a service behind them.
    /// </remarks>
    public sealed class LocalLeaderboards : ILeaderboardService
    {
        const string KeyPrefix = "yass.board.";

        /// <summary>How many runs are kept per board. Enough to fill a screen, not enough to be a database.</summary>
        public const int Keep = 20;

        readonly ISettingsStore _store;

        public LocalLeaderboards(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Always: there is nothing to sign in to.</summary>
        public bool IsReady => true;

        public void Prepare(Action<bool> ready = null) => ready?.Invoke(true);

        public void Submit(GameMode mode, Difficulty difficulty, string playerName, long score,
            Action<LeaderboardResult> done = null)
        {
            if (!Leaderboards.WorthSubmitting(score))
            {
                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded));
                return;
            }

            var id = Leaderboards.IdFor(mode, difficulty);
            var entries = Read(id);
            entries.Add(new Run(Leaderboards.CleanName(playerName), score, mine: true));

            Save(id, entries);

            // Zero, not the new run's place: on a keep-best board a player's rank is their best run's, which
            // Build works out from the entries it has just sorted.
            done?.Invoke(Build(entries, 0));
        }

        public void Top(GameMode mode, Difficulty difficulty, int count, Action<LeaderboardResult> done)
        {
            var entries = Read(Leaderboards.IdFor(mode, difficulty));
            done?.Invoke(Build(entries, 0, count));
        }

        LeaderboardResult Build(List<Run> runs, int yourRank, int count = Keep)
        {
            runs.Sort((a, b) => b.Score.CompareTo(a.Score));

            var entries = new List<LeaderboardEntry>();
            for (var i = 0; i < runs.Count && i < count; i++)
            {
                entries.Add(new LeaderboardEntry(i + 1, runs[i].Name, runs[i].Score, runs[i].Mine));
                if (runs[i].Mine && yourRank == 0) yourRank = i + 1;
            }

            return new LeaderboardResult(LeaderboardStatus.Succeeded, entries, yourRank, runs.Count);
        }

        List<Run> Read(string id)
        {
            var runs = new List<Run>();
            for (var i = 0; i < Keep; i++)
            {
                var score = (long)_store.GetFloat($"{KeyPrefix}{id}.{i}.score", 0f);
                if (score <= 0) continue;

                runs.Add(new Run(_store.GetString($"{KeyPrefix}{id}.{i}.name", Leaderboards.DefaultName),
                    score, _store.GetBool($"{KeyPrefix}{id}.{i}.mine", false)));
            }

            return runs;
        }

        void Save(string id, List<Run> runs)
        {
            runs.Sort((a, b) => b.Score.CompareTo(a.Score));

            for (var i = 0; i < Keep; i++)
            {
                var has = i < runs.Count;
                _store.SetFloat($"{KeyPrefix}{id}.{i}.score", has ? runs[i].Score : 0f);
                _store.SetString($"{KeyPrefix}{id}.{i}.name", has ? runs[i].Name : string.Empty);
                _store.SetBool($"{KeyPrefix}{id}.{i}.mine", has && runs[i].Mine);
            }

            _store.Save();
        }

        readonly struct Run
        {
            public readonly string Name;
            public readonly long Score;
            public readonly bool Mine;

            public Run(string name, long score, bool mine)
            {
                Name = name;
                Score = score;
                Mine = mine;
            }
        }
    }
}
