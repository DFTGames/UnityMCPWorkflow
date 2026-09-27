using NUnit.Framework;
using UnityEngine;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The beam's shimmer and its warning. Tested here because the failures are the kind nobody can judge by
    /// eye at sixty frames a second: a flicker that touches zero, a width that goes negative and turns the
    /// beam inside out, or a warning that is already bright when it should still be a hint.
    /// </summary>
    public class BeamLookTests
    {
        /// <summary>
        /// Sampled densely across several seconds, because a wave that misbehaves does it at particular
        /// moments and a handful of round numbers is exactly where two sine waves both read zero.
        /// </summary>
        static float[] Moments()
        {
            var moments = new float[2000];
            for (var i = 0; i < moments.Length; i++) moments[i] = i * 0.0037f;

            return moments;
        }

        [Test]
        public void TheWidth_ShimmersWithoutEverCollapsing()
        {
            foreach (var t in Moments())
            {
                var width = BeamLook.WidthAt(t);

                Assert.That(width, Is.GreaterThan(0f), $"the beam turned inside out at {t}s");
                Assert.That(width, Is.InRange(1f - BeamLook.WidthSwing - 0.001f, 1f + BeamLook.WidthSwing + 0.001f),
                    $"the width swung further than the art direction allows at {t}s");
            }
        }

        [Test]
        public void TheBrightness_FlickersButNeverBlinksOut()
        {
            foreach (var t in Moments())
                Assert.That(BeamLook.BrightnessAt(t),
                    Is.InRange(BeamLook.DimmestWhileFiring - 0.001f, 1.001f),
                    $"the beam blinked out, or outshone its own colour, at {t}s");
        }

        /// <summary>A beam that never varies is a rectangle, which is what this replaced.</summary>
        [Test]
        public void TheBeam_ActuallyMoves()
        {
            float widest = 0f, narrowest = 2f, brightest = 0f, dimmest = 2f;

            foreach (var t in Moments())
            {
                var w = BeamLook.WidthAt(t);
                var b = BeamLook.BrightnessAt(t);

                if (w > widest) widest = w;
                if (w < narrowest) narrowest = w;
                if (b > brightest) brightest = b;
                if (b < dimmest) dimmest = b;
            }

            Assert.That(widest - narrowest, Is.GreaterThan(0.05f), "the width is effectively static");
            Assert.That(brightest - dimmest, Is.GreaterThan(0.05f), "the brightness is effectively static");
        }

        [Test]
        public void TheWarning_StartsAtNothingAndEndsAtEverything()
        {
            Assert.That(BeamLook.WarningAt(0f), Is.EqualTo(0f));
            Assert.That(BeamLook.WarningAt(1f), Is.EqualTo(1f));
        }

        /// <summary>
        /// The warning is the whole fight, so it has to say how close the shot is. A line that fades up
        /// evenly is bright enough to ignore for most of its life; this one stays faint and then rushes.
        /// </summary>
        [Test]
        public void TheWarning_RushesLate_RatherThanFadingEvenly()
        {
            Assert.That(BeamLook.WarningAt(0.5f), Is.LessThan(0.35f),
                "half way through the telegraph it should still be a hint");

            var firstHalf = BeamLook.WarningAt(0.5f) - BeamLook.WarningAt(0f);
            var secondHalf = BeamLook.WarningAt(1f) - BeamLook.WarningAt(0.5f);

            Assert.That(secondHalf, Is.GreaterThan(firstHalf * 2f),
                "the last half of the warning must be the loud one");
        }

        [Test]
        public void TheWarning_OnlyEverGrows()
        {
            var previous = -1f;

            for (var t = 0f; t <= 1f; t += 0.01f)
            {
                var now = BeamLook.WarningAt(t);

                Assert.That(now, Is.GreaterThanOrEqualTo(previous), $"the warning dimmed at {t}");
                previous = now;
            }
        }

        /// <summary>
        /// Measured against the beam itself rather than against a number chosen here. The difference
        /// between "about to fire" and "firing" has to be visible at a glance, because one of them is
        /// survivable, and that only holds if the fattest the warning ever gets is still thinner than the
        /// thinnest the beam ever gets. An earlier version compared against a constant nothing in the
        /// system used, which would have stayed green through any retuning of either side.
        /// </summary>
        [Test]
        public void TheWarningLine_IsAlwaysThinnerThanTheShot()
        {
            var fattestWarning = 0f;
            for (var t = 0f; t <= 1f; t += 0.01f)
                fattestWarning = Mathf.Max(fattestWarning, BeamLook.WarningWidthAt(t));

            var thinnestBeam = 2f;
            foreach (var t in Moments()) thinnestBeam = Mathf.Min(thinnestBeam, BeamLook.WidthAt(t));

            Assert.That(fattestWarning, Is.LessThan(thinnestBeam),
                $"the warning reaches {fattestWarning:0.00} of the beam's width and the beam thins to " +
                $"{thinnestBeam:0.00}, so at some point the telegraph is as fat as the shot");

            Assert.That(BeamLook.WarningWidthAt(1f), Is.GreaterThan(BeamLook.WarningWidthAt(0f)),
                "it should thicken as the shot approaches");
        }

        /// <summary>
        /// Nothing here may flicker in the 15 to 25 Hz band, which is the one most associated with
        /// photosensitive seizures, and a bright beam is exactly the wrong thing to put there. It also has
        /// to stay slow enough that the Sniper's fixed step at 50 Hz samples it honestly rather than
        /// aliasing it into a slow irregular beat.
        /// </summary>
        [Test]
        public void NothingFlickers_InTheBandThatCausesSeizures()
        {
            Assert.That(BeamLook.FastestHertz, Is.LessThan(12f),
                "a bright beam must stay well clear of 15 to 25 Hz");

            const float fixedStepHertz = 50f;
            Assert.That(BeamLook.FastestHertz, Is.LessThan(fixedStepHertz / 4f),
                "too fast for the fixed step to sample without aliasing into a strobe");
        }

        /// <summary>Nothing outside the telegraph's own span may produce a stranger answer than its ends.</summary>
        [Test]
        public void ProgressOutsideItsRange_IsClamped()
        {
            Assert.That(BeamLook.WarningAt(-5f), Is.EqualTo(0f));
            Assert.That(BeamLook.WarningAt(12f), Is.EqualTo(1f));
            Assert.That(BeamLook.WarningWidthAt(-1f), Is.EqualTo(BeamLook.WarningWidthAt(0f)));
            Assert.That(BeamLook.WarningWidthAt(9f), Is.EqualTo(BeamLook.WarningWidthAt(1f)));
        }
    }
}
