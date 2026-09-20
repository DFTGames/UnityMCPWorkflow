using TMPro;
using UnityEngine;
using YASS.Feedback;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Difficulty select (GDD "UI Flow and Screens"). Choosing one starts the campaign; Endless is shown but
    /// locked until the campaign is finished.
    /// </summary>
    public sealed class DifficultyMenu : MonoBehaviour
    {
        [SerializeField, Tooltip("Shown but not selectable until the campaign is completed.")]
        Button endlessButton;

        [SerializeField, Tooltip("Tint for a locked entry's label, so it reads as locked and not merely unselected.")]
        Color lockedLabel = new Color(0.65f, 0.6f, 0.72f, 0.6f);

        void Awake()
        {
            if (endlessButton == null) return;

            // The unlock rule is in place (GDD "Core Loop": finishing the campaign unlocks Endless), but the
            // mode itself is not built yet, so the entry stays disabled and says which it is.
            endlessButton.interactable = false;

            var label = endlessButton.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;

            label.text = GameFlow.Progress.IsEndlessUnlocked ? "Endless (coming soon)" : "Endless (locked)";

            // The colour tint only dims the button's own graphic; its label would stay bright white.
            label.color = lockedLabel;
        }

        public void ChooseCadet() => Choose(Difficulty.Cadet);

        public void ChoosePilot() => Choose(Difficulty.Pilot);

        public void ChooseAce() => Choose(Difficulty.Ace);

        static void Choose(Difficulty difficulty)
        {
            Cue.Play(Sfx.UiConfirm);
            GameFlow.StartCampaign(difficulty);
        }
    }
}
