using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The menu's drifting starfield (GDD "Art Direction": star fields).
    /// </summary>
    public class StarfieldDriftTests
    {
        static StarfieldDrift Field(int count = 200, int seed = 1) => new StarfieldDrift(count, seed);

        [Test]
        public void AField_HasTheStarsItWasAskedFor()
        {
            Assert.That(Field(250).Count, Is.EqualTo(250));
            Assert.That(Field(0).Count, Is.EqualTo(0), "an empty sky is allowed, it just draws nothing");
        }

        [Test]
        public void EveryStar_StartsInsideTheView()
        {
            foreach (var star in Field().Stars)
            {
                Assert.That(star.X, Is.InRange(0f, 1f));
                Assert.That(star.Y, Is.InRange(0f, 1f));
                Assert.That(star.Size, Is.GreaterThan(0f));
                Assert.That(star.Brightness, Is.InRange(0f, 1f));
            }
        }

        /// <summary>The same seed is the same sky, or the menu would reshuffle its stars every time it opened.</summary>
        [Test]
        public void TheSameSeed_MakesTheSameSky()
        {
            var a = Field(50, 4242).Stars;
            var b = Field(50, 4242).Stars;

            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(b[i].X, Is.EqualTo(a[i].X));
                Assert.That(b[i].Y, Is.EqualTo(a[i].Y));
            }

            var other = Field(50, 99).Stars;
            Assert.That(other[0].X, Is.Not.EqualTo(a[0].X), "a different seed should be a different sky");
        }

        [Test]
        public void Drifting_MovesStarsLeftAndKeepsThemInTheView()
        {
            var field = Field(100, 7);
            var before = (Star[])field.Stars.Clone();

            field.Advance(1f);

            var moved = 0;
            for (var i = 0; i < field.Count; i++)
            {
                Assert.That(field.Stars[i].X, Is.InRange(0f, 1f), "a star drifted out of the view");
                Assert.That(field.Stars[i].Y, Is.EqualTo(before[i].Y), "stars keep their row");
                if (field.Stars[i].X < before[i].X) moved++;
            }

            Assert.That(moved, Is.GreaterThan(0), "nothing drifted at all");
        }

        /// <summary>
        /// A long first frame, or a step taken while the app was in the background, can carry a star more
        /// than a whole width. One wrap would leave it off the left of the view for the rest of the session.
        /// </summary>
        [Test]
        public void AVeryLongStep_StillLeavesEveryStarInTheView()
        {
            var field = Field(100, 3);

            field.Advance(10_000f);

            foreach (var star in field.Stars)
                Assert.That(star.X, Is.InRange(0f, 1f));
        }

        [Test]
        public void AStepOfNoTime_ChangesNothing()
        {
            var field = Field(20, 5);
            var before = (Star[])field.Stars.Clone();

            field.Advance(0f);
            field.Advance(-1f); // a paused frame must not run the sky backwards

            for (var i = 0; i < field.Count; i++)
                Assert.That(field.Stars[i].X, Is.EqualTo(before[i].X));
        }

        /// <summary>
        /// The depth of the field: stars differ in speed, and the slow ones are the small dim ones. Without
        /// that pairing the sky reads as noise rather than as distance.
        /// </summary>
        [Test]
        public void Stars_VaryInDepth_AndSpeedTracksSizeAndBrightness()
        {
            var stars = Field(300, 11).Stars;

            var slowest = stars[0];
            var fastest = stars[0];
            foreach (var star in stars)
            {
                if (star.Speed < slowest.Speed) slowest = star;
                if (star.Speed > fastest.Speed) fastest = star;
            }

            Assert.That(fastest.Speed, Is.GreaterThan(slowest.Speed * 1.5f), "the field has no spread of depth");
            Assert.That(fastest.Size, Is.GreaterThan(slowest.Size), "near stars should be the bigger ones");
            Assert.That(fastest.Brightness, Is.GreaterThan(slowest.Brightness), "and the brighter ones");
        }

        /// <summary>
        /// Most of a sky is faint. Spread evenly, the stars come out at much the same brightness and the
        /// field reads as falling snow rather than as distance, which is exactly how it first looked.
        /// </summary>
        [Test]
        public void MostStars_AreFaintOnes()
        {
            var stars = Field(600, 17).Stars;

            // "Faint" is relative to the floor, which is deliberately not zero: a star dimmer than this
            // would be invisible rather than distant, and the field would just be drawing nothing.
            var dim = 0;
            var bright = 0;
            foreach (var star in stars)
            {
                if (star.Brightness < 0.65f) dim++;
                if (star.Brightness > 0.85f) bright++;
            }

            Assert.That(dim, Is.GreaterThan(stars.Length / 2),
                "over half the sky should be faint, or the field reads as snow");
            Assert.That(bright, Is.GreaterThan(0), "but some stars have to stand out");
            Assert.That(bright, Is.LessThan(stars.Length / 4), "and not too many of them");
        }
    }
}
