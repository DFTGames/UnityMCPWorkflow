using System;
using System.Collections.Generic;
using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>
    /// What Endless mode is made of (GDD "Core Loop", Endless mode): the campaign's levels, which between them
    /// supply every wave it can draw, every boss it can meet and every sky it can wear. Endless authors no
    /// content of its own; it rearranges the campaign's.
    /// </summary>
    [CreateAssetMenu(menuName = "YASS/Endless Definition", fileName = "Endless")]
    public sealed class EndlessDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("The campaign's levels, in order. Cycle 1 wears the first one's setting.")]
        LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public int LevelCount => levels.Length;

        public LevelDefinition Level(int index) => levels[index];

        /// <summary>Returns a description of the first problem found, or null when Endless is playable.</summary>
        public string Validate()
        {
            if (levels.Length == 0) return "no levels to draw from";

            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null) return $"level {i + 1} is missing";
                if (levels[i].Waves.Count == 0) return $"{levels[i].name} has no waves";
                if (levels[i].BossPrefab != null && levels[i].BossPrefab.Definition == null)
                    return $"{levels[i].name}'s boss has no definition, so it cannot be scored";
                if (levels[i].Backdrop == null) return $"{levels[i].name} has no backdrop";

                // Endless is the one mode that plays a level outside its own scene, so nothing else has
                // checked these: a wave with no groups would throw as the cycle was built.
                if (levels[i].Validate() is string problem) return $"{levels[i].name}: {problem}";
            }

            return null;
        }
    }
}
