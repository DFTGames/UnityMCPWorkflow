using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The board kept on this machine: what a player with no network sees, and what the screens are built
    /// against. It is the same <see cref="ILeaderboardService"/> the online one implements, so what is proved
    /// here is the contract both sides of that interface have to keep.
    /// </summary>
    public class LocalLeaderboardsTests
    {
        sealed class Store : ISettingsStore
        {
            readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();
            readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

            public float GetFloat(string key, float fallback) => _floats.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) => _floats[key] = value;
            public bool GetBool(string key, bool fallback) => _bools.TryGetValue(key, out var v) ? v : fallback;
            public void SetBool(string key, bool value) => _bools[key] = value;
            public string GetString(string key, string fallback) => _strings.TryGetValue(key, out var v) ? v : fallback;
            public void SetString(string key, string value) => _strings[key] = value;
            public void Save() { }
        }

        static LocalLeaderboards Board(out Store store)
        {
            store = new Store();
            return new LocalLeaderboards(store);
        }

        static LeaderboardResult Read(ILeaderboardService board,
            GameMode mode = GameMode.Campaign, Difficulty difficulty = Difficulty.Pilot, int count = 10)
        {
            LeaderboardResult read = default;
            board.Top(mode, difficulty, count, r => read = r);
            return read;
        }

        [Test]
        public void ARun_GoesOntoItsOwnBoard()
        {
            var board = Board(out _);

            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 1000);

            Assert.That(Read(board).Entries, Has.Count.EqualTo(1));
            Assert.That(Read(board, GameMode.Endless).Entries, Is.Empty, "Endless has its own board");
            Assert.That(Read(board, difficulty: Difficulty.Ace).Entries, Is.Empty, "and so does each difficulty");
        }

        [Test]
        public void TheBoard_IsSortedHighestFirst()
        {
            var board = Board(out _);

            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Low", 100);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "High", 5000);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Middle", 900);

            var entries = Read(board).Entries;
            Assert.That(entries[0].Score, Is.EqualTo(5000));
            Assert.That(entries[1].Score, Is.EqualTo(900));
            Assert.That(entries[2].Score, Is.EqualTo(100));
            Assert.That(entries[0].Rank, Is.EqualTo(1), "ranks are what the player reads, counting from one");
        }

        [Test]
        public void ARunWorthNothing_IsNotKept()
        {
            var board = Board(out _);

            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 0);

            Assert.That(Read(board).Entries, Is.Empty);
        }

        [Test]
        public void SubmittingReports_WhereTheRunCame()
        {
            var board = Board(out _);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Someone", 5000);

            LeaderboardResult result = default;
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 900, r => result = r);

            Assert.That(result.Status, Is.EqualTo(LeaderboardStatus.Succeeded));
            Assert.That(result.Total, Is.EqualTo(2));
            Assert.That(result.YourRank, Is.GreaterThan(0), "a run that was kept has a place on the board");
        }

        [Test]
        public void YourBestRun_IsTheOneYouAreRankedBy()
        {
            // The online boards keep the best score per player, so the local one has to agree: a bad run
            // after a good one must not push the player down the board.
            var board = Board(out _);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 5000);

            LeaderboardResult result = default;
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 10, r => result = r);

            Assert.That(result.YourRank, Is.EqualTo(1), "the earlier, better run still stands");
        }

        /// <summary>
        /// An Endless run's score multiplier is uncapped (GDD "Core Loop"), so eight-figure scores are
        /// reachable. They were stored as floats, which have 24 bits of mantissa, so the board showed a
        /// number a few points away from the one the results screen had shown a second earlier.
        /// </summary>
        [Test]
        public void ABigScore_ComesBackExactly()
        {
            const long score = 20_481_377L; // not representable as a float
            var board = Board(out _);

            LeaderboardResult result = default;
            board.Submit(GameMode.Endless, Difficulty.Ace, "Ace", score);
            board.Top(GameMode.Endless, Difficulty.Ace, 10, r => result = r);

            Assert.That(result.Entries[0].Score, Is.EqualTo(score));
        }

        /// <summary>
        /// Every row on this board is the player's, so marking them all as theirs paints the whole screen in
        /// the colour that exists to pick their run out of everyone else's. One row is marked: their best,
        /// which is the row the online board would show them as.
        /// </summary>
        [Test]
        public void OnlyYourBestRun_IsMarkedAsYours()
        {
            var board = Board(out _);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 500);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 9000);
            board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 3000);

            LeaderboardResult result = default;
            board.Top(GameMode.Campaign, Difficulty.Pilot, 10, r => result = r);

            Assert.That(result.Entries, Has.Count.EqualTo(3), "the history is still kept");

            var mine = 0;
            foreach (var entry in result.Entries)
                if (entry.IsYou) mine++;

            Assert.That(mine, Is.EqualTo(1), "only one row is the player's own");
            Assert.That(result.Entries[0].IsYou, Is.True, "and it is their best run");
            Assert.That(result.YourRank, Is.EqualTo(1));
        }

        [Test]
        public void TheBoard_SurvivesTheSession()
        {
            var first = Board(out var store);
            first.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", 1234);

            var later = new LocalLeaderboards(store);

            var entries = Read(later).Entries;
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Name, Is.EqualTo("Ace"));
            Assert.That(entries[0].Score, Is.EqualTo(1234));
        }

        [Test]
        public void TheBoard_DoesNotGrowForEver()
        {
            var board = Board(out _);
            for (var i = 1; i <= LocalLeaderboards.Keep + 15; i++)
                board.Submit(GameMode.Campaign, Difficulty.Pilot, "Ace", i);

            var result = Read(board, count: 100);

            Assert.That(result.Entries, Has.Count.LessThanOrEqualTo(LocalLeaderboards.Keep));
            Assert.That(result.Entries[0].Score, Is.EqualTo(LocalLeaderboards.Keep + 15),
                "and it is the worst runs that fall off, not the best");
        }

        [Test]
        public void ANamelessRun_IsStillReadable()
        {
            var board = Board(out _);

            board.Submit(GameMode.Campaign, Difficulty.Pilot, "   ", 400);

            Assert.That(Read(board).Entries[0].Name, Is.EqualTo(Leaderboards.DefaultName));
        }

        [Test]
        public void TheLocalBoard_IsAlwaysReady()
        {
            // There is nothing to sign in to, so the game never waits on it and never fails against it.
            var board = Board(out _);
            var ready = false;

            board.Prepare(r => ready = r);

            Assert.That(board.IsReady, Is.True);
            Assert.That(ready, Is.True);
        }
    }
}
