using System;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class HoldingPatternTests
    {
        static readonly Vector2 Start = new Vector2(12f, 1f);

        [Test]
        public void ItFliesInUntilItReachesItsStation()
        {
            AssertVector(new Vector2(10f, 1f), HoldingPattern.Evaluate(Start, 4f, 6f, 0.5f));
            AssertVector(new Vector2(8f, 1f), HoldingPattern.Evaluate(Start, 4f, 6f, 1f));
        }

        [Test]
        public void ThenItStopsAndStays()
        {
            AssertVector(new Vector2(6f, 1f), HoldingPattern.Evaluate(Start, 4f, 6f, 1.5f));
            AssertVector(new Vector2(6f, 1f), HoldingPattern.Evaluate(Start, 4f, 6f, 30f), Tolerance,
                "a gunship holds its station rather than drifting off screen");
        }

        [Test]
        public void ArrivalIsWhenItStops()
        {
            Assert.That(HoldingPattern.HasArrived(Start, 4f, 6f, 1f), Is.False);
            Assert.That(HoldingPattern.HasArrived(Start, 4f, 6f, 1.5f), Is.True);
        }

        [Test]
        public void AStationaryEnemyHasAlreadyArrived()
        {
            Assert.That(HoldingPattern.HasArrived(Start, 0f, 6f, 0f), Is.True);
        }

        [Test]
        public void TheStationIsAFractionInFromTheRightEdge()
        {
            var field = new Playfield(-10f, 10f, -5f, 5f);

            Assert.That(HoldingPattern.StationFor(field, 0f), Is.EqualTo(10f));
            Assert.That(HoldingPattern.StationFor(field, 1f / 3f), Is.EqualTo(10f - 20f / 3f).Within(1e-4f));
            Assert.That(HoldingPattern.StationFor(field, 1f), Is.EqualTo(-10f));
        }

        [Test]
        public void NegativeTime_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HoldingPattern.Evaluate(Start, 4f, 6f, -1f));
        }
    }

    public class DiveAttackTests
    {
        static DiveAttack Create() => new DiveAttack(0.5f, 0.6f, 12f);

        static readonly Vector2 Self = new Vector2(6f, 2f);
        static readonly Vector2 Player = new Vector2(-4f, -1f);

        [Test]
        public void ItEntersThenLocksThenCharges()
        {
            var dive = Create();
            Assert.That(dive.Phase, Is.EqualTo(DivePhase.Entering));

            dive.Tick(0.5f, Self, Player);
            Assert.That(dive.Phase, Is.EqualTo(DivePhase.Locking));

            dive.Tick(0.6f, Self, Player);
            Assert.That(dive.Phase, Is.EqualTo(DivePhase.Charging));
        }

        [Test]
        public void WhileLockingItHoldsStill()
        {
            // Stopping is the tell that tells the player to move.
            var dive = Create();
            dive.Tick(0.5f, Self, Player);

            Assert.That(dive.Step(0.02f, 3f), Is.EqualTo(Vector2.Zero));
            Assert.That(dive.LockProgress, Is.Zero.Within(1e-4f));

            dive.Tick(0.3f, Self, Player);
            Assert.That(dive.LockProgress, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void TheChargeCommitsToWhereThePlayerWas()
        {
            var dive = Create();
            dive.Tick(0.5f, Self, Player);
            dive.Tick(0.6f, Self, Player);
            var committed = dive.ChargeDirection;

            // The player runs; the charge does not follow.
            dive.Tick(0.1f, Self, new Vector2(-4f, 4f));

            AssertVector(committed, dive.ChargeDirection, 1e-5f, "a locked charge cannot be steered");
            Assert.That(committed.Length(), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ChargingIsFasterThanCruising()
        {
            var dive = Create();
            var cruise = dive.Step(0.1f, 3f).Length();

            dive.Tick(0.5f, Self, Player);
            dive.Tick(0.6f, Self, Player);
            var charge = dive.Step(0.1f, 3f).Length();

            Assert.That(charge, Is.GreaterThan(cruise));
            Assert.That(charge, Is.EqualTo(1.2f).Within(1e-4f)); // 12 units/s for 0.1 s
        }

        [Test]
        public void AimingAtItself_ChargesForward()
        {
            var dive = Create();
            dive.Tick(0.5f, Self, Self);
            dive.Tick(0.6f, Self, Self);

            AssertVector(-Vector2.UnitX, dive.ChargeDirection, 1e-4f);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiveAttack(-1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiveAttack(1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiveAttack(1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create().Tick(-0.1f, Self, Player));
        }
    }

    public class FrontShieldTests
    {
        static readonly Vector2 FacingLeft = -Vector2.UnitX;

        [Test]
        public void AShotIntoItsNoseIsTurnedAway()
        {
            // The frigate faces left; the player is to its left, so a straight shot lands on the shield.
            Assert.That(FrontShield.Blocks(FacingLeft, new Vector2(-1f, 0f)), Is.True);
        }

        [Test]
        public void AShotFromBehindGetsThrough()
        {
            Assert.That(FrontShield.Blocks(FacingLeft, new Vector2(1f, 0f)), Is.False);
        }

        [Test]
        public void AShotFromTheSideGetsThrough()
        {
            Assert.That(FrontShield.Blocks(FacingLeft, new Vector2(0f, 1f)), Is.False,
                "which is what the twin-stick aiming is for");
            Assert.That(FrontShield.Blocks(FacingLeft, new Vector2(0f, -1f)), Is.False);
        }

        [Test]
        public void TheArcEdgeIsWhereItStopsBlocking()
        {
            var justInside = Direction(180f - FrontShield.ArcDegrees + 2f);
            var justOutside = Direction(180f - FrontShield.ArcDegrees - 2f);

            Assert.That(FrontShield.Blocks(FacingLeft, justInside), Is.True);
            Assert.That(FrontShield.Blocks(FacingLeft, justOutside), Is.False);
        }

        [Test]
        public void ADegenerateHitIsNotBlocked()
        {
            Assert.That(FrontShield.Blocks(FacingLeft, Vector2.Zero), Is.False);
            Assert.That(FrontShield.Blocks(Vector2.Zero, new Vector2(-1f, 0f)), Is.False);
        }

        [Test]
        public void AnImpossibleArc_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FrontShield.Blocks(FacingLeft, -Vector2.UnitX, 200f));
        }

        static Vector2 Direction(float degrees)
        {
            var radians = degrees * MathF.PI / 180f;
            return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        }
    }

    public class SniperShotTests
    {
        static SniperShot Create() => new SniperShot(1.2f, 0.25f, 1.5f);

        static readonly Vector2 Self = new Vector2(8f, 0f);
        static readonly Vector2 Player = new Vector2(-6f, 2f);

        [Test]
        public void ItWarnsBeforeItFires()
        {
            var sniper = Create();
            Assert.That(sniper.IsWarning, Is.True);
            Assert.That(sniper.IsFiring, Is.False);

            sniper.Tick(1.1f, Self, Player);
            Assert.That(sniper.IsFiring, Is.False, "the player still has time to move");

            sniper.Tick(0.2f, Self, Player);
            Assert.That(sniper.IsFiring, Is.True);
        }

        [Test]
        public void TheWarningShowsWhereTheShotWillGo()
        {
            var sniper = Create();
            sniper.Tick(0.6f, Self, Player);
            Assert.That(sniper.WarningProgress, Is.EqualTo(0.5f).Within(1e-3f));

            // The line is taken as the warning begins, so what is drawn is what will be fired.
            var expected = EnemyMotion.AimAt(Self, Player, -Vector2.UnitX);
            AssertVector(expected, sniper.Aim, 1e-4f);

            sniper.Tick(0.7f, Self, Player); // fires
            Assert.That(sniper.IsFiring, Is.True);
            AssertVector(expected, sniper.Aim, 1e-4f);
        }

        [Test]
        public void MovingDuringTheWarningIsHowYouDodgeIt()
        {
            // The whole fight: the line is fixed when the warning starts and never corrects for where the
            // player went (GDD "Enemies and Hazards", the new enemies' rules).
            var sniper = Create();
            sniper.Tick(0.1f, Self, Player);
            var aim = sniper.Aim;

            var moved = new Vector2(-6f, -9f);
            sniper.Tick(1.2f, Self, moved); // the warning ends and the beam goes live

            Assert.That(sniper.IsFiring, Is.True);
            AssertVector(aim, sniper.Aim, 1e-5f);
            Assert.That(SniperShot.HitsPoint(Self, sniper.Aim, moved, 0.15f, 40f), Is.False,
                "a player who moved off the line is missed");
        }

        [Test]
        public void AFiredBeamDoesNotFollowThePlayer()
        {
            var sniper = Create();
            sniper.Tick(1.3f, Self, Player);
            var aim = sniper.Aim;

            sniper.Tick(0.1f, Self, new Vector2(-6f, -5f));

            AssertVector(aim, sniper.Aim, 1e-5f);
        }

        [Test]
        public void EachWarningTakesAFreshLine()
        {
            var sniper = Create();
            sniper.Tick(1.3f, Self, Player);   // firing at the first target
            var first = sniper.Aim;
            sniper.Tick(0.3f, Self, Player);   // beam over
            sniper.Tick(1.6f, Self, Player);   // recovered: warning again

            Assert.That(sniper.IsWarning, Is.True);
            var moved = new Vector2(-6f, -8f);
            sniper.Tick(0.1f, Self, moved);

            Assert.That(sniper.Aim, Is.Not.EqualTo(first), "it aims again where the player is now");
            AssertVector(EnemyMotion.AimAt(Self, moved, -Vector2.UnitX), sniper.Aim, 1e-4f);
        }

        [Test]
        public void ItRecoversAndWarnsAgain()
        {
            var sniper = Create();
            sniper.Tick(1.3f, Self, Player);   // firing
            sniper.Tick(0.3f, Self, Player);   // beam over
            Assert.That(sniper.IsFiring, Is.False);
            Assert.That(sniper.IsWarning, Is.False, "recovering");

            sniper.Tick(1.6f, Self, Player);
            Assert.That(sniper.IsWarning, Is.True, "and lines up the next shot");
        }

        [Test]
        public void TheBeamHitsWhatIsOnItsLine()
        {
            var origin = new Vector2(5f, 0f);
            var aim = -Vector2.UnitX;

            Assert.That(SniperShot.HitsPoint(origin, aim, new Vector2(0f, 0f), 0.3f, 20f), Is.True);
            Assert.That(SniperShot.HitsPoint(origin, aim, new Vector2(0f, 0.2f), 0.3f, 20f), Is.True);
        }

        [Test]
        public void TheBeamMissesWhatIsNotOnIt()
        {
            var origin = new Vector2(5f, 0f);
            var aim = -Vector2.UnitX;

            Assert.That(SniperShot.HitsPoint(origin, aim, new Vector2(0f, 1f), 0.3f, 20f), Is.False, "too far off the line");
            Assert.That(SniperShot.HitsPoint(origin, aim, new Vector2(8f, 0f), 0.3f, 20f), Is.False, "behind the sniper");
            Assert.That(SniperShot.HitsPoint(origin, aim, new Vector2(-30f, 0f), 0.3f, 20f), Is.False, "beyond its reach");
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SniperShot(0f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SniperShot(1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SniperShot(1f, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SniperShot.HitsPoint(Vector2.Zero, -Vector2.UnitX, Vector2.Zero, 0f, 1f));
        }
    }

    public class ProximityMineTests
    {
        static ProximityMine Create() => MineSpec.Create();

        [Test]
        public void TheSpecMatchesTheDesign()
        {
            // GDD "Enemies and Hazards": arms after 0.6 s, triggers within 1.5 units, 25 damage, gone after 12 s.
            Assert.That(MineSpec.ArmSeconds, Is.EqualTo(0.6f));
            Assert.That(MineSpec.TriggerRadius, Is.EqualTo(1.5f));
            Assert.That(MineSpec.Damage, Is.EqualTo(25f));
            Assert.That(MineSpec.LifetimeSeconds, Is.EqualTo(12f));
        }

        [Test]
        public void ItDoesNotGoOffBeforeItIsArmed()
        {
            // Otherwise the Mine Layer would kill the ship chasing it the moment it dropped one.
            var mine = Create();
            mine.Tick(0.3f);

            Assert.That(mine.IsArmed, Is.False);
            Assert.That(mine.ShouldDetonate(0.1f), Is.False);
        }

        [Test]
        public void OnceArmedItGoesOffWhenThePlayerIsClose()
        {
            var mine = Create();
            mine.Tick(0.6f);

            Assert.That(mine.IsArmed, Is.True);
            Assert.That(mine.ShouldDetonate(1.4f), Is.True);
            Assert.That(mine.ShouldDetonate(1.6f), Is.False, "and leaves a wide berth alone");
        }

        [Test]
        public void ItExpiresIfNobodyComesNear()
        {
            var mine = Create();
            mine.Tick(12f);

            Assert.That(mine.HasExpired, Is.True);
            Assert.That(mine.ShouldDetonate(0f), Is.False, "an expired mine is inert");
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProximityMine(-1f, 1f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProximityMine(1f, 0f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProximityMine(5f, 1f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create().Tick(-1f));
        }
    }

    public class BurstFireTests
    {
        static BurstFire Create() => new BurstFire(3, 0.15f, 2f, 0.5f);

        [Test]
        public void ItFiresThreeShotsCloseTogether()
        {
            var burst = Create();
            Assert.That(burst.Tick(0.5f), Is.True, "first shot after the initial delay");
            Assert.That(burst.Tick(0.15f), Is.True);
            Assert.That(burst.Tick(0.15f), Is.True);

            Assert.That(burst.Tick(0.15f), Is.False, "then it waits");
        }

        [Test]
        public void ThenItWaitsMuchLongerBeforeTheNextBurst()
        {
            var burst = Create();
            for (var i = 0; i < 3; i++) burst.Tick(i == 0 ? 0.5f : 0.15f);

            Assert.That(burst.Tick(1.9f), Is.False);
            Assert.That(burst.Tick(0.2f), Is.True, "the next burst starts");
        }

        [Test]
        public void ALongFrameStillOnlyFiresOnce()
        {
            // The same rule as FireTimer: a hitch must not empty the magazine in one step.
            var burst = Create();

            Assert.That(burst.Tick(10f), Is.True);
            Assert.That(burst.FiredInBurst, Is.EqualTo(1));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstFire(0, 0.1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstFire(3, 0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstFire(3, 1f, 0.5f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstFire(3, 0.1f, 1f, -1f));
        }
    }
}
