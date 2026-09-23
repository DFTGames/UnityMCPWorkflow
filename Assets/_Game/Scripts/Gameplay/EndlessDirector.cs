using System;
using System.Collections.Generic;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// Assembles one Endless cycle: ten waves drawn from the campaign's levels, a boss, and the sky to play it
    /// under (GDD "Core Loop", Endless mode). The escalation itself is <see cref="EndlessRun"/> in the rules
    /// layer; this only turns its choices into the assets a level needs.
    /// </summary>
    public sealed class EndlessDirector
    {
        readonly EndlessDefinition _definition;
        readonly IRandomSource _random;
        readonly int[] _wavesPerLevel;

        /// <summary>Which level each of this cycle's waves came from, so a spawn can find its prefabs again.</summary>
        readonly List<EndlessWave> _cycleWaves = new List<EndlessWave>();

        int _previousBoss = -1;

        public EndlessDirector(EndlessDefinition definition, Difficulty difficulty, IRandomSource random)
        {
            _definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            if (definition.Validate() is string problem)
                throw new ArgumentException($"Endless is not playable: {problem}.", nameof(definition));

            Run = new EndlessRun(difficulty, definition.LevelCount);

            _wavesPerLevel = new int[definition.LevelCount];
            for (var i = 0; i < definition.LevelCount; i++) _wavesPerLevel[i] = definition.Level(i).Waves.Count;
        }

        public EndlessRun Run { get; }

        /// <summary>The boss of the cycle being played.</summary>
        public BossView BossPrefab { get; private set; }

        /// <summary>
        /// Draws the cycle's waves and boss and returns the wave script for them. Called once per cycle, before
        /// its first wave.
        /// </summary>
        public SpawnGroupSpec[][] BeginCycle()
        {
            Run.PickWaves(_wavesPerLevel, _random, _cycleWaves);

            var boss = Run.PickBoss(_random, _previousBoss);
            _previousBoss = boss;
            BossPrefab = _definition.Level(boss).BossPrefab;

            var specs = new SpawnGroupSpec[_cycleWaves.Count][];
            for (var i = 0; i < _cycleWaves.Count; i++)
            {
                var wave = _cycleWaves[i];
                specs[i] = _definition.Level(wave.LevelIndex).WaveToSpecs(wave.WaveIndex);
            }

            return specs;
        }

        /// <summary>The boss is down: the next cycle is faster, fuller and worth more.</summary>
        public void CompleteCycle() => Run.CompleteCycle();

        /// <summary>The group a spawn request refers to, found in whichever level that wave was drawn from.</summary>
        public LevelDefinition.Group GroupFor(int waveIndex, int groupIndex)
        {
            var wave = _cycleWaves[waveIndex];
            return _definition.Level(wave.LevelIndex).GetGroup(wave.WaveIndex, groupIndex);
        }

        /// <summary>Every enemy that could appear in any cycle, so their pools exist before one is drawn.</summary>
        public IEnumerable<EnemyView> AllEnemies()
        {
            foreach (var level in _definition.Levels)
            foreach (var wave in level.Waves)
            {
                if (wave.groups == null) continue;

                foreach (var group in wave.groups)
                    if (group.enemy != null) yield return group.enemy;
            }
        }

        /// <summary>Every boss that could turn up, so nothing is built mid-fight.</summary>
        public IEnumerable<BossView> AllBosses()
        {
            foreach (var level in _definition.Levels)
                if (level.BossPrefab != null) yield return level.BossPrefab;
        }
    }
}
