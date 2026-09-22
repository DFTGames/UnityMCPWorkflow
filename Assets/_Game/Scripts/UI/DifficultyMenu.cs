using TMPro;
using UnityEngine;
using YASS.Feedback;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Difficulty select (GDD "UI Flow and Screens"). Cadet, Pilot and Ace start a run; Endless is a switch on
    /// the same screen rather than a fourth choice, because Endless needs a difficulty too (the leaderboards are
    /// per mode and difficulty). It is dead until the campaign has been finished.
    /// </summary>
    public sealed class DifficultyMenu : MonoBehaviour
    {
        [SerializeField, Tooltip("Switches the screen between Campaign and Endless. Locked until the campaign is done.")]
        Button endlessButton;

        [SerializeField, Tooltip("Tint for a locked entry's label, so it reads as locked and not merely unselected.")]
        Color lockedLabel = new Color(0.65f, 0.6f, 0.72f, 0.6f);

        [SerializeField] Color unlockedLabel = Color.white;

        [SerializeField, Tooltip("The line under the Endless entry, which also changes when it unlocks.")]
        TMP_Text endlessDescription;

        TMP_Text _endlessLabel;
        bool _unlocked;
        bool _endlessChosen;

        /// <summary>Test seam: which mode the next choice of difficulty will start.</summary>
        internal GameMode Mode => _endlessChosen ? GameMode.Endless : GameMode.Campaign;

        void Awake()
        {
            if (endlessButton == null) return;

            _unlocked = GameFlow.Progress.IsEndlessUnlocked;
            endlessButton.interactable = _unlocked;
            _endlessLabel = endlessButton.GetComponentInChildren<TMP_Text>(true);
            Refresh();
        }

        /// <summary>The Endless entry: turns the screen's mode over rather than starting anything itself.</summary>
        public void ToggleEndless()
        {
            if (!_unlocked) return;

            _endlessChosen = !_endlessChosen;
            Cue.Play(Sfx.UiMove);
            Refresh();
        }

        public void ChooseCadet() => Choose(Difficulty.Cadet);

        public void ChoosePilot() => Choose(Difficulty.Pilot);

        public void ChooseAce() => Choose(Difficulty.Ace);

        void Choose(Difficulty difficulty)
        {
            Cue.Play(Sfx.UiConfirm);

            if (_endlessChosen) GameFlow.StartEndless(difficulty);
            else GameFlow.StartCampaign(difficulty);
        }

        /// <summary>
        /// The entry says what it is: locked, or which mode picking a difficulty will start. The colour tint only
        /// dims the button's own graphic, so the label is set here too.
        /// </summary>
        void Refresh()
        {
            if (_endlessLabel == null) return;

            if (!_unlocked)
            {
                _endlessLabel.text = "Endless (locked)";
                _endlessLabel.color = lockedLabel;
                if (endlessDescription != null) endlessDescription.text = "Finish the campaign to unlock.";
                return;
            }

            _endlessLabel.text = _endlessChosen ? "Endless: on" : "Endless: off";
            _endlessLabel.color = unlockedLabel;

            if (endlessDescription != null)
                endlessDescription.text = _endlessChosen
                    ? "Cycles of ten waves and a boss, faster every time. Pick a difficulty to begin."
                    : "Cycles of ten waves and a boss, faster every time. Turn it on, then pick a difficulty.";
        }
    }
}
