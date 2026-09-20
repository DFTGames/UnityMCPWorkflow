using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The campaign's levels in order (GDD "Levels"): the scene to load for each. Grows as levels are built;
    /// the run is won after clearing the last one in this list.
    /// </summary>
    [CreateAssetMenu(fileName = "Campaign", menuName = "YASS/Campaign")]
    public sealed class Campaign : ScriptableObject
    {
        [SerializeField, Tooltip("Level scenes in play order. Each must be in the build settings.")]
        string[] levelScenes = { GameFlow.FirstLevelScene };

        public int LevelCount => levelScenes.Length;

        /// <summary>The scene for a level, counting from zero.</summary>
        public string SceneFor(int levelIndex) =>
            levelIndex >= 0 && levelIndex < levelScenes.Length ? levelScenes[levelIndex] : null;

        /// <summary>A fresh run of this campaign on the given difficulty.</summary>
        public CampaignRun NewRun(Difficulty difficulty) => new CampaignRun(difficulty, LevelCount);
    }
}
