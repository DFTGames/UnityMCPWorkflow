using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class PointValuesTests
    {
        [TestCase(EnemySize.Small, 100)]
        [TestCase(EnemySize.Medium, 250)]
        [TestCase(EnemySize.Large, 500)]
        public void Enemy(EnemySize size, int points) => Assert.That(PointValues.Enemy(size), Is.EqualTo(points));

        [TestCase(MeteorSize.Small, 20)]
        [TestCase(MeteorSize.Medium, 30)]
        [TestCase(MeteorSize.Large, 50)]
        public void SplittingMeteor(MeteorSize size, int points) =>
            Assert.That(PointValues.SplittingMeteor(size), Is.EqualTo(points));

        [Test]
        public void Constants_MatchGdd()
        {
            Assert.That(PointValues.SolidMeteor, Is.EqualTo(150));
            Assert.That(PointValues.NoDamageBossBonus, Is.EqualTo(5000));
            Assert.That(PointValues.MaxLevelWeaponUpgrade, Is.EqualTo(500));
        }

        [Test]
        public void UnknownSizes_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PointValues.Enemy((EnemySize)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => PointValues.SplittingMeteor((MeteorSize)99));
        }

        [TestCase(1, 10000)]
        [TestCase(8, 80000)]
        public void Boss_IsTenThousandTimesLevel(int level, int points) =>
            Assert.That(PointValues.Boss(level), Is.EqualTo(points));

        [Test]
        public void Boss_RejectsLevelBelowOne() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => PointValues.Boss(0));

        [TestCase(1f, 2000)]
        [TestCase(0.5f, 1000)]
        [TestCase(0.333f, 666)]
        [TestCase(0f, 0)]
        [TestCase(1.5f, 2000)]
        [TestCase(-1f, 0)]
        public void LevelClear_IsHealthPercentTimesTwenty(float fraction, int points) =>
            Assert.That(PointValues.LevelClear(fraction), Is.EqualTo(points));
    }

    public class ScoreKeeperTests
    {
        const float Window = GameTuning.ChainWindowSeconds;

        static ScoreKeeper WithChainSteps(float difficulty, int steps)
        {
            var score = new ScoreKeeper(difficulty);
            for (var i = 0; i <= steps; i++) score.RegisterKill(0);
            Assert.That(score.ChainSteps, Is.EqualTo(steps));
            return score;
        }

        [Test]
        public void FirstKill_AwardsBaseTimesDifficulty()
        {
            var score = new ScoreKeeper(1.5f);

            Assert.That(score.RegisterKill(100), Is.EqualTo(150));
            Assert.That(score.Score, Is.EqualTo(150));
            Assert.That(score.Kills, Is.EqualTo(1));
            Assert.That(score.ChainMultiplier, Is.EqualTo(1f));
            Assert.That(score.EndlessMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void KillWithinWindow_RaisesChainBeforeAwarding()
        {
            var score = new ScoreKeeper(1f);
            score.RegisterKill(100);
            score.Tick(1.0f);

            Assert.That(score.RegisterKill(100), Is.EqualTo(110));
            Assert.That(score.ChainSteps, Is.EqualTo(1));
        }

        [Test]
        public void KillAtWindowReachedInManyFixedTicks_StillChains()
        {
            var score = new ScoreKeeper(1f);
            score.RegisterKill(100);
            for (var i = 0; i < 75; i++) score.Tick(0.02f); // 1.5 s at 50 Hz, with float drift

            Assert.That(score.RegisterKill(100), Is.EqualTo(110));
        }

        [Test]
        public void WindowExpiry_ResetsChain()
        {
            var score = new ScoreKeeper(1f);
            score.RegisterKill(100);
            score.RegisterKill(100);
            score.Tick(Window + 0.01f);

            Assert.That(score.ChainSteps, Is.EqualTo(0));
            Assert.That(score.RegisterKill(100), Is.EqualTo(100));
        }

        [Test]
        public void Chain_CapsAtThree()
        {
            var score = WithChainSteps(1f, 20);

            score.RegisterKill(0);
            Assert.That(score.ChainSteps, Is.EqualTo(20));
            Assert.That(score.ChainMultiplier, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(score.RegisterKill(100), Is.EqualTo(300));
        }

        [TestCase(30, 1.5f, 11, 95)]   // 30 x 2.1 x 1.5 = 94.5
        [TestCase(50, 1.5f, 11, 158)]  // 157.5
        [TestCase(50, 2.5f, 11, 263)]  // 262.5
        [TestCase(500, 1.5f, 1, 825)]
        public void ChainedKill_RoundsHalvesAwayFromZeroExactly(int basePoints, float difficulty, int steps,
            int expected)
        {
            var score = WithChainSteps(difficulty, steps - 1);

            Assert.That(score.RegisterKill(basePoints), Is.EqualTo(expected));
        }

        [Test]
        public void EndlessMultiplier_AppliesToKills()
        {
            var score = WithChainSteps(1.5f, 10);
            score.SetEndlessMultiplier(1.5f);

            Assert.That(score.RegisterKill(100), Is.EqualTo(473)); // 100 x 2.1 x 1.5 x 1.5 = 472.5
        }

        [Test]
        public void PlayerDamaged_ResetsChain()
        {
            var score = new ScoreKeeper(1f);
            score.RegisterKill(100);
            score.RegisterKill(100);

            score.NotifyPlayerDamaged();

            Assert.That(score.ChainSteps, Is.EqualTo(0));
            Assert.That(score.RegisterKill(100), Is.EqualTo(100));
        }

        [Test]
        public void Bonus_IgnoresChainButUsesDifficultyAndEndless()
        {
            var score = WithChainSteps(2.5f, 4);
            score.SetEndlessMultiplier(1.25f);

            Assert.That(score.RegisterBonus(500), Is.EqualTo(1563)); // 500 x 2.5 x 1.25 = 1562.5
            Assert.That(score.Kills, Is.EqualTo(5));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreKeeper(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreKeeper(1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreKeeper(1f, 1f, -1));
            var score = new ScoreKeeper(1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => score.RegisterKill(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => score.RegisterBonus(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => score.SetEndlessMultiplier(0f));
        }
    }
}
