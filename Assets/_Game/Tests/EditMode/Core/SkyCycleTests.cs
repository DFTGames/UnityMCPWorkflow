using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The sky of an Endless run has to drift, not change (GDD "Core Loop", Endless mode). A run is one
    /// unbroken journey, so anything that swaps the backdrop between one frame and the next is a fault.
    /// </summary>
    public class SkyCycleTests
    {
        const float Hold = 60f;
        const float Fade = 10f;

        static SkyCycle Create(int skies = 4) => new SkyCycle(skies, Hold, Fade);

        [Test]
        public void ARunOpens_OnOneWholeSky()
        {
            var sky = Create();

            Assert.That(sky.Showing, Is.EqualTo(0));
            Assert.That(sky.Blend, Is.EqualTo(0f), "a run must not open mid-dissolve");
        }

        [Test]
        public void ASkyIsHeld_BeforeItStartsToGo()
        {
            var sky = Create();

            sky.Tick(Hold - 1f);

            Assert.That(sky.Showing, Is.EqualTo(0));
            Assert.That(sky.Blend, Is.EqualTo(0f), "the dissolve started early");
        }

        [Test]
        public void TheDissolve_RunsAcrossTheFade()
        {
            var sky = Create();
            sky.Tick(Hold);

            sky.Tick(Fade / 4f);
            Assert.That(sky.Blend, Is.EqualTo(0.25f).Within(1e-4f));

            sky.Tick(Fade / 4f);
            Assert.That(sky.Blend, Is.EqualTo(0.5f).Within(1e-4f));

            Assert.That(sky.Showing, Is.EqualTo(0), "the sky being left does not change until the dissolve ends");
            Assert.That(sky.Arriving, Is.EqualTo(1));
        }

        [Test]
        public void TheNextSky_TakesOverWhenTheDissolveEnds()
        {
            var sky = Create();

            sky.Tick(Hold + Fade);

            Assert.That(sky.Showing, Is.EqualTo(1));
            Assert.That(sky.Blend, Is.EqualTo(0f), "the new sky is whole, not still arriving");
        }

        [Test]
        public void TheRing_ComesBackToTheFirstSky()
        {
            var sky = Create(3);

            for (var i = 0; i < 3; i++) sky.Tick(Hold + Fade);

            Assert.That(sky.Showing, Is.EqualTo(0), "the set has to loop, not run out");
        }

        [Test]
        public void TheLastSky_DissolvesIntoTheFirst()
        {
            var sky = Create(3);

            sky.Tick((Hold + Fade) * 2f); // on the last of the three
            sky.Tick(Hold + Fade / 2f);

            Assert.That(sky.Showing, Is.EqualTo(2));
            Assert.That(sky.Arriving, Is.EqualTo(0), "the ring has a seam: the last sky cuts back to the first");
            Assert.That(sky.Blend, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void OneSky_NeverGoesAnywhere()
        {
            var sky = Create(1);

            sky.Tick((Hold + Fade) * 3.5f);

            Assert.That(sky.Showing, Is.EqualTo(0));
            Assert.That(sky.Arriving, Is.EqualTo(0));
        }

        [Test]
        public void ManySmallTicks_ArriveWhereOneBigOneDoes()
        {
            var stepped = Create();
            var jumped = Create();

            for (var i = 0; i < 500; i++) stepped.Tick(0.02f); // 10 seconds at the fixed step
            jumped.Tick(10f);

            Assert.That(stepped.Showing, Is.EqualTo(jumped.Showing));
            Assert.That(stepped.Blend, Is.EqualTo(jumped.Blend).Within(1e-3f));
        }

        [Test]
        public void AStall_LandsWhereItShould()
        {
            var sky = Create(4);

            // The editor losing focus, or a breakpoint: one enormous step rather than many small ones.
            sky.Tick((Hold + Fade) * 9f + Hold + Fade / 2f);

            Assert.That(sky.Showing, Is.EqualTo(9 % 4));
            Assert.That(sky.Blend, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void TimeThatDoesNotMove_ChangesNothing()
        {
            var sky = Create();
            sky.Tick(Hold + Fade / 2f); // halfway through the first dissolve
            var showing = sky.Showing;
            var blend = sky.Blend;

            sky.Tick(0f);
            sky.Tick(-5f);
            sky.Tick(float.NaN);

            Assert.That(sky.Showing, Is.EqualTo(showing));
            Assert.That(sky.Blend, Is.EqualTo(blend), "a dissolve must not creep while time stands still");
        }

        [Test]
        public void ARunCanOpen_OnAnySky()
        {
            Assert.That(new SkyCycle(4, Hold, Fade, 2).Showing, Is.EqualTo(2));
            Assert.That(new SkyCycle(4, Hold, Fade, 6).Showing, Is.EqualTo(2), "an index past the end wraps");
            Assert.That(new SkyCycle(4, Hold, Fade, -1).Showing, Is.EqualTo(3), "and so does one before it");
        }

        [Test]
        public void ASkyRing_RefusesToCut()
        {
            // The whole point of the ring: a fade of nothing is the hard swap this replaced.
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkyCycle(4, Hold, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkyCycle(4, Hold, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkyCycle(0, Hold, Fade));
        }
    }
}
