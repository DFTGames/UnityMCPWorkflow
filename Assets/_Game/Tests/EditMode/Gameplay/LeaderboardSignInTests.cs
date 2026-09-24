using System;
using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Nothing reaches an online board until the player has been signed in, and a service that is never asked
    /// to sign in reports itself unready for ever: every submission then fails and every board falls back to
    /// the local one, silently and identically to having no network (GDD "Scoring", Leaderboards).
    /// </summary>
    public class LeaderboardSignInTests
    {
        /// <summary>
        /// Signs in only when the test says so, the way a real service does. Deliberately not instantaneous:
        /// a fake that answers on the spot cannot tell a service that queues its callers from one that turns
        /// them away, which is the difference this whole path exists to get right.
        /// </summary>
        sealed class FakeBoards : ILeaderboardService
        {
            readonly List<Action<bool>> _waiting = new List<Action<bool>>();

            public int Prepares;
            public bool SignsIn = true;

            public bool IsReady { get; private set; }

            /// <summary>How many callers are still waiting for an answer.</summary>
            public int Waiting => _waiting.Count;

            public void Prepare(Action<bool> ready = null)
            {
                Prepares++;
                if (ready != null) _waiting.Add(ready);
            }

            /// <summary>The round trip comes back, as it would a second or so later.</summary>
            public void SignInAnswers()
            {
                IsReady = SignsIn;

                var waiting = _waiting.ToArray();
                _waiting.Clear();
                foreach (var callback in waiting) callback(IsReady);
            }

            public void Submit(GameMode mode, Difficulty difficulty, string playerName, long score,
                Action<LeaderboardResult> done = null) =>
                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded));

            public void Top(GameMode mode, Difficulty difficulty, int count, Action<LeaderboardResult> done) =>
                done?.Invoke(new LeaderboardResult(LeaderboardStatus.Succeeded));
        }

        FakeBoards _boards;

        [SetUp]
        public void UseAFakeService()
        {
            _boards = new FakeBoards();
            GameFlow.UseLeaderboards(_boards);
        }

        [TearDown]
        public void ForgetTheFake() => GameFlow.UseLeaderboards(null);

        [Test]
        public void TheFirstThingThatNeedsABoard_SignsThePlayerIn()
        {
            bool? answer = null;
            GameFlow.WhenReady(ready => answer = ready);

            Assert.That(_boards.Prepares, Is.EqualTo(1), "nothing signed in, so the boards stay out of reach");
            Assert.That(answer, Is.Null, "the answer cannot be known before the round trip comes back");

            _boards.SignInAnswers();
            Assert.That(answer, Is.True, "the caller was never told the sign-in had worked");
        }

        /// <summary>
        /// The case that made the online boards look unreachable on a working connection: opening the board
        /// screen starts a sign-in, and pressing a button during it used to be answered "not signed in" on
        /// the spot, because a sign-in was already in flight. Everyone waiting gets the real answer.
        /// </summary>
        [Test]
        public void EveryoneWhoAsksDuringASignIn_IsAnsweredByIt()
        {
            var answers = new List<bool>();
            GameFlow.WhenReady(answers.Add);
            GameFlow.WhenReady(answers.Add);
            GameFlow.WhenReady(answers.Add);

            Assert.That(_boards.Prepares, Is.EqualTo(3), "each of them asked");
            Assert.That(_boards.Waiting, Is.EqualTo(3), "and none of them was turned away");
            Assert.That(answers, Is.Empty, "nobody can be answered yet");

            _boards.SignInAnswers();

            Assert.That(answers, Is.EqualTo(new[] { true, true, true }),
                "a caller that arrived mid sign-in was told the boards could not be reached");
        }

        [Test]
        public void SigningIn_IsNotRepeated_OnceItHasWorked()
        {
            GameFlow.WhenReady(_ => { });
            _boards.SignInAnswers();

            var answer = false;
            GameFlow.WhenReady(ready => answer = ready);

            Assert.That(_boards.Prepares, Is.EqualTo(1), "already signed in; asking again is a wasted round trip");
            Assert.That(answer, Is.True, "an already-signed-in service answers at once");
        }

        /// <summary>
        /// No network is an ordinary condition, not a fault: the caller is told no and carries on, rather than
        /// being left without an answer.
        /// </summary>
        [Test]
        public void APlayerWhoCannotSignIn_IsToldSo()
        {
            _boards.SignsIn = false;

            var answers = 0;
            GameFlow.WhenReady(ready =>
            {
                answers++;
                Assert.That(ready, Is.False);
            });

            _boards.SignInAnswers();
            Assert.That(answers, Is.EqualTo(1));
        }
    }
}
