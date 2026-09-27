using System;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// How hard a thumb has to push before the right stick aims, and why that is not the gamepad's answer.
    /// </summary>
    /// <remarks>
    /// Reported from play as the aim being unusable on a phone. The cause was geometric: the aim uses the
    /// angle of the thumb's offset and never its length, so the only thing setting a player's precision is
    /// how far out the thumb is when the aim is first believed. These pin the arithmetic that fixes it,
    /// because it is the sort of thing a later tidy-up would "simplify" back to one shared number.
    /// </remarks>
    public class TouchAimTests
    {
        /// <summary>A thumb resting on glass wanders about this much without its owner meaning anything.</summary>
        const float ThumbWobblePixels = 8f;

        /// <summary>A phone's shorter side, for working in real pixels rather than fractions.</summary>
        const float ShortSidePixels = 1080f;

        static float EngageDistance(float deadZone) =>
            ShortSidePixels * TouchControls.StickRadiusFraction * deadZone;

        static float WobbleDegrees(float deadZone) =>
            (float)(Math.Atan(ThumbWobblePixels / EngageDistance(deadZone)) * 180.0 / Math.PI);

        [Test]
        public void TouchAimsLater_ThanAGamepad()
        {
            Assert.That(GameTuning.TouchAimDeadZone, Is.GreaterThan(GameTuning.GamepadAimDeadZone),
                "a thumb has no centring spring, so it needs further out before its angle means anything");
        }

        /// <summary>
        /// The number that matters, expressed as what the player feels. The firing arc is 35 degrees either
        /// way, so a wobble anywhere near that is the ship aiming itself.
        /// </summary>
        [Test]
        public void AThumbAtRest_DoesNotSwingTheNoseAcrossTheArc()
        {
            var wobble = WobbleDegrees(GameTuning.TouchAimDeadZone);

            Assert.That(wobble, Is.LessThan(GameTuning.FiringArcDegrees * 0.25f),
                $"thumb noise swings the aim {wobble:0.0} degrees inside an arc of " +
                $"{GameTuning.FiringArcDegrees} either way, which is what made it unaimable");
        }

        /// <summary>The fault this replaced, kept as a measurement so the improvement is not a matter of opinion.</summary>
        [Test]
        public void TheGamepadsDeadZone_WouldStillBeTooTwitchyForAThumb()
        {
            Assert.That(WobbleDegrees(GameTuning.GamepadAimDeadZone), Is.GreaterThan(WobbleDegrees(GameTuning.TouchAimDeadZone)),
                "the change has to actually reduce the swing, not merely differ");
        }

        [Test]
        public void BelowTheDeadZone_ItFiresStraightAhead_RatherThanAiming()
        {
            // A thumb just short of the threshold: held, so it fires, but not aiming yet.
            var nudge = new Vector2(GameTuning.TouchAimDeadZone - 0.05f, 0f);

            Assert.That(TouchControls.ResolveAim(nudge, true, GameTuning.TouchAimDeadZone, out var direction),
                Is.True, "the thumb is down, so it is firing");
            Assert.That(direction.X, Is.EqualTo(1f).Within(0.0001f), "straight ahead");
            Assert.That(direction.Y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void PastTheDeadZone_ItAimsAlongThePush()
        {
            var push = Vector2.Normalize(new Vector2(1f, 1f)) * (GameTuning.TouchAimDeadZone + 0.2f);

            Assert.That(TouchControls.ResolveAim(push, true, GameTuning.TouchAimDeadZone, out var direction),
                Is.True);
            Assert.That(direction.Y, Is.GreaterThan(0.5f), "it should be aiming up and forward");
        }

        [Test]
        public void WithNoThumbDown_NothingIsFired()
        {
            Assert.That(TouchControls.ResolveAim(Vector2.Zero, false, GameTuning.TouchAimDeadZone, out var direction),
                Is.False);
            Assert.That(direction, Is.EqualTo(Vector2.Zero));
        }
    }
}
