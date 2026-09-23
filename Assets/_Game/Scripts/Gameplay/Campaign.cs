using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// The campaign's levels in order (GDD "Levels"). A level is data, not a scene: one scene plays all of
    /// them, and this says which definition it plays for each step of a run.
    /// </summary>
    /// <remarks>
    /// There was a scene per level once, and this listed their names. They were identical copies kept in step
    /// by a builder, so a change to one had to be made to a table in code and regenerated into the other seven.
    /// Adding a level now means making a <see cref="LevelDefinition"/> and dropping it in here.
    /// </remarks>
    [CreateAssetMenu(fileName = "Campaign", menuName = "YASS/Campaign")]
    public sealed class Campaign : ScriptableObject
    {
        [SerializeField, Tooltip("The levels of the campaign, in play order.")]
        LevelDefinition[] levels = new LevelDefinition[0];

        public int LevelCount => levels.Length;

        /// <summary>The level to play for a step of a run, counting from zero, or null past the end.</summary>
        public LevelDefinition LevelFor(int levelIndex) =>
            levelIndex >= 0 && levelIndex < levels.Length ? levels[levelIndex] : null;

        /// <summary>A fresh run of this campaign on the given difficulty.</summary>
        public CampaignRun NewRun(Difficulty difficulty) => new CampaignRun(difficulty, LevelCount);

        /// <summary>Returns a description of the first problem found, or null when the campaign is playable.</summary>
        public string Validate()
        {
            if (levels.Length == 0) return "no levels";

            for (var i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null) return $"level {i + 1} is missing";
                if (levels[i].Validate() is string problem) return $"{levels[i].name}: {problem}";
            }

            return null;
        }
    }
}
