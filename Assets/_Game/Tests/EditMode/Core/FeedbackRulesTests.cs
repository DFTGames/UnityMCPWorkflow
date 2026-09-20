using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class ScreenShakeTests
    {
        [Test]
        public void NoTrauma_MeansNoShake()
        {
            Assert.That(ScreenShake.Offset(0f, 1.23f), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void Add_BuildsUpButNeverPastFull()
        {
            var trauma = ScreenShake.Add(0f, ScreenShake.PlayerHitTrauma);
            Assert.That(trauma, Is.EqualTo(ScreenShake.PlayerHitTrauma));

            // Several hits in a row build on each other rather than restarting the same jolt.
            trauma = ScreenShake.Add(trauma, ScreenShake.PlayerHitTrauma);
            Assert.That(trauma, Is.GreaterThan(ScreenShake.PlayerHitTrauma));

            for (var i = 0; i < 10; i++) trauma = ScreenShake.Add(trauma, ScreenShake.BossDefeatedTrauma);
            Assert.That(trauma, Is.EqualTo(1f));
        }

        [Test]
        public void Decay_RunsOutAndStopsAtZero()
        {
            var trauma = ScreenShake.Add(0f, 1f);

            for (var i = 0; i < 60; i++) trauma = ScreenShake.Decay(trauma, 1f / 60f);

            Assert.That(trauma, Is.Zero, "a full shake lasts well under a second");
            Assert.That(ScreenShake.Decay(0f, 1f), Is.Zero, "decay never goes negative");
        }

        [Test]
        public void Offset_StaysWithinTheLimitOnEachAxis()
        {
            // MaxOffset is per axis, so the diagonal reaches MaxOffset * sqrt(2); the GDD records both numbers.
            for (var i = 0; i < 200; i++)
            {
                var offset = ScreenShake.Offset(1f, i * 0.01f);
                Assert.That(Math.Abs(offset.X), Is.LessThanOrEqualTo(ScreenShake.MaxOffset));
                Assert.That(Math.Abs(offset.Y), Is.LessThanOrEqualTo(ScreenShake.MaxOffset));
            }
        }

        [Test]
        public void Offset_ShrinksAsTheShakeFadesOut()
        {
            var strong = ScreenShake.Offset(1f, 0.05f).Length();
            var weak = ScreenShake.Offset(0.3f, 0.05f).Length();

            Assert.That(weak, Is.LessThan(strong));
        }

        [Test]
        public void Offset_KeepsMoving()
        {
            // A shake that returned the same offset every frame would just look like a displaced camera.
            var a = ScreenShake.Offset(1f, 0.00f);
            var b = ScreenShake.Offset(1f, 0.05f);
            var c = ScreenShake.Offset(1f, 0.10f);

            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(b, Is.Not.EqualTo(c));
        }

        [Test]
        public void Offset_IsRepeatable()
        {
            Assert.That(ScreenShake.Offset(0.6f, 1.5f), Is.EqualTo(ScreenShake.Offset(0.6f, 1.5f)));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenShake.Add(0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenShake.Decay(1f, -0.1f));
        }
    }

    public class AudioRulesTests
    {
        [Test]
        public void FullVolume_IsUnchangedAndSilenceIsMuted()
        {
            Assert.That(AudioRules.ToDecibels(1f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AudioRules.ToDecibels(0f), Is.EqualTo(AudioRules.MutedDecibels));
            Assert.That(AudioRules.ToDecibels(float.NaN), Is.EqualTo(AudioRules.MutedDecibels));
        }

        [Test]
        public void HalfTheSlider_IsAboutSixDecibelsDown()
        {
            // Loudness is logarithmic: halving the amplitude is -6 dB, not half the decibels.
            Assert.That(AudioRules.ToDecibels(0.5f), Is.EqualTo(-6.02f).Within(0.01f));
        }

        [Test]
        public void Decibels_RiseWithTheSlider()
        {
            var previous = AudioRules.MutedDecibels;
            for (var i = 1; i <= 10; i++)
            {
                var decibels = AudioRules.ToDecibels(i / 10f);
                Assert.That(decibels, Is.GreaterThan(previous));
                previous = decibels;
            }
        }

        [Test]
        public void AboveOne_IsTreatedAsFullVolume()
        {
            Assert.That(AudioRules.ToDecibels(4f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void RepeatedShots_AreSpacedOut()
        {
            // Level 5 fires about 11 times a second; without this the shots stack into noise.
            Assert.That(AudioRules.CanPlay(Sfx.PlayerShot, 0f, 0.02f), Is.False);
            Assert.That(AudioRules.CanPlay(Sfx.PlayerShot, 0f, 0.07f), Is.True);
        }

        [Test]
        public void OneOffSounds_AreNeverHeldBack()
        {
            Assert.That(AudioRules.MinimumInterval(Sfx.SectorClear), Is.Zero);
            Assert.That(AudioRules.CanPlay(Sfx.SectorClear, 1f, 1f), Is.True);
        }

        [Test]
        public void ASoundThatHasNeverPlayed_CanPlay()
        {
            Assert.That(AudioRules.CanPlay(Sfx.PlayerShot, -1f, 0f), Is.True);
        }

        [Test]
        public void Nothing_IsNeverPlayed()
        {
            Assert.That(AudioRules.CanPlay(Sfx.None, -1f, 10f), Is.False);
        }

        [Test]
        public void RepeatedSounds_VaryInPitch()
        {
            var pitches = new float[5];
            for (var i = 0; i < pitches.Length; i++) pitches[i] = AudioRules.Pitch(Sfx.PlayerShot, i);

            Assert.That(pitches, Is.Unique, "a stream of shots must not sound like one note");
            foreach (var pitch in pitches)
                Assert.That(pitch, Is.InRange(0.9f, 1.1f), "the variation stays subtle");
        }

        [Test]
        public void OneOffSounds_KeepTheirPitch()
        {
            Assert.That(AudioRules.Pitch(Sfx.BossExplosion, 3), Is.EqualTo(1f));
        }

        [Test]
        public void TheGamesSoundsAreTheOnesTheGddLists()
        {
            // The GDD's table is the contract; this fails if a sound is added or renamed without updating it.
            var expected = new[]
            {
                Sfx.PlayerShot, Sfx.EnemyShot, Sfx.SmallExplosion, Sfx.LargeExplosion, Sfx.MeteorBreak,
                Sfx.PlayerHit, Sfx.ShieldHit, Sfx.LifeLost, Sfx.Pickup, Sfx.WeaponUpgrade, Sfx.BossWarning,
                Sfx.BossExplosion, Sfx.UiMove, Sfx.UiConfirm, Sfx.GameOver, Sfx.SectorClear
            };

            var actual = new List<Sfx>();
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
                if (sfx != Sfx.None) actual.Add(sfx);

            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void OnlyRepeatingSounds_AreHeldBack()
        {
            // A one-off sound held back would be silence where the player expects a bang.
            foreach (var sfx in new[] { Sfx.LargeExplosion, Sfx.LifeLost, Sfx.BossExplosion, Sfx.Pickup,
                         Sfx.SectorClear, Sfx.GameOver, Sfx.WeaponUpgrade, Sfx.BossWarning })
                Assert.That(AudioRules.MinimumInterval(sfx), Is.Zero, sfx.ToString());

            foreach (var sfx in new[] { Sfx.PlayerShot, Sfx.EnemyShot, Sfx.SmallExplosion, Sfx.MeteorBreak,
                         Sfx.PlayerHit, Sfx.ShieldHit })
                Assert.That(AudioRules.MinimumInterval(sfx), Is.GreaterThan(0f), sfx.ToString());
        }
    }

    public class HitFlashTests
    {
        [Test]
        public void FlashIsBrightestAtTheMomentOfTheHit()
        {
            Assert.That(VisualCues.HitFlash(0f), Is.EqualTo(1f));
        }

        [Test]
        public void FlashFadesOutAndStaysOut()
        {
            Assert.That(VisualCues.HitFlash(VisualCues.HitFlashSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(VisualCues.HitFlash(VisualCues.HitFlashSeconds), Is.Zero);
            Assert.That(VisualCues.HitFlash(10f), Is.Zero);
        }

        [Test]
        public void BeforeTheHit_ThereIsNoFlash()
        {
            Assert.That(VisualCues.HitFlash(-1f), Is.Zero);
        }

        [Test]
        public void ZeroDuration_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VisualCues.HitFlash(0f, 0f));
        }
    }
}
