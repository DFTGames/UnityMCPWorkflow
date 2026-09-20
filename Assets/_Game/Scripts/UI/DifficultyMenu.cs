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

            endlessButton.interactable = false;

            // The colour tint only dims the button's own graphic; its label would stay bright white.
            var label = endlessButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = lockedLabel;
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
