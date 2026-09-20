using System;

namespace YASS.Core
{
    /// <summary>
    /// What the player has achieved across sessions: how far the campaign has been played on each difficulty,
    /// and whether it has been finished (which is what unlocks Endless, per GDD "Core Loop").
    /// </summary>
    /// <remarks>
    /// Kept per difficulty because finishing on Cadet should not claim the Ace campaign. Stored through the same
    /// key-value store as the settings; the keys are separate.
    /// </remarks>
    public sealed class CampaignProgress
    {
        const string KeyPrefix = "yass.progress.";

        readonly ISettingsStore _store;

        public CampaignProgress(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>How many levels have been cleared on a difficulty, 0 before the first is finished.</summary>
        public int LevelsCleared(Difficulty difficulty) =>
            Math.Max(0, (int)_store.GetFloat(ClearedKey(difficulty), 0f));

        /// <summary>True once the campaign has been finished on that difficulty.</summary>
        public bool IsCampaignComplete(Difficulty difficulty) =>
            _store.GetBool(CompleteKey(difficulty), false);

        /// <summary>True once the campaign has been finished on any difficulty: what unlocks Endless.</summary>
        public bool IsEndlessUnlocked
        {
            get
            {
                foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
                    if (IsCampaignComplete(difficulty)) return true;

                return false;
            }
        }

        /// <summary>
        /// Records a cleared level. Progress only ever moves forward: replaying level 1 does not undo having
        /// reached level 5.
        /// </summary>
        public void RecordLevelCleared(Difficulty difficulty, int levelNumber, int levelCount)
        {
            if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            if (levelCount < levelNumber) throw new ArgumentOutOfRangeException(nameof(levelCount));

            if (levelNumber > LevelsCleared(difficulty))
                _store.SetFloat(ClearedKey(difficulty), levelNumber);

            if (levelNumber >= levelCount) _store.SetBool(CompleteKey(difficulty), true);

            _store.Save();
        }

        /// <summary>Test seam, and the basis of any future "clear my progress" option.</summary>
        public void Reset()
        {
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                _store.SetFloat(ClearedKey(difficulty), 0f);
                _store.SetBool(CompleteKey(difficulty), false);
            }

            _store.Save();
        }

        static string ClearedKey(Difficulty difficulty) => $"{KeyPrefix}{difficulty}.levelsCleared";
        static string CompleteKey(Difficulty difficulty) => $"{KeyPrefix}{difficulty}.complete";
    }
}
