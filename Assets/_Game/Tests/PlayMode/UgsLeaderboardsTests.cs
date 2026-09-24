using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The online boards, against the real service (GDD "Scoring", Leaderboards). Everything else about
    /// leaderboards is tested locally; this is the only thing that proves the game can actually talk to Unity
    /// Gaming Services, which no amount of mocking can.
    /// </summary>
    /// <remarks>
    /// **Runs against the 'test' environment, never production.** Its boards are the same six ids but set to
    /// keepLatest, so a test run overwrites its own previous score instead of accumulating, and nothing a test
    /// does can appear on a board a player sees.
    ///
    /// Credentials come from <c>.ugs/test-user.txt</c>, which is gitignored: nothing secret is committed, and
    /// the test reports itself as ignored rather than failing when the file is absent or the service cannot be
    /// reached. A leaderboard being down must never look like a broken game.
    ///
    /// Deliberately not under <c>Temp/</c>, where it used to live: Unity empties that folder, and when it did,
    /// these two tests quietly began ignoring themselves. A suite that reports a skip is honest, but the online
    /// path then goes unverified run after run without anybody noticing.
    /// </remarks>
    public class UgsLeaderboardsTests
    {
        const string Environment = "test";
        const string CredentialsFile = ".ugs/test-user.txt";

        /// <summary>Long enough for a round trip on a slow connection, short enough not to hang a suite.</summary>
        const float Timeout = 20f;

        static string _user;
        static string _password;

        /// <summary>
        /// Whether this fixture is the one that started Unity Services in this play session, which is what
        /// makes the environment and the signed-in player knowable.
        /// </summary>
        static bool _weStartedTheServices;

        [OneTimeSetUp]
        public void ReadCredentials()
        {
            // Domain reloading is off, so this survives the previous play session: it only means anything
            // while the services it refers to are still running.
            if (UnityServices.State == ServicesInitializationState.Uninitialized) _weStartedTheServices = false;

            if (!File.Exists(CredentialsFile)) return;

            var lines = File.ReadAllLines(CredentialsFile);
            if (lines.Length < 2) return;

            _user = lines[0].Trim();
            _password = lines[1].Trim();
        }

        [UnitySetUp]
        public IEnumerator SignIn()
        {
            if (string.IsNullOrEmpty(_user))
                Assert.Ignore($"No {CredentialsFile}, so the online boards are not exercised and nothing " +
                              "here proves the game can reach the service. Write a username on the first " +
                              "line and a password on the second to enable it; the account is created on " +
                              $"first use, in the '{Environment}' environment.");

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                options.SetEnvironmentName(Environment);
                yield return Wait(UnityServices.InitializeAsync(), "the services to start");
                _weStartedTheServices = true;
            }

            // The services are a process-wide singleton: whoever starts them first chooses the environment for
            // the whole play session, and whoever signs in first chooses the player. Unless that was this
            // fixture, it would be testing some other environment as some other player and reporting whatever
            // it found there. That is how a gameplay test which submitted a score once made this one read back
            // a number it had never sent.
            Assert.That(_weStartedTheServices, Is.True,
                $"something started Unity Services before this fixture, so it cannot be sure it is on the " +
                $"'{Environment}' environment as the test user. A test that plays a run submits a score like " +
                "any other run: give that fixture boards that go nowhere (GameFlow.UseLeaderboards).");

            if (!AuthenticationService.Instance.IsSignedIn) yield return SignInAsTheTestUser();
            if (!AuthenticationService.Instance.IsSignedIn) Assert.Ignore("Could not sign in; is there a network?");
        }

        /// <summary>Signs the test user in, creating the account the first time this ever runs.</summary>
        static IEnumerator SignInAsTheTestUser()
        {
            var signIn = AuthenticationService.Instance.SignInWithUsernamePasswordAsync(_user, _password);
            while (!signIn.IsCompleted) yield return null;

            if (!signIn.IsFaulted) yield break;

            // No such account yet: make it. Any other failure is reported by the caller's IsSignedIn check.
            var signUp = AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(_user, _password);
            while (!signUp.IsCompleted) yield return null;

            if (signUp.IsFaulted) Debug.LogWarning($"Could not sign up the test user: {signUp.Exception?.Message}");
        }

        static IEnumerator Wait(System.Threading.Tasks.Task task, string what)
        {
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Ignore($"Timed out waiting for {what}.");
                yield return null;
            }
        }

        static IEnumerator Until(System.Func<bool> done, string what)
        {
            var deadline = Time.realtimeSinceStartup + Timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {what}.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AScore_GoesUpAndComesBack()
        {
            var board = new UgsLeaderboards();
            var prepared = false;
            board.Prepare(_ => prepared = true);
            yield return Until(() => prepared, "the board to be ready");

            Assert.That(board.IsReady, Is.True, "signed in, so the board should be usable");

            // A number that goes up every run, so reading it back proves this run's write arrived rather than
            // an earlier one's. Deliberately increasing rather than merely different: a board may keep the
            // best score instead of the latest, and this test is about whether the game can talk to the
            // service at all, not about which policy the board was configured with. A number that only
            // changed left it reading back a score from a previous run, and no amount of resetting the board
            // fixed that, because the next run's number was lower again.
            var score = (long)(System.DateTime.UtcNow - new System.DateTime(2026, 1, 1)).TotalSeconds;
            var name = "TestRunner";

            LeaderboardResult submitted = default;
            board.Submit(GameMode.Campaign, Difficulty.Pilot, name, score, r => submitted = r);
            yield return Until(() => submitted.Status != LeaderboardStatus.Working, "the score to be sent");

            Assert.That(submitted.Status, Is.EqualTo(LeaderboardStatus.Succeeded), submitted.Problem);
            Assert.That(submitted.YourRank, Is.GreaterThan(0), "a submitted score has a place on the board");

            LeaderboardResult read = default;
            board.Top(GameMode.Campaign, Difficulty.Pilot, 25, r => read = r);
            yield return Until(() => read.Status != LeaderboardStatus.Working, "the board to come back");

            Assert.That(read.Status, Is.EqualTo(LeaderboardStatus.Succeeded), read.Problem);

            var mine = default(LeaderboardEntry);
            var found = false;
            var mineCount = 0;
            foreach (var entry in read.Entries)
                if (entry.IsYou) { mine = entry; found = true; mineCount++; }

            // The board itself, when something does not add up. Every failure here so far has been about
            // which board, which environment or which player was actually being read, and none of that is
            // visible from "expected 4350 but was 17550".
            var what = new System.Text.StringBuilder();
            what.Append("board=").Append(Leaderboards.IdFor(GameMode.Campaign, Difficulty.Pilot))
                .Append(" env=").Append(Environment)
                .Append(" player=").Append(AuthenticationService.Instance.PlayerId)
                .Append(" sent=").Append(score)
                .Append(" rankFromSubmit=").Append(submitted.YourRank)
                .Append(" total=").Append(read.Total)
                .Append(" minePresent=").Append(mineCount)
                .AppendLine();

            foreach (var entry in read.Entries)
                what.Append("  #").Append(entry.Rank).Append(' ').Append(entry.Name)
                    .Append(' ').Append(entry.Score).Append(entry.IsYou ? "  <- me" : string.Empty)
                    .AppendLine();

            Assert.That(found, Is.True, "the run that was just submitted is not on the board. " + what);
            Assert.That(mineCount, Is.EqualTo(1), "more than one entry claims to be the player's. " + what);
            Assert.That(mine.Score, Is.EqualTo(score), "the score came back as something else. " + what);
            Assert.That(mine.Name, Is.EqualTo(name), "the name on the board is not the one submitted. " + what);
            Assert.That(mine.Name, Does.Not.Contain("#"), "the service's unique suffix should not be shown");
        }

        [UnityTest]
        public IEnumerator EachModeAndDifficulty_IsADifferentBoard()
        {
            // Six boards, and a run on one must not turn up on another: the whole point of keeping them apart.
            var board = new UgsLeaderboards();
            var prepared = false;
            board.Prepare(_ => prepared = true);
            yield return Until(() => prepared, "the board to be ready");

            var score = 500 + System.DateTime.UtcNow.Second;
            LeaderboardResult submitted = default;
            board.Submit(GameMode.Endless, Difficulty.Ace, "TestRunner", score, r => submitted = r);
            yield return Until(() => submitted.Status != LeaderboardStatus.Working, "the score to be sent");
            Assert.That(submitted.Status, Is.EqualTo(LeaderboardStatus.Succeeded), submitted.Problem);

            LeaderboardResult other = default;
            board.Top(GameMode.Campaign, Difficulty.Cadet, 25, r => other = r);
            yield return Until(() => other.Status != LeaderboardStatus.Working, "the other board to come back");

            foreach (var entry in other.Entries)
                Assert.That(entry.Score, Is.Not.EqualTo(score),
                    "an Endless Ace run turned up on the Campaign Cadet board");
        }
    }
}
