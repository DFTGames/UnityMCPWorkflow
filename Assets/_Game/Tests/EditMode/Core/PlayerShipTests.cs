using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class PlayerShipTests
    {
        const float Invulnerability = GameTuning.RespawnInvulnerabilitySeconds;

        static readonly PlayerCommand NoInput = default;

        static PlayerShip Create(DifficultySettings settings = null) =>
            new PlayerShip(0, settings ?? DifficultySettings.Pilot);

        static void Wait(PlayerShip ship, float seconds) => ship.Tick(seconds, NoInput, new List<ShotSpec>());

        static void LoseLife(PlayerShip ship)
        {
            ship.TakeHit(1000f);
            Wait(ship, Invulnerability + 0.01f);
        }

        [Test]
        public void TakeHit_WithoutShield_DamagesHealth()
        {
            var ship = Create();

            Assert.That(ship.TakeHit(30f), Is.EqualTo(HitOutcome.Damaged));
            Assert.That(ship.Vitals.Health, Is.EqualTo(70f).Within(1e-4f));
        }

        [Test]
        public void TakeHit_WithShield_IsAbsorbedAndHealthUntouched()
        {
            var ship = Create();
            ship.Collect(PickupType.Shield);

            Assert.That(ship.TakeHit(30f), Is.EqualTo(HitOutcome.Absorbed));
            Assert.That(ship.Vitals.Health, Is.EqualTo(100f));
            Assert.That(ship.Shield.HitsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void TakeHit_AfterShieldExpires_GoesToHealth()
        {
            var ship = Create();
            ship.Collect(PickupType.Shield);
            Wait(ship, GameTuning.ShieldDurationSeconds + 0.01f);

            Assert.That(ship.TakeHit(30f), Is.EqualTo(HitOutcome.Damaged));
        }

        [Test]
        public void TakeHit_WhileInvulnerable_DoesNotUseShield()
        {
            var ship = Create();
            ship.TakeHit(100f);
            ship.Collect(PickupType.Shield);

            Assert.That(ship.TakeHit(30f), Is.EqualTo(HitOutcome.Ignored));
            Assert.That(ship.Shield.HitsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void LosingALife_DropsWeaponOneLevel()
        {
            var ship = Create();
            ship.Collect(PickupType.WeaponUpgrade);
            ship.Collect(PickupType.WeaponUpgrade);

            Assert.That(ship.TakeHit(100f), Is.EqualTo(HitOutcome.LifeLost));
            Assert.That(ship.Weapon.Level, Is.EqualTo(2));
        }

        [Test]
        public void LosingLastLife_IsGameOverAndDropsWeapon()
        {
            var ship = Create(DifficultySettings.Ace);
            ship.Collect(PickupType.WeaponUpgrade);
            ship.Collect(PickupType.WeaponUpgrade);
            ship.Collect(PickupType.WeaponUpgrade);
            LoseLife(ship);

            Assert.That(ship.TakeHit(100f), Is.EqualTo(HitOutcome.GameOver));
            Assert.That(ship.IsGameOver, Is.True);
            Assert.That(ship.Weapon.Level, Is.EqualTo(2));
            Assert.That(ship.TakeHit(10f), Is.EqualTo(HitOutcome.Ignored));
        }

        [Test]
        public void Ram_NonBossWhileShielded_DestroysEnemyWithoutUsingShield()
        {
            var ship = Create();
            ship.Collect(PickupType.Shield);

            var result = ship.Ram(false, 40f);

            Assert.That(result.EnemyDestroyed, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Absorbed));
            Assert.That(ship.Shield.HitsRemaining, Is.EqualTo(3));
            Assert.That(ship.Vitals.Health, Is.EqualTo(100f));
        }

        [Test]
        public void Ram_BossWhileShielded_UsesOneShieldHit()
        {
            var ship = Create();
            ship.Collect(PickupType.Shield);

            var result = ship.Ram(true, 40f);

            Assert.That(result.EnemyDestroyed, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Absorbed));
            Assert.That(ship.Shield.HitsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void Ram_NonBossUnshielded_DamagesShipAndDestroysEnemy()
        {
            var ship = Create();

            var result = ship.Ram(false, 40f);

            Assert.That(result.EnemyDestroyed, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Damaged));
            Assert.That(ship.Vitals.Health, Is.EqualTo(60f).Within(1e-4f));
        }

        [Test]
        public void Ram_BossUnshielded_DamagesShipAndBossSurvives()
        {
            var ship = Create();

            var result = ship.Ram(true, 40f);

            Assert.That(result.EnemyDestroyed, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Damaged));
            Assert.That(ship.Vitals.Health, Is.EqualTo(60f).Within(1e-4f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Ram_WhileInvulnerable_PassesThroughHarmlessly(bool isBoss)
        {
            var ship = Create();
            ship.TakeHit(100f);

            var result = ship.Ram(isBoss, 40f);

            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Ignored));
            Assert.That(result.EnemyDestroyed, Is.False);
        }

        [Test]
        public void Ram_AfterGameOver_IsIgnored()
        {
            var ship = Create(DifficultySettings.Ace);
            LoseLife(ship);
            LoseLife(ship);

            var result = ship.Ram(false, 40f);

            Assert.That(result.Outcome, Is.EqualTo(HitOutcome.Ignored));
            Assert.That(result.EnemyDestroyed, Is.False);
        }

        [Test]
        public void Collect_Health_HealsByProvisionalAmount()
        {
            var ship = Create();
            ship.TakeHit(50f);

            ship.Collect(PickupType.Health);

            Assert.That(ship.Vitals.Health, Is.EqualTo(50f + GameTuning.HealthPickupAmount).Within(1e-4f));
        }

        [Test]
        public void Collect_WhileInvulnerable_StillApplies()
        {
            var ship = Create();
            ship.TakeHit(100f);

            ship.Collect(PickupType.ExtraLife);

            Assert.That(ship.Vitals.Lives, Is.EqualTo(3));
        }

        [Test]
        public void Collect_WeaponUpgradeAtMax_ReportsBonus()
        {
            var ship = Create();
            for (var i = 0; i < 4; i++)
                Assert.That(ship.Collect(PickupType.WeaponUpgrade), Is.False);

            Assert.That(ship.Collect(PickupType.WeaponUpgrade), Is.True);
            Assert.That(ship.Weapon.Level, Is.EqualTo(5));
        }

        [Test]
        public void Collect_None_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Create().Collect(PickupType.None));
        }

        [Test]
        public void Tick_AdvancesShieldAndInvulnerability()
        {
            var ship = Create();
            ship.TakeHit(100f);
            ship.Collect(PickupType.Shield);

            Wait(ship, Invulnerability + 0.01f);
            Assert.That(ship.Vitals.IsInvulnerable, Is.False);
            Assert.That(ship.Shield.IsActive, Is.True);

            Wait(ship, GameTuning.ShieldDurationSeconds);
            Assert.That(ship.Shield.IsActive, Is.False);
        }

        [Test]
        public void Tick_FiresFromCommandWithOwnerIndex()
        {
            var ship = new PlayerShip(1, DifficultySettings.Pilot);
            var shots = new List<ShotSpec>();

            ship.Tick(0.02f, new PlayerCommand(Vector2.Zero, true, Vector2.UnitX), shots);

            Assert.That(shots.Count, Is.EqualTo(1));
            Assert.That(shots[0].PlayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void Tick_ClampsFiringToTheForwardArc()
        {
            var ship = Create();
            var shots = new List<ShotSpec>();

            // Aiming straight back: a side-scroller still fires forward (GDD "Controls", Aim tilt).
            ship.Tick(0.02f, new PlayerCommand(Vector2.Zero, true, -Vector2.UnitX), shots);

            Assert.That(shots.Count, Is.EqualTo(1));
            Assert.That(shots[0].Direction.X, Is.GreaterThan(0f));
            AssertVector(Vector2.UnitX, shots[0].Direction, 1e-4f);
        }

        [Test]
        public void Tick_SteepAimFiresAtTheArcEdge()
        {
            var ship = Create();
            var shots = new List<ShotSpec>();

            ship.Tick(0.02f, new PlayerCommand(Vector2.Zero, true, Vector2.UnitY), shots);

            // 35 degrees, the arc edge: pinned literally so a regression in the clamp cannot hide here.
            Assert.That(AngleDegrees(shots[0].Direction), Is.EqualTo(35f).Within(1e-3f));
            Assert.That(shots[0].Direction.Length(), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Tick_FiringWithNoAimShootsStraightAhead()
        {
            var ship = Create();
            var shots = new List<ShotSpec>();

            // "Fire" with a dead stick still shoots: forward is the only sensible nose direction (GDD "Controls").
            ship.Tick(0.02f, new PlayerCommand(Vector2.Zero, true, Vector2.Zero), shots);

            Assert.That(shots.Count, Is.EqualTo(1));
            AssertVector(Vector2.UnitX, shots[0].Direction);
        }

        [Test]
        public void Tick_AfterGameOver_DoesNotFire()
        {
            var ship = Create(DifficultySettings.Ace);
            LoseLife(ship);
            LoseLife(ship);
            var shots = new List<ShotSpec>();

            ship.Tick(0.02f, new PlayerCommand(Vector2.Zero, true, Vector2.UnitX), shots);

            Assert.That(shots, Is.Empty);
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new PlayerShip(-1, DifficultySettings.Pilot));
            Assert.Throws<System.ArgumentNullException>(() => new PlayerShip(0, null));
        }
    }
}
