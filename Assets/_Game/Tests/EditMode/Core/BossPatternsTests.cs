using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class BossPatternsTests
    {
        [Test]
        public void Position_FliesInThenHolds()
        {
            var start = new Vector2(12f, 0f);

            AssertVector(new Vector2(10f, 0f), BossPatterns.Position(start, 5f, 2f, 1f, 6f, 1f));
            var atArrival = BossPatterns.Position(start, 5f, 2f, 1f, 6f, 3.5f);
            AssertVector(new Vector2(5f, 0f), atArrival, 1e-4f);
        }

        [Test]
        public void Position_DriftsUpAndDownAfterArriving()
        {
            var start = new Vector2(12f, 1f);
            var quarter = BossPatterns.Position(start, 5f, 2f, 2f, 8f, 3.5f + 2f);

            AssertVector(new Vector2(5f, 3f), quarter, 1e-4f);
        }

        [Test]
        public void Spread_FansEvenlyAroundAim()
        {
            var directions = new List<Vector2>();

            BossPatterns.Spread(-Vector2.UnitX, 5, 60f, directions);

            Assert.That(directions.Count, Is.EqualTo(5));
            var angles = directions.ConvertAll(AngleDegrees);
            Assert.That(angles, Has.Some.EqualTo(150f).Within(0.01f));
            Assert.That(angles, Has.Some.EqualTo(-150f).Within(0.01f));
            Assert.That(angles, Has.Some.EqualTo(180f).Within(0.01f).Or.EqualTo(-180f).Within(0.01f));
            foreach (var d in directions) Assert.That(d.Length(), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Spread_SingleBulletGoesStraightAtAim()
        {
            var directions = new List<Vector2>();

            BossPatterns.Spread(new Vector2(0f, 3f), 1, 60f, directions);

            AssertVector(Vector2.UnitY, directions[0]);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPatterns.Position(Vector2.Zero, 0f, 0f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPatterns.Position(Vector2.Zero, 0f, 1f, 1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPatterns.Spread(Vector2.UnitX, 0, 60f, new List<Vector2>()));
            Assert.Throws<ArgumentNullException>(() => BossPatterns.Spread(Vector2.UnitX, 1, 60f, null));
        }
    }

    public class BossContactTests
    {
        [Test]
        public void BossRam_HitsOnceThenIgnoresContactDuringCooldown()
        {
            var session = new GameSession(DifficultySettings.Pilot, new TestRandom());

            Assert.That(session.ReportPlayerRam(0, true, 40f, 0).Outcome, Is.EqualTo(HitOutcome.Damaged));
            Assert.That(session.ReportPlayerRam(0, true, 40f, 0).Outcome, Is.EqualTo(HitOutcome.Ignored));
            Assert.That(session.GetPlayer(0).Vitals.Health, Is.EqualTo(60f).Within(1e-4f));

            Idle(session, GameTuning.BossContactCooldownSeconds + 0.02f);
            Assert.That(session.ReportPlayerRam(0, true, 40f, 0).Outcome, Is.EqualTo(HitOutcome.Damaged));
        }

        [Test]
        public void BossRam_AbsorbedByShield_AlsoStartsCooldown()
        {
            var session = new GameSession(DifficultySettings.Pilot, new TestRandom());
            session.ReportPickupCollected(0, PickupType.Shield);

            session.ReportPlayerRam(0, true, 40f, 0);
            session.ReportPlayerRam(0, true, 40f, 0);

            Assert.That(session.GetPlayer(0).Shield.HitsRemaining, Is.EqualTo(GameTuning.ShieldHits - 1));
        }

        [Test]
        public void NonBossRams_HaveNoCooldown()
        {
            var session = new GameSession(DifficultySettings.Pilot, new TestRandom());

            session.ReportPlayerRam(0, false, 10f, 100);
            session.ReportPlayerRam(0, false, 10f, 100);

            Assert.That(session.GetPlayer(0).Vitals.Health, Is.EqualTo(80f).Within(1e-4f));
        }
    }
}
