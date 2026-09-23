using System;
using System.Collections.Generic;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// A level's wave script and boss (GDD "Wave System" and the level's own page). Each group spawns either an
    /// enemy prefab or a meteor definition.
    /// </summary>
    [CreateAssetMenu(menuName = "YASS/Level Definition", fileName = "LevelDefinition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Serializable]
        public struct Group
        {
            [Tooltip("Enemy to spawn. Leave empty to spawn the meteor instead.")]
            public EnemyView enemy;
            public MeteorDefinition meteor;
            [Min(1)] public int count;
            public Formation formation;
            [Range(0f, 1f), Tooltip("0 = bottom of the spawn band, 1 = top.")]
            public float entryHeight;
            [Min(0f), Tooltip("Seconds between members for Line, Staggered and Scatter formations.")]
            public float spacing;
            [Min(0f), Tooltip("Seconds after the wave starts before this group begins.")]
            public float startDelay;
        }

        [Serializable]
        public struct Wave
        {
            public string name;
            public Group[] groups;
        }

        [SerializeField, Min(1)] int levelNumber = 1;
        [SerializeField] string displayName = "Magenta Nebula";
        [SerializeField, Min(0f)] float firstWaveDelay = 1f;
        [SerializeField] Wave[] waves = Array.Empty<Wave>();
        [SerializeField] BossView bossPrefab;
        [SerializeField, Tooltip("The level's sky, under Resources: loaded when the level starts, freed when it ends.")]
        string backdropPath;

        public int LevelNumber => levelNumber;
        public string DisplayName => displayName;
        public float FirstWaveDelay => firstWaveDelay;
        public IReadOnlyList<Wave> Waves => waves;
        public BossView BossPrefab => bossPrefab;

        /// <summary>
        /// Where the level's sky lives, as a Resources path. A path rather than a reference so that a level
        /// holds its own sky and nothing else: one scene plays all eight, and a direct reference would put
        /// every backdrop in the scene's dependency graph (see <see cref="ContentCache"/>).
        /// </summary>
        public string BackdropPath => backdropPath;

        public Group GetGroup(int waveIndex, int groupIndex) => waves[waveIndex].groups[groupIndex];

        /// <summary>One wave of this level as rules-layer specs, for a mode that mixes levels together.</summary>
        public SpawnGroupSpec[] WaveToSpecs(int waveIndex)
        {
            var groups = waves[waveIndex].groups ?? Array.Empty<Group>();
            var specs = new SpawnGroupSpec[groups.Length];
            for (var g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                specs[g] = new SpawnGroupSpec(group.count, group.formation, group.entryHeight, group.spacing,
                    group.startDelay);
            }

            return specs;
        }

        /// <summary>The wave script as rules-layer specs, in the same order as <see cref="Waves"/>.</summary>
        public SpawnGroupSpec[][] ToSpecs()
        {
            var specs = new SpawnGroupSpec[waves.Length][];
            for (var w = 0; w < waves.Length; w++)
            {
                var groups = waves[w].groups ?? Array.Empty<Group>();
                specs[w] = new SpawnGroupSpec[groups.Length];
                for (var g = 0; g < groups.Length; g++)
                {
                    var group = groups[g];
                    specs[w][g] = new SpawnGroupSpec(group.count, group.formation, group.entryHeight, group.spacing,
                        group.startDelay);
                }
            }

            return specs;
        }

        /// <summary>Returns a description of the first problem found, or null when the level is usable.</summary>
        public string Validate()
        {
            if (bossPrefab == null) return "no boss prefab";

            // One scene plays every level, so this is the only thing that paints the sky: without it the
            // level opens on a blank backdrop and nothing says why.
            if (string.IsNullOrEmpty(backdropPath)) return "no backdrop";
            for (var w = 0; w < waves.Length; w++)
            {
                var groups = waves[w].groups;
                if (groups == null || groups.Length == 0) return $"wave {w + 1} has no groups";
                for (var g = 0; g < groups.Length; g++)
                {
                    if (groups[g].enemy == null && groups[g].meteor == null)
                        return $"wave {w + 1}, group {g + 1} has neither an enemy nor a meteor";
                    if (groups[g].enemy != null && groups[g].meteor != null)
                        return $"wave {w + 1}, group {g + 1} has both an enemy and a meteor";
                    if (groups[g].count < 1) return $"wave {w + 1}, group {g + 1} has a count below 1";
                    if (FormationLayout.IsSequential(groups[g].formation) && groups[g].spacing <= 0f && groups[g].count > 1)
                        return $"wave {w + 1}, group {g + 1} spawns several members with zero spacing, so they would stack";
                }
            }

            return null;
        }
    }
}
