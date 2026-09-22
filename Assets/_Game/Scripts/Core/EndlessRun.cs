using System;
using System.Collections.Generic;

namespace YASS.Core
{
    /// <summary>One wave of a cycle: which level it was taken from, and which of that level's waves it is.</summary>
    public readonly struct EndlessWave
    {
        public readonly int LevelIndex;
        public readonly int WaveIndex;

        public EndlessWave(int levelIndex, int waveIndex)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            if (waveIndex < 0) throw new ArgumentOutOfRangeException(nameof(waveIndex));

            LevelIndex = levelIndex;
            WaveIndex = waveIndex;
        }
    }

    /// <summary>
    /// An Endless run (GDD "Core Loop", Endless mode): cycles of ten waves and a boss, each cycle faster, more
    /// crowded and worth more. There is no win condition; the score is the result, so the run only ends when the
    /// player does.
    /// </summary>
    public sealed class EndlessRun
    {
        /// <summary>Waves in a cycle, before the boss.</summary>
        public const int WavesPerCycle = 10;

        /// <summary>What each completed cycle adds (GDD "Core Loop", Endless mode).</summary>
        public const float SpeedStepPerCycle = 0.10f;
        public const float CountStepPerCycle = 0.15f;
        public const float ScoreStepPerCycle = 0.25f;

        /// <summary>Speed and count stop at twice their cycle 1 values; the score multiplier does not stop.</summary>
        public const float EscalationCap = 2f;

        public EndlessRun(Difficulty difficulty, int levelCount)
        {
            if (levelCount < 1) throw new ArgumentOutOfRangeException(nameof(levelCount));

            Difficulty = difficulty;
            LevelCount = levelCount;
        }

        public Difficulty Difficulty { get; }

        /// <summary>How many campaign levels the run can draw its waves, settings and bosses from.</summary>
        public int LevelCount { get; }

        /// <summary>The cycle being played, counting from one.</summary>
        public int Cycle { get; private set; } = 1;

        /// <summary>How many cycles have been finished: what the escalation is built on.</summary>
        public int CompletedCycles => Cycle - 1;

        public float EnemySpeedMultiplier => Escalated(SpeedStepPerCycle);
        public float EnemyCountMultiplier => Escalated(CountStepPerCycle);

        /// <summary>Uncapped, so a long run is worth playing for (GDD "Scoring").</summary>
        public float ScoreMultiplier => 1f + ScoreStepPerCycle * CompletedCycles;

        /// <summary>
        /// The level whose setting this cycle wears, looping after the last one
        /// (GDD: "the background moves on to the next level's setting every cycle").
        /// </summary>
        public int SettingIndex => (Cycle - 1) % LevelCount;

        /// <summary>The boss is over; the next cycle begins.</summary>
        public void CompleteCycle() => Cycle++;

        float Escalated(float step) => Math.Min(1f + step * CompletedCycles, EscalationCap);

        /// <summary>
        /// The levels a cycle may draw waves from: its own cycle number plus one, capped at what exists. Cycle 1
        /// draws from the first two levels, so an Endless run opens at something like the campaign's difficulty
        /// rather than with a level 8 wall, and by cycle 7 anything can turn up.
        /// </summary>
        public int DrawableLevels => Math.Min(LevelCount, Cycle + 1);

        /// <summary>
        /// Picks this cycle's waves. <paramref name="wavesPerLevel"/> is how many waves each campaign level has;
        /// a level with none is skipped. The same wave may be drawn twice in a cycle: at ten waves from a pool
        /// this size, forbidding it would bias the order more than the repeat costs.
        /// </summary>
        public void PickWaves(IReadOnlyList<int> wavesPerLevel, IRandomSource random, List<EndlessWave> into)
        {
            if (wavesPerLevel == null) throw new ArgumentNullException(nameof(wavesPerLevel));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (into == null) throw new ArgumentNullException(nameof(into));

            into.Clear();

            var drawable = Math.Min(DrawableLevels, wavesPerLevel.Count);
            var available = new List<int>();
            for (var level = 0; level < drawable; level++)
                if (wavesPerLevel[level] > 0) available.Add(level);

            if (available.Count == 0)
                throw new InvalidOperationException("Endless needs at least one level with waves to draw from.");

            for (var i = 0; i < WavesPerCycle; i++)
            {
                var level = available[Index(random, available.Count)];
                into.Add(new EndlessWave(level, Index(random, wavesPerLevel[level])));
            }
        }

        /// <summary>
        /// The boss for this cycle, drawn from all of them however early it is: the bosses are the reward for
        /// getting there, and meeting the Overmind in cycle 2 is a story worth having. Never the same boss twice
        /// running, so a run cannot stall on one fight.
        /// </summary>
        public int PickBoss(IRandomSource random, int previousBoss = -1)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (LevelCount == 1) return 0;

            if (previousBoss < 0 || previousBoss >= LevelCount) return Index(random, LevelCount);

            // Draw from the others and step over the one just played: one draw, no retry, and no boss made
            // likelier than the rest by being next in line.
            var boss = Index(random, LevelCount - 1);
            return boss >= previousBoss ? boss + 1 : boss;
        }

        static int Index(IRandomSource random, int count) => Math.Min((int)(random.NextFloat() * count), count - 1);
    }
}
