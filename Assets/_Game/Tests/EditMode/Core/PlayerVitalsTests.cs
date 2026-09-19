using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class PlayerVitalsTests
    {
        const float Invulnerability = GameTuning.RespawnInvulnerabilitySeconds;

        static PlayerVitals Create(int lives = 3, float damageMultiplier = 1f) =>
            new PlayerVitals(lives, 100f, damageMultiplier, Invulnerability);

        [Test]
        public void NewVitals_StartAtFullHealthAndVulnerable()
        {
            var v = Create();

            Assert.That(v.Lives, Is.EqualTo(3));
            Assert.That(v.Health, Is.EqualTo(100f));
            Assert.That(v.HealthFraction, Is.EqualTo(1f));
            Assert.That(v.IsInvulnerable, Is.False);
            Assert.That(v.IsGameOver, Is.False);
        }

        [Test]
        public void For_UsesDifficultyLivesHealthAndInvulnerability()
        {
            var v = PlayerVitals.For(DifficultySettings.Cadet);
            v.ApplyDamage(1000f);

            Assert.That(v.Lives, Is.EqualTo(4));
            Assert.That(v.MaxHealth, Is.EqualTo(100f));
            Assert.That(v.InvulnerabilityRemaining, Is.EqualTo(Invulnerability));
        }

        [Test]
        public void ApplyDamage_ScalesByDifficultyMultiplier()
        {
            var v = Create(damageMultiplier: 1.5f);

            Assert.That(v.ApplyDamage(20f), Is.EqualTo(HitOutcome.Damaged));
            Assert.That(v.Health, Is.EqualTo(70f).Within(1e-4f));
        }

        [Test]
        public void ApplyDamage_ReachingZero_LosesLifeRefillsHealthAndGrantsInvulnerability()
        {
            var v = Create();

            Assert.That(v.ApplyDamage(100f), Is.EqualTo(HitOutcome.LifeLost));
            Assert.That(v.Lives, Is.EqualTo(2));
            Assert.That(v.Health, Is.EqualTo(100f));
            Assert.That(v.IsInvulnerable, Is.True);
        }

        [Test]
        public void ApplyDamage_SecondHitInSameTickAfterLifeLost_IsIgnored()
        {
            var v = Create();
            v.ApplyDamage(100f);

            Assert.That(v.ApplyDamage(50f), Is.EqualTo(HitOutcome.Ignored));
            Assert.That(v.Health, Is.EqualTo(100f));
            Assert.That(v.Lives, Is.EqualTo(2));
        }

        [Test]
        public void Invulnerability_ExpiresAfterDuration()
        {
            var v = Create();
            v.ApplyDamage(100f);

            v.Tick(Invulnerability - 0.1f);
            Assert.That(v.IsInvulnerable, Is.True);

            v.Tick(0.2f);
            Assert.That(v.IsInvulnerable, Is.False);
            Assert.That(v.InvulnerabilityRemaining, Is.EqualTo(0f));
            Assert.That(v.ApplyDamage(10f), Is.EqualTo(HitOutcome.Damaged));
        }

        [Test]
        public void ApplyDamage_OnLastLife_IsGameOver()
        {
            var v = Create(lives: 1);

            Assert.That(v.ApplyDamage(150f), Is.EqualTo(HitOutcome.GameOver));
            Assert.That(v.Lives, Is.EqualTo(0));
            Assert.That(v.Health, Is.EqualTo(0f));
            Assert.That(v.IsGameOver, Is.True);
        }

        [Test]
        public void AfterGameOver_DamageHealAndLivesAreIgnored()
        {
            var v = Create(lives: 1);
            v.ApplyDamage(100f);

            Assert.That(v.ApplyDamage(10f), Is.EqualTo(HitOutcome.Ignored));
            v.Heal(50f);
            v.AddLife();

            Assert.That(v.Health, Is.EqualTo(0f));
            Assert.That(v.Lives, Is.EqualTo(0));
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        public void ApplyDamage_NonPositive_IsIgnored(float damage)
        {
            var v = Create();

            Assert.That(v.ApplyDamage(damage), Is.EqualTo(HitOutcome.Ignored));
            Assert.That(v.Health, Is.EqualTo(100f));
        }

        [Test]
        public void Heal_ClampsToMaxHealth()
        {
            var v = Create();
            v.ApplyDamage(30f);

            v.Heal(20f);
            Assert.That(v.Health, Is.EqualTo(90f).Within(1e-4f));

            v.Heal(50f);
            Assert.That(v.Health, Is.EqualTo(100f));
        }

        [Test]
        public void Heal_AtFullHealth_StaysAtMax()
        {
            var v = Create();

            v.Heal(25f);

            Assert.That(v.Health, Is.EqualTo(100f));
        }

        [Test]
        public void AddLife_CanExceedStartingLives()
        {
            var v = Create();

            v.AddLife();
            v.AddLife();

            Assert.That(v.Lives, Is.EqualTo(5));
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerVitals(0, 100f, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerVitals(3, 0f, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerVitals(3, 100f, -1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerVitals(3, 100f, 1f, -1f));
        }
    }
}
