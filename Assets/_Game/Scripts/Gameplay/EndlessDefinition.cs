using System;
using System.Collections.Generic;
using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>
    /// What Endless mode is made of (GDD "Core Loop", Endless mode): the campaign's levels, which between them
    /// supply every wave it can draw and every boss it can meet, plus its own ring of skies. Endless authors no
    /// gameplay content of its own; it rearranges the campaign's.
    ///
    /// The skies are the one thing it does not borrow. The campaign's backdrops are landmarks, painted to tell
    /// one level from another, and cutting between them announced the loop; Endless has its own calm set,
    /// authored to dissolve into one another (GDD "Art Direction", Endless skies).
    /// </summary>
    [CreateAssetMenu(menuName = "YASS/Endless Definition", fileName = "Endless")]
    public sealed class EndlessDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("The campaign's levels, in order: the waves and bosses Endless draws from.")]
        LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        [SerializeField, Tooltip("The ring of skies, as Resources paths: only the two a dissolve needs are held.")]
        string[] skyPaths = Array.Empty<string>();

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public int LevelCount => levels.Length;

        /// <summary>
        /// The ring of skies as Resources paths, in the order they dissolve into one another. Paths rather
        /// than references so a run holds the two skies it is showing and not the other four.
        /// </summary>
        public IReadOnlyList<string> SkyPaths => skyPaths;

        public LevelDefinition Level(int index) => levels[index];

        /// <summary>Returns a description of the first problem found, or null when Endless is playable.</summary>
        public string Validate()
        {
            if (levels.Length == 0) return "no levels to draw from";

            // Two is the fewest that can dissolve into one another and still be a ring.
            if (skyPaths.Length < 2) return "fewer than two skies, so the sky could only cut or stand still";

            for (var i = 0; i < skyPaths.Length; i++)
                if (string.IsNullOrEmpty(skyPaths[i])) return $"sky {i + 1} has no path";

            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null) return $"level {i + 1} is missing";
                if (levels[i].Waves.Count == 0) return $"{levels[i].name} has no waves";
                if (levels[i].BossPrefab != null && levels[i].BossPrefab.Definition == null)
                    return $"{levels[i].name}'s boss has no definition, so it cannot be scored";

                // Endless rearranges levels the campaign may never reach in a given run, so nothing else has
                // necessarily checked these: a wave with no groups would throw as the cycle was built.
                if (levels[i].Validate() is string problem) return $"{levels[i].name}: {problem}";
            }

            return null;
        }
    }
}
