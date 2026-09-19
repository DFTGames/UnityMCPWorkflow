using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class DifficultySettingsTests
    {
        [TestCase(Difficulty.Cadet, 5, 0.75f, 0.85f, 0.75f, 0.7f, 1.25f, 1.0f)]
        [TestCase(Difficulty.Pilot, 3, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.5f)]
        [TestCase(Difficulty.Ace, 2, 1.5f, 1.2f, 1.3f, 1.3f, 0.75f, 2.5f)]
        public void For_ReturnsGddValues(Difficulty difficulty, int lives, float damage, float speed, float count,
            float fireRate, float drops, float score)
        {
            var s = DifficultySettings.For(difficulty);

            Assert.That(s.Difficulty, Is.EqualTo(difficulty));
            Assert.That(s.StartingLives, Is.EqualTo(lives));
            Assert.That(s.HealthPerLife, Is.EqualTo(100f));
            Assert.That(s.DamageTakenMultiplier, Is.EqualTo(damage));
            Assert.That(s.EnemySpeedMultiplier, Is.EqualTo(speed));
            Assert.That(s.EnemyCountMultiplier, Is.EqualTo(count));
            Assert.That(s.EnemyFireRateMultiplier, Is.EqualTo(fireRate));
            Assert.That(s.PickupDropChanceMultiplier, Is.EqualTo(drops));
            Assert.That(s.ScoreMultiplier, Is.EqualTo(score));
        }

        [Test]
        public void For_UnknownDifficulty_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DifficultySettings.For((Difficulty)99));
        }
    }
}
