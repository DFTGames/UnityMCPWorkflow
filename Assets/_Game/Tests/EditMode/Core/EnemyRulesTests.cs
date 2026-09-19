using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class HealthTests
    {
        [Test]
        public void TakeDamage_ReportsOnlyTheKillingHit()
        {
            var health = new Health(3f);

            Assert.That(health.TakeDamage(1f), Is.False);
            Assert.That(health.TakeDamage(1f), Is.False);
            Assert.That(health.TakeDamage(5f), Is.True);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.Current, Is.EqualTo(0f));
            Assert.That(health.TakeDamage(1f), Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void TakeDamage_NonPositive_IsIgnored(float amount)
        {
            var health = new Health(1f);

            Assert.That(health.TakeDamage(amount), Is.False);
            Assert.That(health.Current, Is.EqualTo(1f));
        }

        [Test]
        public void Constructor_RejectsNonPositiveMax()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Health(0f));
        }
    }

    public class EnemyMotionTests
    {
        [Test]
        public void Straight_FliesLeftAtSpeed()
        {
            AssertVector(new Vector2(4f, 2f), EnemyMotion.Straight(new Vector2(10f, 2f), 3f, 2f));
        }

        [Test]
        public void SineWave_OscillatesAroundStartHeight()
        {
            var start = new Vector2(10f, 1f);

            AssertVector(new Vector2(10f, 1f), EnemyMotion.SineWave(start, 2f, 1.5f, 0.5f, 0f));
            AssertVector(new Vector2(9f, 2.5f), EnemyMotion.SineWave(start, 2f, 1.5f, 0.5f, 0.5f));
            AssertVector(new Vector2(6f, 1f), EnemyMotion.SineWave(start, 2f, 1.5f, 0.5f, 2f), 1e-4f);
        }

        [Test]
        public void Evaluate_DispatchesByPattern()
        {
            var start = new Vector2(10f, 1f);

            AssertVector(EnemyMotion.Straight(start, 3f, 1f),
                EnemyMotion.Evaluate(MotionPattern.Straight, start, 3f, 1.5f, 0.5f, 1f));
            AssertVector(EnemyMotion.SineWave(start, 3f, 1.5f, 0.5f, 0.3f),
                EnemyMotion.Evaluate(MotionPattern.SineWave, start, 3f, 1.5f, 0.5f, 0.3f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EnemyMotion.Evaluate((MotionPattern)99, start, 1f, 1f, 1f, 1f));
        }

        [Test]
        public void AimAt_ReturnsUnitVectorOrFallback()
        {
            AssertVector(new Vector2(-0.6f, 0.8f), EnemyMotion.AimAt(new Vector2(3f, 0f), new Vector2(0f, 4f), Vector2.UnitX));
            AssertVector(-Vector2.UnitX, EnemyMotion.AimAt(Vector2.One, Vector2.One, -Vector2.UnitX));
        }
    }

    public class FireTimerTests
    {
        [Test]
        public void FiresAfterInitialDelayThenEveryInterval()
        {
            var timer = new FireTimer(1f, 0.5f);

            Assert.That(timer.Tick(0.25f), Is.False);
            Assert.That(timer.Tick(0.25f), Is.True);
            Assert.That(timer.Tick(0.75f), Is.False);
            Assert.That(timer.Tick(0.25f), Is.True);
        }

        [Test]
        public void LongTick_FiresOnceWithoutBurst()
        {
            var timer = new FireTimer(1f, 0f);

            Assert.That(timer.Tick(5f), Is.True);
            Assert.That(timer.Tick(0.5f), Is.False);
            Assert.That(timer.Tick(0.5f), Is.True);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTimer(0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTimer(1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireTimer(1f, 0f).Tick(-1f));
        }
    }

    public class MeteorRulesTests
    {
        [Test]
        public void TrySplit_LargeToMediumToSmallThenStops()
        {
            Assert.That(MeteorRules.TrySplit(MeteorSize.Large, out var a), Is.True);
            Assert.That(a, Is.EqualTo(MeteorSize.Medium));
            Assert.That(MeteorRules.TrySplit(MeteorSize.Medium, out var b), Is.True);
            Assert.That(b, Is.EqualTo(MeteorSize.Small));
            Assert.That(MeteorRules.TrySplit(MeteorSize.Small, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => MeteorRules.TrySplit((MeteorSize)99, out _));
        }

        [Test]
        public void Points_AndDropSource_FollowType()
        {
            Assert.That(MeteorRules.Points(true, MeteorSize.Large), Is.EqualTo(50));
            Assert.That(MeteorRules.Points(false, MeteorSize.Large), Is.EqualTo(150));
            Assert.That(MeteorRules.DropSourceFor(true), Is.EqualTo(DropSource.SplittingMeteor));
            Assert.That(MeteorRules.DropSourceFor(false), Is.EqualTo(DropSource.SolidMeteor));
        }

        [Test]
        public void FragmentVelocity_RotatesParentBySpreadEitherSide()
        {
            var parent = new Vector2(-2f, 0f);

            var first = MeteorRules.FragmentVelocity(parent, 0);
            var second = MeteorRules.FragmentVelocity(parent, 1);

            Assert.That(first.Length(), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(AngleDegrees(first), Is.EqualTo(-155f).Within(0.01f)); // 180 + 25
            Assert.That(AngleDegrees(second), Is.EqualTo(155f).Within(0.01f)); // 180 - 25
            Assert.Throws<ArgumentOutOfRangeException>(() => MeteorRules.FragmentVelocity(parent, 2));
        }
    }

    public class SpawnDirectorTests
    {
        static SpawnDirector Create(TestRandom random, float countMultiplier = 1f, float initialDelay = 0f) =>
            new SpawnDirector(new[] { 3, 0, 1 }, 2f, countMultiplier, random, initialDelay);

        [Test]
        public void Interval_IsBaseDividedByCountMultiplier()
        {
            Assert.That(Create(new TestRandom(), 1.3f).Interval, Is.EqualTo(2f / 1.3f).Within(1e-5f));
        }

        [Test]
        public void SpawnsAfterDelayThenEveryInterval()
        {
            var director = Create(new TestRandom(0f, 0.25f), initialDelay: 1f);
            var requests = new List<SpawnRequest>();

            Assert.That(director.Tick(0.5f, requests), Is.False);
            Assert.That(director.Tick(0.5f, requests), Is.True);
            Assert.That(director.Tick(1.5f, requests), Is.False);
            Assert.That(director.Tick(0.5f, requests), Is.True);
            Assert.That(requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void LongTick_SpawnsOnlyOnce()
        {
            var director = Create(new TestRandom(0f));
            var requests = new List<SpawnRequest>();

            director.Tick(10f, requests);

            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [TestCase(0.0f, 0)]
        [TestCase(0.74f, 0)]
        [TestCase(0.75f, 2)]
        [TestCase(0.999f, 2)]
        public void PicksEntryByWeightSkippingZeroWeights(float roll, int expected)
        {
            var director = Create(new TestRandom(roll, 0.6f));
            var requests = new List<SpawnRequest>();

            director.Tick(0f, requests);

            Assert.That(requests[0].EntryIndex, Is.EqualTo(expected));
            Assert.That(requests[0].NormalisedY, Is.EqualTo(0.6f));
        }

        [TestCase(0.749f, 0)]
        [TestCase(0.75f, 3)]
        public void ConsecutiveZeroWeights_AreSkipped(float roll, int expected)
        {
            var director = new SpawnDirector(new[] { 3, 0, 0, 1 }, 2f, 1f, new TestRandom(roll, 0f), 0f);
            var requests = new List<SpawnRequest>();

            director.Tick(0f, requests);

            Assert.That(requests[0].EntryIndex, Is.EqualTo(expected));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var random = new TestRandom();
            Assert.Throws<ArgumentNullException>(() => new SpawnDirector(null, 1f, 1f, random, 0f));
            Assert.Throws<ArgumentException>(() => new SpawnDirector(new[] { 0, 0 }, 1f, 1f, random, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnDirector(new[] { -1, 2 }, 1f, 1f, random, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnDirector(new[] { 1 }, 0f, 1f, random, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnDirector(new[] { 1 }, 1f, 0f, random, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnDirector(new[] { 1 }, 1f, 1f, random, -1f));
            Assert.Throws<ArgumentNullException>(() => new SpawnDirector(new[] { 1 }, 1f, 1f, null, 0f));
            var director = new SpawnDirector(new[] { 1 }, 1f, 1f, random, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => director.Tick(-1f, new List<SpawnRequest>()));
            Assert.Throws<ArgumentNullException>(() => director.Tick(0f, null));
        }
    }
}
