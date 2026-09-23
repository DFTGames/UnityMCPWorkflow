using TMPro;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Asks the player what to call them, once, before their first run (GDD "Scoring", Leaderboards: an entry
    /// needs a name). The same name goes on the HUD.
    /// </summary>
    /// <remarks>
    /// Asked rather than generated, because a name the player chose is a reason to care where they came on a
    /// board, and a name the game invented is not. Asked once: after that it lives in Settings like any other
    /// preference, and this screen never appears again.
    /// </remarks>
    public sealed class NameEntryMenu : MonoBehaviour
    {
        [SerializeField] MenuRouter router;
        [SerializeField] TMP_InputField field;

        [SerializeField, Tooltip("Where the player goes once they have a name. The difficulty screen, normally.")]
        MenuScreen next = MenuScreen.Difficulty;

        void OnEnable()
        {
            if (field == null) return;

            field.characterLimit = Leaderboards.MaxNameLength;
            field.text = GameFlow.Settings.PlayerName;
            field.Select();
            field.ActivateInputField();
        }

        /// <summary>
        /// Whether the player still has to be asked. Called before starting a run, so the question comes up
        /// at the moment it matters rather than the moment the game opens.
        /// </summary>
        public static bool NeedsAsking => !GameFlow.Settings.HasPlayerName;

        public void Accept()
        {
            // Whatever they typed is cleaned to something that can go on a public board, and an empty box
            // is a name too: the default, rather than refusing to let them past.
            GameFlow.Settings.SetPlayerName(field != null ? field.text : null);
            GameFlow.Settings.Flush();

            router.Open(next);
        }

        public void Back() => router.Back();
    }
}
