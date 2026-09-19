using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class ShieldTests
    {
        [Test]
        public void NewShield_IsInactive()
        {
            var shield = new Shield();

            Assert.That(shield.IsActive, Is.False);
            Assert.That(shield.IsFlickering, Is.False);
            Assert.That(shield.TryAbsorbHit(), Is.False);
        }

        [Test]
        public void Activate_UsesGddDefaults()
        {
            var shield = new Shield();

            shield.Activate();

            Assert.That(shield.IsActive, Is.True);
            Assert.That(shield.HitsRemaining, Is.EqualTo(3));
            Assert.That(shield.TimeRemaining, Is.EqualTo(10f));
        }

        [Test]
        public void AbsorbsThreeHitsThenDeactivates()
        {
            var shield = new Shield();
            shield.Activate();

            Assert.That(shield.TryAbsorbHit(), Is.True);
            Assert.That(shield.TryAbsorbHit(), Is.True);
            Assert.That(shield.TryAbsorbHit(), Is.True);

            Assert.That(shield.IsActive, Is.False);
            Assert.That(shield.TimeRemaining, Is.EqualTo(0f));
            Assert.That(shield.TryAbsorbHit(), Is.False);
        }

        [Test]
        public void ExpiresAfterDurationEvenWithHitsRemaining()
        {
            var shield = new Shield();
            shield.Activate();

            shield.Tick(GameTuning.ShieldDurationSeconds - 0.1f);
            Assert.That(shield.IsActive, Is.True);

            shield.Tick(0.2f);
            Assert.That(shield.IsActive, Is.False);
            Assert.That(shield.HitsRemaining, Is.EqualTo(0));
            Assert.That(shield.TryAbsorbHit(), Is.False);
        }

        [Test]
        public void FlickersOnlyInLastTwoSeconds()
        {
            var shield = new Shield();
            shield.Activate();

            shield.Tick(GameTuning.ShieldDurationSeconds - GameTuning.ShieldFlickerSeconds - 0.1f);
            Assert.That(shield.IsFlickering, Is.False);

            shield.Tick(0.2f);
            Assert.That(shield.IsFlickering, Is.True);
        }

        [Test]
        public void Activate_WhileActive_RefreshesWithoutStacking()
        {
            var shield = new Shield();
            shield.Activate();
            shield.TryAbsorbHit();
            shield.Tick(5f);

            shield.Activate();

            Assert.That(shield.HitsRemaining, Is.EqualTo(3));
            Assert.That(shield.TimeRemaining, Is.EqualTo(10f));
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Shield(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Shield(3, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Shield(3, 10f, -1f));
        }
    }
}
