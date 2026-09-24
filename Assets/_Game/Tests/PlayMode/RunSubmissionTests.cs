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

            /// <summary>Starts signed out, as the real service does: a run has to sign in before it can be sent.</summary>
            public bool IsReady { get; private set; }

            public bool SignsIn = true;

            public void Prepare(Action<bool> ready = null)
            {
                if (SignsIn) IsReady = true;
                ready?.Invoke(IsReady);
            }

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
        FakeBoards _local;

        protected override void ConfigureRun()
        {
            _boards = new FakeBoards();
            _local = new FakeBoards();

            GameFlow.UseLeaderboards(_boards);

            // The local board too: every finished run is mirrored there whatever the network did, so without
            // a fake the suite files its own thousand-point runs in the saved scores of whoever runs it.
            GameFlow.UseLocalBoards(_local);

            RunContext.Configure(Difficulty.Ace, GameMode.Endless);
        }

        [TearDown]
        public void ForgetTheFakes()
        {
            GameFlow.UseLeaderboards(null);
            GameFlow.UseLocalBoards(null);
        }

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

            // The run is kept on this machine as well, which is what a player with no network is shown.
            Assert.That(_local.Sent, Has.Count.EqualTo(1), "the run was not mirrored locally");
            Assert.That(_local.Sent[0].Score, Is.EqualTo(sent.Score));
        }

        /// <summary>
        /// A player with no network finishes runs too. The panel has to appear and the buttons have to work;
        /// only the placing is missing (GDD "Scoring": a leaderboard never interrupts a game).
        /// </summary>
        [UnityTest]
        public IEnumerator ARun_ThatCannotSignIn_StillEndsProperly()
        {
            _boards.SignsIn = false;
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            Session.ReportKill(0, 1000);
            yield return EndTheRun();

            Assert.That(_boards.Sent, Is.Empty, "a run that could not sign in has nowhere to go");
            Assert.That(_local.Sent, Has.Count.EqualTo(1),
                "with no network the local board is the only record there is, so it must still be written");

            var router = UnityEngine.Object.FindAnyObjectByType<MenuRouter>();
            Assert.That(router.Current, Is.EqualTo(MenuScreen.GameOver), "the run still has to end on its panel");
        }

        [UnityTest]
        public IEnumerator ARunWorthNothing_IsNotSent()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            yield return EndTheRun();

            Assert.That(_boards.Sent, Is.Empty, "a board full of zeroes buries the runs that meant something");
            Assert.That(_local.Sent, Is.Empty, "nor is it worth keeping on this machine");
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
