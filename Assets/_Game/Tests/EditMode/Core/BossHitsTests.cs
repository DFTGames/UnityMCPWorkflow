using System.Numerics;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// Who takes a shot when the armour and the core are both in the way.
    /// </summary>
    /// <remarks>
    /// This replaced a rule that said they must never be in the way at once. That rule was enforced by a
    /// test on the prefabs, and enforcing it meant moving the core off where the art had put it on six of
    /// the eight bosses. The fight is about the lit circle, so the lit circle is what a shot landing on it
    /// should hit, and the armour underneath is now beside the point.
    /// </remarks>
    public class BossHitsTests
    {
        static readonly Vector2 Core = new Vector2(3f, -1f);
        const float Radius = 0.5f;

        [Test]
        public void AShotOnAnOpenCore_HitsTheCore_EvenWhereArmourCoversIt()
        {
            // Dead centre, and just inside the edge. The armour is not a parameter at all any more, which is
            // the whole point: it cannot take a shot away from an open core however much it overlaps.
            Assert.That(BossHits.CoreTakesIt(Core, Core, Radius, coreOpen: true), Is.True);
            Assert.That(BossHits.CoreTakesIt(new Vector2(3.49f, -1f), Core, Radius, true), Is.True);
            Assert.That(BossHits.CoreTakesIt(new Vector2(3f, -0.51f), Core, Radius, true), Is.True);
        }

        [Test]
        public void AShotThatMissedTheCore_LeavesItToTheArmour()
        {
            Assert.That(BossHits.CoreTakesIt(new Vector2(3.51f, -1f), Core, Radius, true), Is.False);
            Assert.That(BossHits.CoreTakesIt(new Vector2(0f, 0f), Core, Radius, true), Is.False);

            // Diagonally out: inside the bounding square, outside the circle. A box check here would be
            // wrong in exactly this corner.
            Assert.That(BossHits.CoreTakesIt(new Vector2(3.4f, -0.6f), Core, Radius, true), Is.False);
        }

        /// <summary>
        /// A closed core is shutters, not a weak point. The shot is worth what hitting the boss anywhere
        /// else is worth, and giving the player the core's explosion would teach them the opposite of the
        /// one mechanic the fight has.
        /// </summary>
        [Test]
        public void AClosedCore_TakesNothing_HoweverWellAimed()
        {
            Assert.That(BossHits.CoreTakesIt(Core, Core, Radius, coreOpen: false), Is.False);
        }

        /// <summary>
        /// A boss with no core collider, or one that has not been sized, must not turn every hull hit into a
        /// core hit. Distance to a zero-radius circle is zero at its centre, so without the guard a missing
        /// collider would make the boss die to a shot landing on one exact point.
        /// </summary>
        [Test]
        public void ACoreWithNoSize_IsNotAHitbox()
        {
            Assert.That(BossHits.CoreTakesIt(Core, Core, 0f, true), Is.False);
            Assert.That(BossHits.CoreTakesIt(Core, Core, -1f, true), Is.False);
        }

        /// <summary>
        /// The boundary itself counts as a hit. A shot exactly on the edge should not fall through to the
        /// armour: the player aimed at the circle and reached it.
        /// </summary>
        [Test]
        public void TheEdgeOfTheCore_IsStillTheCore()
        {
            Assert.That(BossHits.CoreTakesIt(new Vector2(Core.X + Radius, Core.Y), Core, Radius, true), Is.True);
        }
    }
}
