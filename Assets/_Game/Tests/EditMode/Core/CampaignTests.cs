using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class CampaignRunTests
    {
        static GameSession Session(Difficulty difficulty = Difficulty.Pilot) =>
            new GameSession(DifficultySettings.For(difficulty), new TestRandom(0.99f));

        [Test]
        public void ANewRun_StartsOnTheFirstLevelWithNothingBanked()
        {
            var run = new CampaignRun(Difficulty.Pilot, 8);

            Assert.That(run.LevelIndex, Is.Zero);
            Assert.That(run.LevelNumber, Is.EqualTo(1));
            Assert.That(run.CarriedScore, Is.Zero);
            Assert.That(run.IsComplete, Is.False);
            Assert.That(run.CarryFor(0), Is.Null, "there is nothing to carry into the first level");
        }

        [Test]
        public void ClearingALevel_BanksItsScoreAndMovesOn()
        {
            var run = new CampaignRun(Difficulty.Pilot, 8);
            var session = Session();
            session.Score.RegisterKill(100);
            session.Score.RegisterBonus(2000);

            run.CompleteLevel(session);

            Assert.That(run.LevelNumber, Is.EqualTo(2));
            Assert.That(run.CarriedScore, Is.EqualTo(session.Score.Score));
            Assert.That(run.CarriedKills, Is.EqualTo(1));
            Assert.That(run.IsComplete, Is.False);
        }

        [Test]
        public void TheRunsScore_IsEveryLevelAddedTogether()
        {
            var run = new CampaignRun(Difficulty.Pilot, 8);

            var first = Session();
            first.Score.RegisterKill(100);
            run.CompleteLevel(first);

            var second = Session();
            second.Score.RegisterKill(100);

            Assert.That(run.TotalScore(second), Is.EqualTo(first.Score.Score + second.Score.Score));
            Assert.That(run.TotalKills(second), Is.EqualTo(2));
        }

        [Test]
        public void TheBestChain_IsTheBestOfTheWholeRun()
        {
            var run = new CampaignRun(Difficulty.Pilot, 8);

            var first = Session();
            for (var i = 0; i < 5; i++) first.Score.RegisterKill(10); // a chain of four
            run.CompleteLevel(first);
            Assert.That(run.BestChainSteps, Is.EqualTo(4));

            var second = Session();
            second.Score.RegisterKill(10); // no chain at all
            run.CompleteLevel(second);

            Assert.That(run.BestChainSteps, Is.EqualTo(4), "a later, worse level does not lower it");
        }

        [Test]
        public void APlayerCarriesTheirShipIntoTheNextLevel()
        {
            var run = new CampaignRun(Difficulty.Pilot, 8);
            var session = Session();
            session.ReportPlayerHit(0, 30f);                       // damaged
            session.ReportPickupCollected(0, PickupType.WeaponUpgrade);

            run.CompleteLevel(session);
            var carry = run.CarryFor(0);

            Assert.That(carry, Is.Not.Null);
            Assert.That(carry.Value.Lives, Is.EqualTo(session.GetPlayer(0).Vitals.Lives));
            Assert.That(carry.Value.Health, Is.EqualTo(70f).Within(1e-3f), "a hurt ship stays hurt");
            Assert.That(carry.Value.WeaponLevel, Is.EqualTo(2), "and keeps its upgrade");
        }

        [Test]
        public void TheLastLevel_FinishesTheCampaign()
        {
            var run = new CampaignRun(Difficulty.Ace, 2);

            Assert.That(run.IsFinalLevel, Is.False);
            run.CompleteLevel(Session(Difficulty.Ace));

            Assert.That(run.IsFinalLevel, Is.True, "now on the last level");
            run.CompleteLevel(Session(Difficulty.Ace));

            Assert.That(run.IsComplete, Is.True);
        }

        [Test]
        public void ClearingPastTheEnd_Throws()
        {
            var run = new CampaignRun(Difficulty.Pilot, 1);
            run.CompleteLevel(Session());

            Assert.Throws<InvalidOperationException>(() => run.CompleteLevel(Session()));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignRun(Difficulty.Pilot, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignRun(Difficulty.Pilot, 8, 0));
            Assert.Throws<ArgumentNullException>(() => new CampaignRun(Difficulty.Pilot, 8).CompleteLevel(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignRun(Difficulty.Pilot, 8).CarryFor(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerCarry(-1, 100f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerCarry(3, 100f, 0));
        }
    }

    public class CampaignProgressTests
    {
        static CampaignProgress Create(out FakeSettingsStore store)
        {
            store = new FakeSettingsStore();
            return new CampaignProgress(store);
        }

        [Test]
        public void NothingIsClearedToBeginWith()
        {
            var progress = Create(out _);

            Assert.That(progress.LevelsCleared(Difficulty.Pilot), Is.Zero);
            Assert.That(progress.IsCampaignComplete(Difficulty.Pilot), Is.False);
            Assert.That(progress.IsEndlessUnlocked, Is.False);
        }

        [Test]
        public void ClearedLevelsAreRemembered()
        {
            var progress = Create(out var store);

            progress.RecordLevelCleared(Difficulty.Pilot, 3, 8);

            Assert.That(progress.LevelsCleared(Difficulty.Pilot), Is.EqualTo(3));
            Assert.That(new CampaignProgress(store).LevelsCleared(Difficulty.Pilot), Is.EqualTo(3),
                "and survive into the next session");
        }

        [Test]
        public void ProgressOnlyMovesForward()
        {
            var progress = Create(out _);
            progress.RecordLevelCleared(Difficulty.Pilot, 5, 8);

            progress.RecordLevelCleared(Difficulty.Pilot, 1, 8); // replaying an early level

            Assert.That(progress.LevelsCleared(Difficulty.Pilot), Is.EqualTo(5));
        }

        [Test]
        public void EachDifficultyIsTrackedSeparately()
        {
            var progress = Create(out _);

            progress.RecordLevelCleared(Difficulty.Cadet, 8, 8);

            Assert.That(progress.IsCampaignComplete(Difficulty.Cadet), Is.True);
            Assert.That(progress.IsCampaignComplete(Difficulty.Ace), Is.False,
                "finishing on Cadet does not claim the Ace campaign");
        }

        [Test]
        public void FinishingTheCampaign_UnlocksEndless()
        {
            var progress = Create(out _);
            progress.RecordLevelCleared(Difficulty.Pilot, 7, 8);
            Assert.That(progress.IsEndlessUnlocked, Is.False, "seven of eight is not finished");

            progress.RecordLevelCleared(Difficulty.Pilot, 8, 8);

            Assert.That(progress.IsCampaignComplete(Difficulty.Pilot), Is.True);
            Assert.That(progress.IsEndlessUnlocked, Is.True);
        }

        [Test]
        public void AOneLevelCampaign_IsFinishedByClearingIt()
        {
            var progress = Create(out _);

            progress.RecordLevelCleared(Difficulty.Pilot, 1, 1);

            Assert.That(progress.IsEndlessUnlocked, Is.True);
        }

        [Test]
        public void Reset_ForgetsEverything()
        {
            var progress = Create(out _);
            progress.RecordLevelCleared(Difficulty.Ace, 8, 8);

            progress.Reset();

            Assert.That(progress.LevelsCleared(Difficulty.Ace), Is.Zero);
            Assert.That(progress.IsEndlessUnlocked, Is.False);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var progress = Create(out _);

            Assert.Throws<ArgumentNullException>(() => new CampaignProgress(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => progress.RecordLevelCleared(Difficulty.Pilot, 0, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => progress.RecordLevelCleared(Difficulty.Pilot, 9, 8));
        }
    }
}
