using System;
using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class PickupDropperTests
    {
        const float PityDelay = GameTuning.WeaponPityDelaySeconds;

        [TestCase(DropSource.Enemy, 0.08f)]
        [TestCase(DropSource.SolidMeteor, 0.15f)]
        [TestCase(DropSource.SplittingMeteor, 0f)]
        public void BaseDropChance_MatchesGdd(DropSource source, float chance) =>
            Assert.That(PickupDropper.BaseDropChance(source), Is.EqualTo(chance));

        [Test]
        public void DropChance_ScalesWithDifficultyAndClampsToOne()
        {
            Assert.That(new PickupDropper(new TestRandom(), 1.25f).DropChance(DropSource.Enemy),
                Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(new PickupDropper(new TestRandom(), 100f).DropChance(DropSource.SolidMeteor),
                Is.EqualTo(1f));
        }

        [Test]
        public void RollAtOrAboveChance_DropsNothing()
        {
            var dropper = new PickupDropper(new TestRandom(0.08f), 1f);

            Assert.That(dropper.TryRollDrop(DropSource.Enemy, 1, out var pickup), Is.False);
            Assert.That(pickup, Is.EqualTo(PickupType.None));
        }

        [Test]
        public void SplittingMeteor_NeverDropsOrConsumesRandom()
        {
            var random = new TestRandom(0f);
            var dropper = new PickupDropper(random, 1f);

            Assert.That(dropper.TryRollDrop(DropSource.SplittingMeteor, 1, out _), Is.False);
            Assert.That(random.Calls, Is.EqualTo(0));
        }

        [TestCase(0.00f, PickupType.Health)]
        [TestCase(0.399f, PickupType.Health)]
        [TestCase(0.40f, PickupType.WeaponUpgrade)]
        [TestCase(0.749f, PickupType.WeaponUpgrade)]
        [TestCase(0.75f, PickupType.Shield)]
        [TestCase(0.949f, PickupType.Shield)]
        [TestCase(0.951f, PickupType.ExtraLife)]
        [TestCase(0.999f, PickupType.ExtraLife)]
        public void SuccessfulRoll_PicksTypeByWeight(float typeRoll, PickupType expected)
        {
            var dropper = new PickupDropper(new TestRandom(0f, typeRoll), 1f);

            Assert.That(dropper.TryRollDrop(DropSource.Enemy, 1, out var pickup), Is.True);
            Assert.That(pickup, Is.EqualTo(expected));
        }

        [Test]
        public void WeightedPick_MatchesWeightsAcrossUniformRolls()
        {
            var counts = new Dictionary<PickupType, int>();
            for (var i = 0; i < 1000; i++)
            {
                var dropper = new PickupDropper(new TestRandom(0f, (i + 0.5f) / 1000f), 1f);
                dropper.TryRollDrop(DropSource.Enemy, 5, out var pickup);
                counts[pickup] = counts.TryGetValue(pickup, out var c) ? c + 1 : 1;
            }

            Assert.That(counts[PickupType.Health], Is.EqualTo(400));
            Assert.That(counts[PickupType.WeaponUpgrade], Is.EqualTo(350));
            Assert.That(counts[PickupType.Shield], Is.EqualTo(200));
            Assert.That(counts[PickupType.ExtraLife], Is.EqualTo(50));
        }

        [Test]
        public void Pity_ForcesOneWeaponUpgradeThenRestartsTimer()
        {
            var dropper = new PickupDropper(new TestRandom(0f, 0f, 0f, 0f), 1f);
            dropper.Tick(PityDelay);

            Assert.That(dropper.IsPityDue(2), Is.True);
            Assert.That(dropper.TryRollDrop(DropSource.Enemy, 2, out var first), Is.True);
            Assert.That(first, Is.EqualTo(PickupType.WeaponUpgrade));

            Assert.That(dropper.IsPityDue(2), Is.False);
            Assert.That(dropper.TryRollDrop(DropSource.Enemy, 2, out var second), Is.True);
            Assert.That(second, Is.EqualTo(PickupType.Health));
        }

        [Test]
        public void Pity_StillRequiresTheDropRollToSucceed()
        {
            var dropper = new PickupDropper(new TestRandom(0.5f), 1f);
            dropper.Tick(PityDelay + 15f);

            Assert.That(dropper.TryRollDrop(DropSource.Enemy, 1, out _), Is.False);
            Assert.That(dropper.IsPityDue(1), Is.True);
        }

        [Test]
        public void Pity_NotDueAtLevelThreeOrBeforeDelay()
        {
            var dropper = new PickupDropper(new TestRandom(), 1f);
            dropper.Tick(PityDelay - 0.1f);
            Assert.That(dropper.IsPityDue(1), Is.False);

            dropper.Tick(1f);
            Assert.That(dropper.IsPityDue(2), Is.True);
            Assert.That(dropper.IsPityDue(3), Is.False);
        }

        [Test]
        public void WeaponUpgradeCollected_ResetsPityTimer()
        {
            var dropper = new PickupDropper(new TestRandom(), 1f);
            dropper.Tick(PityDelay + 5f);

            dropper.NotifyWeaponUpgradeCollected();

            Assert.That(dropper.TimeSinceWeaponUpgrade, Is.EqualTo(0f));
            Assert.That(dropper.IsPityDue(1), Is.False);
        }

        [Test]
        public void BossDrops_AreWeaponUpgradeAndHealth()
        {
            Assert.That(PickupDropper.BossDrops,
                Is.EquivalentTo(new[] { PickupType.WeaponUpgrade, PickupType.Health }));
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PickupDropper(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupDropper(new TestRandom(), -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PickupDropper.BaseDropChance((DropSource)99));
        }

        [Test]
        public void SystemRandomSource_ReturnsValuesInUnitRangeAndIsSeeded()
        {
            var a = new SystemRandomSource(42);
            var b = new SystemRandomSource(42);
            for (var i = 0; i < 1000; i++)
            {
                var value = a.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(b.NextFloat(), Is.EqualTo(value));
            }
        }
    }
}
