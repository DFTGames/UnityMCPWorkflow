using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class HiveCarrierBrainTests
    {
        const float Step = 0.02f;

        static List<BossAction> Run(HiveCarrierBrain brain, float seconds)
        {
            var actions = new List<BossAction>();
            var steps = (int)Math.Round(seconds / Step);
            for (var i = 0; i < steps; i++) brain.Tick(Step, actions);
            return actions;
        }

        static int Count(List<BossAction> actions, BossActionType type) => actions.FindAll(a => a.Type == type).Count;

        [Test]
        public void Defaults_MatchGdd()
        {
            var spec = HiveCarrierSpec.Default;

            Assert.That(spec.MaxHealth, Is.EqualTo(200f));
            Assert.That(spec.CycleSeconds, Is.EqualTo(8f));
            Assert.That(spec.OpenSeconds, Is.EqualTo(4f));
            Assert.That(spec.DartsPerLaunch, Is.EqualTo(3));
            Assert.That(spec.SpreadIntervalSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.SpreadBullets, Is.EqualTo(5));
            Assert.That(spec.EnragedSpreadBullets, Is.EqualTo(7));
            Assert.That(spec.EnragedAtHealthFraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void StartsClosedAndLaunchesThreeDarts()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);

            var actions = Run(brain, Step);

            Assert.That(brain.IsCoreOpen, Is.False);
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0].Type, Is.EqualTo(BossActionType.LaunchDarts));
            Assert.That(actions[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void CoreOpensForFourSecondsOfEachEight()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);

            Run(brain, 3.9f);
            Assert.That(brain.IsCoreOpen, Is.False);
            Run(brain, 0.2f);
            Assert.That(brain.IsCoreOpen, Is.True);
            Run(brain, 3.8f);
            Assert.That(brain.IsCoreOpen, Is.True);
            Run(brain, 0.2f);
            Assert.That(brain.IsCoreOpen, Is.False);
        }

        [Test]
        public void OneFullCycle_LaunchesOnceAndFiresThreeFiveBulletSpreads()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);

            var actions = Run(brain, 7.98f);

            Assert.That(Count(actions, BossActionType.LaunchDarts), Is.EqualTo(1));
            var spreads = actions.FindAll(a => a.Type == BossActionType.FireSpread);
            Assert.That(spreads.Count, Is.EqualTo(3)); // at 0, 1.5 and 3 s into the open phase
            Assert.That(spreads.TrueForAll(a => a.Count == 5), Is.True);
        }

        [Test]
        public void SecondCycle_LaunchesAgain()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);

            var actions = Run(brain, 8.1f);

            Assert.That(Count(actions, BossActionType.LaunchDarts), Is.EqualTo(2));
        }

        [Test]
        public void CoreTakesDamageOnlyWhileOpen()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);
            Run(brain, 1f);

            Assert.That(brain.TakeCoreHit(10f), Is.False);
            Assert.That(brain.Health.Current, Is.EqualTo(200f));

            Run(brain, 3.1f);
            brain.TakeCoreHit(10f);
            Assert.That(brain.Health.Current, Is.EqualTo(190f));
        }

        [Test]
        public void Enraged_LaunchesTwicePerCycleAndFiresSevenBulletSpreads()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);
            Run(brain, 4.1f);
            brain.TakeCoreHit(100f);
            Assert.That(brain.IsEnraged, Is.True);

            // Step until the core closes, so the next window starts exactly at a closed phase.
            var steps = 0;
            while (brain.IsCoreOpen && steps++ < 1000) brain.Tick(Step, new List<BossAction>());
            var nextCycle = Run(brain, 7.9f);

            Assert.That(Count(nextCycle, BossActionType.LaunchDarts), Is.EqualTo(2));
            var spreads = nextCycle.FindAll(a => a.Type == BossActionType.FireSpread);
            Assert.That(spreads.Count, Is.GreaterThan(0));
            Assert.That(spreads.TrueForAll(a => a.Count == 7), Is.True);
        }

        [Test]
        public void Defeat_ReportsOnceAndStopsActing()
        {
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);
            Run(brain, 4.1f);

            Assert.That(brain.TakeCoreHit(200f), Is.True);
            Assert.That(brain.IsDefeated, Is.True);
            Assert.That(brain.TakeCoreHit(10f), Is.False);
            Assert.That(Run(brain, 10f), Is.Empty);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new HiveCarrierBrain(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HiveCarrierSpec(maxHealth: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HiveCarrierSpec(cycleSeconds: 4f, openSeconds: 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HiveCarrierSpec(spreadIntervalSeconds: 0f));
            var brain = new HiveCarrierBrain(HiveCarrierSpec.Default);
            Assert.Throws<ArgumentOutOfRangeException>(() => brain.Tick(-1f, new List<BossAction>()));
            Assert.Throws<ArgumentNullException>(() => brain.Tick(Step, null));
        }
    }

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
