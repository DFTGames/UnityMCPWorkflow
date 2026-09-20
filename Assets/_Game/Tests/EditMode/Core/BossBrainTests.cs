using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The boss brain is shared by every boss in the campaign; the Hive Carrier's own numbers are the worked
    /// example here, and the assets are checked against the GDD by <c>BossDataTests</c>.
    /// </summary>
    public class BossBrainTests
    {
        const float Step = 0.02f;

        /// <summary>The Hive Carrier as data (GDD "Hive Carrier"): launches while shut, fans bullets while open.</summary>
        static BossSpec HiveCarrier() => new BossSpec("Hive Carrier", 200f, 8f, 4f, new[]
        {
            new BossAttack(BossActionType.LaunchDarts, 3, BossPhase.Armoured),
            new BossAttack(BossActionType.LaunchDarts, 3, BossPhase.Armoured, firstDelaySeconds: 2f,
                activeAtOrBelow: BossBrain.EnragedAtHealthFraction),
            new BossAttack(BossActionType.FireSpread, 5, BossPhase.Vulnerable, intervalSeconds: 1.5f,
                activeAbove: BossBrain.EnragedAtHealthFraction),
            new BossAttack(BossActionType.FireSpread, 7, BossPhase.Vulnerable, intervalSeconds: 1.5f,
                activeAtOrBelow: BossBrain.EnragedAtHealthFraction)
        });

        static List<BossAction> Run(BossBrain brain, float seconds)
        {
            var actions = new List<BossAction>();
            var steps = (int)Math.Round(seconds / Step);
            for (var i = 0; i < steps; i++) brain.Tick(Step, actions);
            return actions;
        }

        static int Count(List<BossAction> actions, BossActionType type) => actions.FindAll(a => a.Type == type).Count;

        [Test]
        public void StartsArmouredAndLaunchesThreeDarts()
        {
            var brain = new BossBrain(HiveCarrier());

            var actions = Run(brain, Step);

            Assert.That(brain.IsCoreOpen, Is.False, "the player sees what it does before getting a shot at it");
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0].Type, Is.EqualTo(BossActionType.LaunchDarts));
            Assert.That(actions[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void CoreOpensForFourSecondsOfEachEight()
        {
            var brain = new BossBrain(HiveCarrier());

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
            var brain = new BossBrain(HiveCarrier());

            var actions = Run(brain, 7.98f);

            Assert.That(Count(actions, BossActionType.LaunchDarts), Is.EqualTo(1), "a one-shot attack fires once per phase");
            var spreads = actions.FindAll(a => a.Type == BossActionType.FireSpread);
            Assert.That(spreads.Count, Is.EqualTo(3)); // at 0, 1.5 and 3 s into the open phase
            Assert.That(spreads.TrueForAll(a => a.Count == 5), Is.True);
        }

        [Test]
        public void SecondCycle_LaunchesAgain()
        {
            var brain = new BossBrain(HiveCarrier());

            var actions = Run(brain, 8.1f);

            Assert.That(Count(actions, BossActionType.LaunchDarts), Is.EqualTo(2));
        }

        [Test]
        public void CoreTakesDamageOnlyWhileOpen()
        {
            var brain = new BossBrain(HiveCarrier());
            Run(brain, 1f);

            Assert.That(brain.TakeCoreHit(10f), Is.False);
            Assert.That(brain.Health.Current, Is.EqualTo(200f));

            Run(brain, 3.1f);
            brain.TakeCoreHit(10f);
            Assert.That(brain.Health.Current, Is.EqualTo(190f));
        }

        [Test]
        public void WornDown_ItSwitchesToItsSecondHalfAttacks()
        {
            var brain = new BossBrain(HiveCarrier());
            Run(brain, 4.1f);
            brain.TakeCoreHit(100f);
            Assert.That(brain.IsEnraged, Is.True);

            // Step until the core closes, collecting as we go: the tick that closes it is already part of the
            // armoured phase, and the first launch goes out on it.
            var nextCycle = new List<BossAction>();
            var steps = 0;
            while (brain.IsCoreOpen && steps++ < 1000) brain.Tick(Step, nextCycle);
            nextCycle.RemoveAll(a => a.Type == BossActionType.FireSpread); // leftovers from the open phase
            nextCycle.AddRange(Run(brain, 7.9f));

            Assert.That(Count(nextCycle, BossActionType.LaunchDarts), Is.EqualTo(2), "a second launch mid-phase");
            var spreads = nextCycle.FindAll(a => a.Type == BossActionType.FireSpread);
            Assert.That(spreads.Count, Is.GreaterThan(0));
            Assert.That(spreads.TrueForAll(a => a.Count == 7), Is.True, "and wider fans");
        }

        [Test]
        public void HealthBands_NeverOverlap()
        {
            // The two spreads share a phase and differ only by health band: exactly one of them may be live.
            var brain = new BossBrain(HiveCarrier());
            Run(brain, 4.1f); // open, at full health
            var healthy = Run(brain, 1.6f).FindAll(a => a.Type == BossActionType.FireSpread);
            Assert.That(healthy, Is.Not.Empty);
            Assert.That(healthy.TrueForAll(a => a.Count == 5), Is.True);

            brain.TakeCoreHit(150f); // a quarter left

            // Step by step, so the gaps between shots can be measured: an attack that has just come into range
            // starts its interval from now, and picking up a schedule it was never on would fire it again on the
            // very next step (fourteen bullets 20 ms apart).
            var firedAt = new List<int>();
            var hurt = new List<BossAction>();
            for (var step = 0; step < 80; step++)
            {
                var thisStep = new List<BossAction>();
                brain.Tick(Step, thisStep);
                foreach (var action in thisStep)
                    if (action.Type == BossActionType.FireSpread)
                    {
                        hurt.Add(action);
                        firedAt.Add(step);
                    }
            }

            Assert.That(hurt, Is.Not.Empty);
            Assert.That(hurt.TrueForAll(a => a.Count == 7), Is.True);
            for (var i = 1; i < firedAt.Count; i++)
                Assert.That(firedAt[i] - firedAt[i - 1], Is.GreaterThanOrEqualTo(70),
                    "two spreads a step and a half apart: the new attack picked up a stale schedule");
        }

        [Test]
        public void ARepeatingAttack_CannotFireTwiceInOneStep()
        {
            // A long step must not let a fast attack empty its whole magazine at once. The phases are long
            // enough that the step cannot flip one, which would reset the schedule and prove nothing.
            var spec = new BossSpec("Gatling", 100f, 20f, 10f, new[]
            {
                new BossAttack(BossActionType.FireSpread, 1, BossPhase.Any, intervalSeconds: 0.1f)
            });
            var brain = new BossBrain(spec);
            var actions = new List<BossAction>();

            brain.Tick(1f, actions);
            Assert.That(actions.Count, Is.EqualTo(1), "one step, one shot");

            actions.Clear();
            brain.Tick(0.02f, actions);
            Assert.That(actions, Is.Empty, "and it does not immediately fire again to catch up");

            actions.Clear();
            brain.Tick(0.1f, actions);
            Assert.That(actions.Count, Is.EqualTo(1), "it picks its rhythm back up from where it fired");
        }

        [Test]
        public void Defeat_ReportsOnceAndStopsActing()
        {
            var brain = new BossBrain(HiveCarrier());
            Run(brain, 4.1f);

            Assert.That(brain.TakeCoreHit(200f), Is.True);
            Assert.That(brain.IsDefeated, Is.True);
            Assert.That(brain.TakeCoreHit(10f), Is.False);
            Assert.That(Run(brain, 10f), Is.Empty);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var attacks = new[] { new BossAttack(BossActionType.FireSpread, 1, BossPhase.Any) };

            Assert.Throws<ArgumentNullException>(() => new BossBrain(null));
            Assert.Throws<ArgumentException>(() => new BossSpec(" ", 100f, 8f, 4f, attacks));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossSpec("X", 0f, 8f, 4f, attacks));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossSpec("X", 100f, 4f, 4f, attacks));
            Assert.Throws<ArgumentException>(() => new BossSpec("X", 100f, 8f, 4f, Array.Empty<BossAttack>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossAttack(BossActionType.FireSpread, 0, BossPhase.Any));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BossAttack(BossActionType.FireSpread, 1, BossPhase.Any, activeAbove: 0.5f, activeAtOrBelow: 0.5f));

            var brain = new BossBrain(HiveCarrier());
            Assert.Throws<ArgumentOutOfRangeException>(() => brain.Tick(-1f, new List<BossAction>()));
            Assert.Throws<ArgumentNullException>(() => brain.Tick(Step, null));
        }
    }

    public class BossDashTests
    {
        static readonly Vector2 Station = new Vector2(6f, 1f);

        static BossDash Create() => new BossDash(0.5f, 20f, 8f, -8f, Station);

        [Test]
        public void ItHoldsStillWhileItWindsUp()
        {
            var dash = Create();
            var at = new Vector2(6f, 1f);

            at = dash.Step(0.3f, at);

            Assert.That(dash.Phase, Is.EqualTo(BossDash.DashPhase.WindUp), "the wind-up is the tell");
            AssertVector(Station, at);
            Assert.That(dash.WindUpProgress, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void ItCrossesTheScreenThenComesBack()
        {
            var dash = Create();
            var at = Station;
            at = dash.Step(0.5f, at); // wind-up over

            at = dash.Step(0.1f, at);
            Assert.That(dash.Phase, Is.EqualTo(BossDash.DashPhase.Dashing));
            Assert.That(at.X, Is.EqualTo(4f).Within(1e-4f), "20 units a second");
            Assert.That(at.Y, Is.EqualTo(Station.Y), "a dash is a straight line");

            for (var i = 0; i < 200 && dash.Phase == BossDash.DashPhase.Dashing; i++) at = dash.Step(0.02f, at);
            Assert.That(dash.Phase, Is.EqualTo(BossDash.DashPhase.Returning));
            Assert.That(at.X, Is.EqualTo(-8f).Within(1e-4f), "it stops at the far edge");

            for (var i = 0; i < 500 && !dash.IsFinished; i++) at = dash.Step(0.02f, at);
            Assert.That(dash.IsFinished, Is.True);
            AssertVector(Station, at, 1e-3f);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossDash(-1f, 1f, 1f, 0f, Station));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossDash(1f, 0f, 1f, 0f, Station));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossDash(1f, 1f, 0f, 0f, Station));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create().Step(-1f, Station));
        }
    }

    public class BeamSweepTests
    {
        [Test]
        public void ItTurnsSteadilyFromOneAngleToTheOther()
        {
            var sweep = new BeamSweep(135f, 225f, 2f, 0.5f);

            AssertVector(Vector2.Normalize(new Vector2(-1f, 1f)), sweep.Direction, 1e-4f);
            sweep.Tick(1f);
            AssertVector(-Vector2.UnitX, sweep.Direction, 1e-4f);
            sweep.Tick(1f);
            Assert.That(sweep.IsFinished, Is.True);
            AssertVector(Vector2.Normalize(new Vector2(-1f, -1f)), sweep.Direction, 1e-4f);
        }

        [Test]
        public void ItBurnsOnContactButNotEveryStep()
        {
            var sweep = new BeamSweep(180f, 180f, 2f, 0.5f);

            Assert.That(sweep.CanBurn, Is.True, "the moment it lights up it can burn");
            sweep.Burned();
            sweep.Tick(0.02f);
            Assert.That(sweep.CanBurn, Is.False, "but not again on the next frame");

            var burns = 0;
            for (var i = 0; i < 50; i++)
            {
                sweep.Tick(0.02f);
                if (!sweep.CanBurn) continue;

                burns++;
                sweep.Burned();
            }

            Assert.That(burns, Is.EqualTo(2), "once every half second");
        }

        [Test]
        public void TheCooldownIsSpentOnABurnThatLanded()
        {
            // A sweep crosses a distant ship in a fraction of its damage interval. Ticking the cooldown on time
            // alone would let the beam pass straight through almost everyone it touched.
            var sweep = new BeamSweep(180f, 180f, 2f, 0.5f);

            for (var i = 0; i < 40; i++) sweep.Tick(0.02f); // 0.8 s of sweeping, touching nobody
            Assert.That(sweep.CanBurn, Is.True, "it is still ready for the first ship it crosses");

            sweep.Burned();
            Assert.That(sweep.CanBurn, Is.False);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeamSweep(0f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeamSweep(0f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeamSweep(0f, 1f, 1f, 1f).Tick(-1f));
        }
    }

    public class GravityPullTests
    {
        [Test]
        public void ItPullsHardestAtTheCentreAndNotAtAllOutsideItsReach()
        {
            Assert.That(BossPatterns.PullStrength(0f, 10f, 4f), Is.EqualTo(4f));
            Assert.That(BossPatterns.PullStrength(5f, 10f, 4f), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(BossPatterns.PullStrength(10f, 10f, 4f), Is.EqualTo(0f));
            Assert.That(BossPatterns.PullStrength(50f, 10f, 4f), Is.EqualTo(0f));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPatterns.PullStrength(1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BossPatterns.PullStrength(1f, 1f, -1f));
        }
    }
}
