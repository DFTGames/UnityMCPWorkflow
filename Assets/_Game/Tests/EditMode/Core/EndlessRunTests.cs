using System;
using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// Endless mode's rules (GDD "Core Loop", Endless mode): cycles of ten waves and a boss, each one faster,
    /// more crowded and worth more, with speed and count capped and the score multiplier not.
    /// </summary>
    public class EndlessRunTests
    {
        const int Levels = 8;

        static EndlessRun Run() => new EndlessRun(Difficulty.Pilot, Levels);

        static readonly int[] WavesPerLevel = { 11, 11, 11, 11, 11, 11, 12, 13 };

        /// <summary>Walks a fixed sequence, so a draw can be checked rather than merely observed.</summary>
        sealed class Sequence : IRandomSource
        {
            readonly float[] _values;
            int _next;

            public Sequence(params float[] values) => _values = values;

            public float NextFloat() => _values[_next++ % _values.Length];
        }

        [Test]
        public void ItStartsAtCycleOneWithNothingAddedYet()
        {
            var run = Run();

            Assert.That(run.Cycle, Is.EqualTo(1));
            Assert.That(run.CompletedCycles, Is.EqualTo(0));
            Assert.That(run.EnemySpeedMultiplier, Is.EqualTo(1f));
            Assert.That(run.EnemyCountMultiplier, Is.EqualTo(1f));
            Assert.That(run.ScoreMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void EachCycleAddsSpeedCountAndScore()
        {
            var run = Run();
            run.CompleteCycle();

            Assert.That(run.Cycle, Is.EqualTo(2));
            Assert.That(run.EnemySpeedMultiplier, Is.EqualTo(1.10f).Within(1e-4f));
            Assert.That(run.EnemyCountMultiplier, Is.EqualTo(1.15f).Within(1e-4f));
            Assert.That(run.ScoreMultiplier, Is.EqualTo(1.25f).Within(1e-4f));

            run.CompleteCycle();
            Assert.That(run.EnemySpeedMultiplier, Is.EqualTo(1.20f).Within(1e-4f));
            Assert.That(run.EnemyCountMultiplier, Is.EqualTo(1.30f).Within(1e-4f));
            Assert.That(run.ScoreMultiplier, Is.EqualTo(1.50f).Within(1e-4f));
        }

        [Test]
        public void SpeedAndCountStopAtTwiceButTheScoreDoesNot()
        {
            var run = Run();
            for (var i = 0; i < 30; i++) run.CompleteCycle();

            Assert.That(run.EnemySpeedMultiplier, Is.EqualTo(2f), "speed is capped");
            Assert.That(run.EnemyCountMultiplier, Is.EqualTo(2f), "and so is the crowd");
            Assert.That(run.ScoreMultiplier, Is.EqualTo(1f + 0.25f * 30).Within(1e-3f), "the score is not");
        }

        [Test]
        public void TheSettingWalksTheCampaignAndLoops()
        {
            var run = Run();

            Assert.That(run.SettingIndex, Is.EqualTo(0), "cycle 1 wears level 1's setting");
            for (var i = 0; i < 7; i++) run.CompleteCycle();
            Assert.That(run.SettingIndex, Is.EqualTo(7), "cycle 8 wears level 8's");

            run.CompleteCycle();
            Assert.That(run.SettingIndex, Is.EqualTo(0), "and cycle 9 starts round again");
        }

        [Test]
        public void EarlyCyclesDrawFromEarlyLevelsOnly()
        {
            // Cycle 1 draws from levels 1 and 2: an Endless run has to open at something like the campaign's
            // difficulty rather than with a wall of Frigates.
            var run = Run();
            Assert.That(run.DrawableLevels, Is.EqualTo(2));

            for (var i = 0; i < 6; i++) run.CompleteCycle();
            Assert.That(run.DrawableLevels, Is.EqualTo(8), "by cycle 7 anything can turn up");

            run.CompleteCycle();
            Assert.That(run.DrawableLevels, Is.EqualTo(8), "and it stops at what exists");
        }

        [Test]
        public void ACycleIsTenWavesFromTheLevelsItMayDrawFrom()
        {
            var run = Run();
            var waves = new List<EndlessWave>();

            run.PickWaves(WavesPerLevel, new TestRandom(0.9f), waves);

            Assert.That(waves.Count, Is.EqualTo(EndlessRun.WavesPerCycle));
            foreach (var wave in waves)
            {
                Assert.That(wave.LevelIndex, Is.InRange(0, run.DrawableLevels - 1));
                Assert.That(wave.WaveIndex, Is.InRange(0, WavesPerLevel[wave.LevelIndex] - 1));
            }
        }

        [Test]
        public void ADrawNeverRunsOffTheEndOfALevel()
        {
            // A random source that returns values just under 1 must still index the last wave, not past it.
            var run = Run();
            var waves = new List<EndlessWave>();

            run.PickWaves(WavesPerLevel, new Sequence(0.999999f), waves);

            foreach (var wave in waves)
            {
                Assert.That(wave.LevelIndex, Is.EqualTo(1), "the last level it may draw from");
                Assert.That(wave.WaveIndex, Is.EqualTo(WavesPerLevel[1] - 1), "and that level's last wave");
            }
        }

        [Test]
        public void ALevelWithNoWavesIsNeverDrawnFrom()
        {
            // Far enough in that every level is drawable, so the empty ones are skipped by the rule rather
            // than by being out of reach.
            var run = Run();
            for (var i = 0; i < 10; i++) run.CompleteCycle();
            Assert.That(run.DrawableLevels, Is.EqualTo(Levels));

            var waves = new List<EndlessWave>();
            run.PickWaves(new[] { 0, 4, 0, 0, 7, 0, 0, 0 }, new Sequence(0.1f, 0.9f, 0.5f), waves);

            Assert.That(waves.Count, Is.EqualTo(EndlessRun.WavesPerCycle));
            foreach (var wave in waves)
                Assert.That(wave.LevelIndex, Is.EqualTo(1).Or.EqualTo(4), "only the levels that have waves");
        }

        [Test]
        public void OneLevelIsAlwaysItsOwnBoss()
        {
            var run = new EndlessRun(Difficulty.Pilot, 1);

            Assert.That(run.PickBoss(new TestRandom(0.5f)), Is.EqualTo(0));
            Assert.That(run.PickBoss(new TestRandom(0.5f), 0), Is.EqualTo(0), "there is nothing else to pick");
        }

        [Test]
        public void ADrawUsesOnlyTheLevelsItWasGivenCountsFor()
        {
            // The definition may list fewer levels than the run believes in; the draw must not run off the end.
            var run = Run();
            for (var i = 0; i < 10; i++) run.CompleteCycle();

            var waves = new List<EndlessWave>();
            run.PickWaves(new[] { 3, 3 }, new TestRandom(0.99f), waves);

            foreach (var wave in waves)
            {
                Assert.That(wave.LevelIndex, Is.LessThan(2));
                Assert.That(wave.WaveIndex, Is.LessThan(3));
            }
        }

        [Test]
        public void TheSameBossNeverTurnsUpTwiceRunning()
        {
            var run = Run();
            var random = new Sequence(0.3f); // always draws the same boss

            var first = run.PickBoss(random);
            var second = run.PickBoss(random, first);

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(second, Is.InRange(0, Levels - 1));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var run = Run();
            var waves = new List<EndlessWave>();

            Assert.Throws<ArgumentOutOfRangeException>(() => new EndlessRun(Difficulty.Pilot, 0));
            Assert.Throws<ArgumentNullException>(() => run.PickWaves(null, new TestRandom(0.5f), waves));
            Assert.Throws<ArgumentNullException>(() => run.PickWaves(WavesPerLevel, null, waves));
            Assert.Throws<ArgumentNullException>(() => run.PickWaves(WavesPerLevel, new TestRandom(0.5f), null));
            Assert.Throws<ArgumentNullException>(() => run.PickBoss(null));
            Assert.Throws<InvalidOperationException>(() =>
                run.PickWaves(new[] { 0, 0, 0, 0, 0, 0, 0, 0 }, new TestRandom(0.5f), waves));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EndlessWave(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EndlessWave(0, -1));
        }
    }
}
