using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class WeaponTests
    {
        const float AngleTolerance = 0.01f;

        static Weapon AtLevel(int level)
        {
            var weapon = new Weapon();
            while (weapon.Level < level) weapon.Upgrade();
            return weapon;
        }

        static List<ShotSpec> FireOnce(Weapon weapon, Vector2 aim, int playerIndex = 0)
        {
            var shots = new List<ShotSpec>();
            weapon.Tick(0f, true, aim, playerIndex, shots);
            return shots;
        }

        static void AssertAngles(List<ShotSpec> shots, params float[] expectedDegrees)
        {
            Assert.That(shots.Count, Is.EqualTo(expectedDegrees.Length));
            var angles = shots.ConvertAll(s => AngleDegrees(s.Direction));
            foreach (var expected in expectedDegrees)
                Assert.That(angles, Has.Some.EqualTo(expected).Within(AngleTolerance), $"missing {expected} degrees");
        }

        [Test]
        public void StartsAtLevelOne()
        {
            Assert.That(new Weapon().Level, Is.EqualTo(1));
        }

        [Test]
        public void Upgrade_StopsAtMaxLevelAndReportsIt()
        {
            var weapon = AtLevel(5);

            Assert.That(weapon.IsMaxLevel, Is.True);
            Assert.That(weapon.Upgrade(), Is.False);
            Assert.That(weapon.Level, Is.EqualTo(5));
        }

        [Test]
        public void Downgrade_DropsOneLevelButNeverBelowOne()
        {
            var weapon = AtLevel(3);

            weapon.Downgrade();
            Assert.That(weapon.Level, Is.EqualTo(2));

            weapon.Downgrade();
            weapon.Downgrade();
            Assert.That(weapon.Level, Is.EqualTo(1));
        }

        [TestCase(1, 8f, 1)]
        [TestCase(2, 8f, 2)]
        [TestCase(3, 8f, 3)]
        [TestCase(4, 11f, 3)]
        [TestCase(5, 11f, 5)]
        public void LevelTable_MatchesGdd(int level, float fireRate, int shots)
        {
            var weapon = AtLevel(level);

            Assert.That(weapon.FireRate, Is.EqualTo(fireRate));
            Assert.That(weapon.ShotsPerVolley, Is.EqualTo(shots));
            Assert.That(FireOnce(weapon, Vector2.UnitX).Count, Is.EqualTo(shots));
        }

        [Test]
        public void LevelOne_FiresSingleNormalisedShotAlongAim()
        {
            var shots = FireOnce(new Weapon(), new Vector2(3f, 3f), playerIndex: 2);

            var diagonal = Vector2.Normalize(new Vector2(1f, 1f));
            AssertVector(diagonal, shots[0].Direction);
            AssertVector(Vector2.Zero, shots[0].Offset);
            Assert.That(shots[0].Piercing, Is.False);
            Assert.That(shots[0].PlayerIndex, Is.EqualTo(2));
        }

        [Test]
        public void LevelTwo_FiresTwoParallelShotsOffsetPerpendicularToAim()
        {
            var shots = FireOnce(AtLevel(2), Vector2.UnitX);

            foreach (var shot in shots)
            {
                AssertVector(Vector2.UnitX, shot.Direction);
                Assert.That(shot.Offset.X, Is.EqualTo(0f).Within(Tolerance));
            }

            Assert.That(shots[0].Offset.Y - shots[1].Offset.Y, Is.EqualTo(Weapon.ParallelShotSpacing).Within(Tolerance));
        }

        [TestCase(3)]
        [TestCase(4)]
        public void LevelsThreeAndFour_SpreadTenDegrees(int level)
        {
            AssertAngles(FireOnce(AtLevel(level), Vector2.UnitX), 10f, 0f, -10f);
        }

        [Test]
        public void LevelFive_SpreadsTwentyDegreesAndOnlyCentreShotPierces()
        {
            var shots = FireOnce(AtLevel(5), Vector2.UnitX);

            AssertAngles(shots, 20f, 10f, 0f, -10f, -20f);
            var piercing = shots.FindAll(s => s.Piercing);
            Assert.That(piercing.Count, Is.EqualTo(1));
            Assert.That(AngleDegrees(piercing[0].Direction), Is.EqualTo(0f).Within(AngleTolerance));
        }

        [Test]
        public void Spread_IsRelativeToAimDirection()
        {
            AssertAngles(FireOnce(AtLevel(3), Vector2.UnitY), 100f, 90f, 80f);
        }

        [Test]
        public void FiresImmediatelyThenAtFireRate()
        {
            var weapon = new Weapon();
            var shots = new List<ShotSpec>();

            Assert.That(weapon.Tick(0.01f, true, Vector2.UnitX, 0, shots), Is.EqualTo(1));
            Assert.That(weapon.Tick(0.1f, true, Vector2.UnitX, 0, shots), Is.EqualTo(0));
            Assert.That(weapon.Tick(0.03f, true, Vector2.UnitX, 0, shots), Is.EqualTo(1));
        }

        [TestCase(1, 80)]
        [TestCase(4, 110)]
        public void SustainedFire_MatchesFireRateOverTenSeconds(int level, int expectedVolleys)
        {
            var weapon = AtLevel(level);
            var shots = new List<ShotSpec>();
            var volleys = 0;

            for (var i = 0; i < 600; i++)
                volleys += weapon.Tick(1f / 60f, true, Vector2.UnitX, 0, shots);

            Assert.That(volleys, Is.InRange(expectedVolleys - 1, expectedVolleys + 1));
        }

        [Test]
        public void NotFiring_ProducesNoShotsAndDoesNotBankCooldown()
        {
            var weapon = new Weapon();
            var shots = new List<ShotSpec>();

            Assert.That(weapon.Tick(5f, false, Vector2.UnitX, 0, shots), Is.EqualTo(0));
            Assert.That(weapon.Tick(0f, true, Vector2.UnitX, 0, shots), Is.EqualTo(1));
            Assert.That(shots.Count, Is.EqualTo(1));
        }

        [Test]
        public void ZeroAim_DoesNotFire()
        {
            var shots = new List<ShotSpec>();

            Assert.That(new Weapon().Tick(0.1f, true, Vector2.Zero, 0, shots), Is.EqualTo(0));
            Assert.That(shots, Is.Empty);
        }

        [Test]
        public void LongTick_FiresExactlyTheCatchUpCap()
        {
            var weapon = new Weapon();
            var shots = new List<ShotSpec>();
            weapon.Tick(0f, true, Vector2.UnitX, 0, shots);

            Assert.That(weapon.Tick(2f, true, Vector2.UnitX, 0, shots), Is.EqualTo(Weapon.MaxVolleysPerTick));
        }

        [Test]
        public void NullShotList_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new Weapon().Tick(0f, true, Vector2.UnitX, 0, null));
        }
    }
}
