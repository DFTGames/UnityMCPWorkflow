using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// A finished run reaches the board it belongs to (GDD "Core Loop": play a run until victory or game
    /// over, submit the score). Against a fake service, so what is proved is the game's half of the bargain
    /// and no test ever writes to a real board.
    /// </summary>
    public class RunSubmissionTests : LevelSceneFixture
    {
        /// <summary>Remembers what it was asked to send, and answers whenever the test tells it to.</summary>
        sealed class FakeBoards : ILeaderboardService
        {
            public readonly List<(GameMode Mode, Difficulty Difficulty, string Name, long Score)> Sent =
                new List<(GameMode, Difficulty, string, long)>();

            Action<LeaderboardResult> _waiting;

            public bool IsReady { get; set; } = true;

            public void Prepare(Action<bool> ready = null) => ready?.Invoke(IsReady);

            public void Submit(GameMode mode, Difficulty difficulty, string playerName, long score,
                Action<LeaderboardResult> done = null)
            {
                Sent.Add((mode, difficulty, playerName, score));
                _waiting = done;
            }

            public void Top(GameMode mode, Difficulty difficulty, int count, Action<LeaderboardResult> done) =>
                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded));

            /// <summary>Answers the submission that is waiting, as the service would once the network replied.</summary>
            public void Answer(int rank) =>
                _waiting?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded, null, rank, 100));
        }

        FakeBoards _boards;

        protected override void ConfigureRun()
        {
            _boards = new FakeBoards();
            GameFlow.UseLeaderboards(_boards);
            RunContext.Configure(Difficulty.Ace, GameMode.Endless);
        }

        [TearDown]
        public void ForgetTheFake() => GameFlow.UseLeaderboards(null);

        [UnityTest]
        public IEnumerator AFinishedRun_GoesToItsOwnBoard()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            GameFlow.Settings.SetPlayerName("Ace");

            // Score something, then end the run: a score of nothing is deliberately never sent.
            Session.ReportKill(0, 1000);
            yield return EndTheRun();

            Assert.That(_boards.Sent, Has.Count.EqualTo(1), "a finished run submits exactly once");

            var sent = _boards.Sent[0];
            Assert.That(sent.Mode, Is.EqualTo(GameMode.Endless), "an Endless run belongs on the Endless board");
            Assert.That(sent.Difficulty, Is.EqualTo(Difficulty.Ace), "and on the one for its difficulty");
            Assert.That(sent.Name, Is.EqualTo("Ace"));
            Assert.That(sent.Score, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ARunWorthNothing_IsNotSent()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            yield return EndTheRun();

            Assert.That(_boards.Sent, Is.Empty, "a board full of zeroes buries the runs that meant something");
        }

        /// <summary>Takes every life off the player, which is what ends a run.</summary>
        IEnumerator EndTheRun()
        {
            while (!Ship.IsGameOver)
            {
                Session.ReportPlayerHit(0, 1000f);
                yield return new UnityEngine.WaitForSeconds(GameTuning.RespawnInvulnerabilitySeconds + 0.1f);
            }

            // The flow shows its panel once the run has ended, and that is where the score is submitted.
            yield return WaitUntil(() => _boards.Sent.Count > 0 || Runner.SecondsSinceRunEnded > 2f,
                8f, "the run to be reported");
        }
    }
}
