using TMPro;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Asked once, before the first run (GDD "UI Flow and Screens", Account): a pilot name for the boards,
    /// and the offer of a Unity account, which is what makes a score the player's rather than this machine's.
    /// </summary>
    /// <remarks>
    /// The pilot name and the account are two different things. The name is a handle the player picks and the
    /// boards show; the account belongs to Unity and is identified by an email address, which must never
    /// appear on a public board. A player can have either without the other.
    ///
    /// **There is no password on this screen, and there never will be.** Signing in opens Unity's own page;
    /// the game never sees a credential, and changing or recovering a password happens in Unity's portal,
    /// reached from the account screen behind Settings.
    ///
    /// Nobody is made to sign in. A leaderboard is not worth blocking a game for, so this screen always has a
    /// way past it, and taking that way is remembered: the player is asked once, not at every start. It is no
    /// longer a one-way door either, since the account screen can sign in later.
    /// </remarks>
    public sealed class NameEntryMenu : MonoBehaviour
    {
        [SerializeField] MenuRouter router;

        [SerializeField, Tooltip("The pilot name the boards show. Not the account: that is Unity's.")]
        TMP_InputField field;

        [SerializeField, Tooltip("Hidden where the build cannot open Unity's sign-in page, such as WebGL.")]
        GameObject signInButton;

        [SerializeField, Tooltip("What went wrong, in the player's words. Keeps its row when empty.")]
        TMP_Text messageText;

        [SerializeField, Tooltip("Where the player goes once they are in. The difficulty screen, normally.")]
        MenuScreen next = MenuScreen.Difficulty;

        bool _waiting;

        /// <summary>
        /// Which attempt the screen is waiting for. Every answer arrives later than the question, and the
        /// player can press Back in between: without this, an answer that landed afterwards would save a
        /// name and open the difficulty screen by itself, from whatever screen they were then looking at.
        /// </summary>
        int _asked;

        /// <summary>
        /// Whether the player still has to be asked. Called before starting a run, so the question comes up
        /// at the moment it matters rather than the moment the game opens.
        /// </summary>
        public static bool NeedsAsking => !GameFlow.Accounts.IsSignedIn && !GameFlow.Settings.PlaysOffline;

        void OnEnable()
        {
            _waiting = false;
            Say(string.Empty);

            // Hidden where the build cannot open Unity's page at all, rather than offered and then refused.
            if (signInButton != null) signInButton.SetActive(GameFlow.Accounts.CanSignIn);

            if (field != null)
            {
                field.characterLimit = Credentials.MaxNameLength;

                // Without notifying: filling the box in is not the player finishing an edit.
                field.SetTextWithoutNotify(GameFlow.Settings.PlayerName);
            }

            // Selecting is left to MenuScreenView, which selects this screen's first selectable.
        }

        /// <summary>
        /// Hands the player to Unity's page. The same page signs them in or signs them up, so the screen
        /// does not have to ask which they meant, and the pilot name they have typed is kept either way.
        /// </summary>
        public void SignIn()
        {
            if (_waiting) return;

            if (!GameFlow.Accounts.CanSignIn)
            {
                Say(AccountScreen.Explain(new AccountResult(AccountStatus.Unsupported)));
                return;
            }

            var asked = Begin("Finish in the page that opened, then come back.");
            var name = Name;
            GameFlow.Accounts.SignIn(result => Answered(asked, name, result));
        }

        int Begin(string saying)
        {
            _waiting = true;
            Say(saying);
            return ++_asked;
        }

        /// <summary>
        /// Plays without an account: no online boards, and the runs stay on this machine. Remembered, so the
        /// question is not asked again.
        /// </summary>
        /// <remarks>
        /// Reversible: the account screen behind Settings signs in later, and the choice is only about not
        /// being asked again now.
        /// </remarks>
        public void PlayOffline()
        {
            if (_waiting) return;

            // Only a name that would do as an account name is kept: otherwise the local board would show
            // whatever half-typed thing was in the box, or the default with nothing to explain it. Playing
            // without an account does not demand one, so an unusable name is simply not stored.
            var name = Name;
            if (Credentials.CheckName(name).IsUsable) GameFlow.Settings.SetPlayerName(name);

            GameFlow.Settings.ChooseOffline();
            GameFlow.Settings.Flush();

            Continue();
        }

        public void Back()
        {
            if (router != null) router.Back();
        }

        /// <summary>Leaving cancels whatever was in flight, so a late answer cannot act on this screen.</summary>
        void OnDisable()
        {
            _asked++;
            _waiting = false;
        }

        string Name => field != null ? field.text.Trim() : string.Empty;

        /// <summary>
        /// The service has answered. Ignored unless this is still the attempt the screen is waiting for and
        /// the screen is still the one in front of the player: Back only deactivates a panel, so a late
        /// answer would otherwise act on a screen nobody is looking at.
        /// </summary>
        void Answered(int asked, string name, AccountResult result)
        {
            if (this == null || !isActiveAndEnabled || asked != _asked) return;

            _waiting = false;

            if (!result.Succeeded)
            {
                // Explain, not the raw Problem: cancelling and "not available here" carry no text of their
                // own, so showing the field directly cleared the line and left the screen looking inert.
                Say(AccountScreen.Explain(result));
                return;
            }

            // The name as it was when they pressed the button, not whatever is in the box now: the player
            // can go on typing while Unity's page is open, and a half-typed name is not what they chose.
            // Only if it would do as a board name, since signing in does not require one.
            if (Credentials.CheckName(name).IsUsable) GameFlow.Settings.SetPlayerName(name);
            GameFlow.Settings.Flush();

            Continue();
        }

        /// <summary>
        /// Replaces rather than opens: the player has answered this screen, so back from the difficulty
        /// screen belongs to the title, not to the question they have just been asked.
        /// </summary>
        void Continue()
        {
            if (router != null) router.Replace(next);
        }

        void Say(string message)
        {
            if (messageText != null) messageText.text = message ?? string.Empty;
        }
    }
}
