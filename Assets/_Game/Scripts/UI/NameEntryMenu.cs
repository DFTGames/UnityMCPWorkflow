using TMPro;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The account screen (GDD "Scoring", Leaderboards): a pilot name and a password, which is what makes a
    /// score the player's rather than this machine's. The pilot name is the account, so there is one name to
    /// remember and it is what the boards show.
    /// </summary>
    /// <remarks>
    /// Nobody is made to sign up. A leaderboard is not worth blocking a game for, so this screen always has a
    /// way past it, and taking that way is remembered: the player is asked once, not at every start.
    ///
    /// Sign up and sign in are separate buttons rather than one clever one. Guessing would mean either
    /// creating an account for somebody who mistyped the name they already have, or telling a new player
    /// their password is wrong, and both are worse than asking which they meant.
    /// </remarks>
    public sealed class NameEntryMenu : MonoBehaviour
    {
        [SerializeField] MenuRouter router;

        [SerializeField, Tooltip("The pilot name, which is also the account name.")]
        TMP_InputField field;

        [SerializeField] TMP_InputField passwordField;

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

            if (field != null)
            {
                field.characterLimit = Credentials.MaxNameLength;

                // Without notifying: filling the box in is not the player finishing an edit.
                field.SetTextWithoutNotify(GameFlow.Settings.PlayerName);
            }

            if (passwordField != null)
            {
                passwordField.characterLimit = Credentials.MaxPasswordLength;
                passwordField.contentType = TMP_InputField.ContentType.Password;
                passwordField.SetTextWithoutNotify(string.Empty);
            }

            // Selecting is left to MenuScreenView, which selects this screen's first selectable.
        }

        public void CreateAccount()
        {
            if (_waiting) return;

            var asked = Begin("Creating your account...");
            var name = Name;
            GameFlow.Accounts.SignUp(name, Password, result => Answered(asked, name, result));
        }

        public void SignIn()
        {
            if (_waiting) return;

            var asked = Begin("Signing in...");
            var name = Name;
            GameFlow.Accounts.SignIn(name, Password, result => Answered(asked, name, result));
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
        /// There is no way back yet. Nothing clears the choice and nothing signs anybody out, so this is a
        /// one-way door: see Open Questions, "Account management". Do not describe it as reversible until
        /// the screen that reverses it exists.
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

        /// <summary>
        /// Leaving cancels whatever was in flight, and takes the password out of the field: it is masked on
        /// screen but held in full by the component, and there is no reason to keep it once it has been used.
        /// </summary>
        void OnDisable()
        {
            _asked++;
            _waiting = false;

            if (passwordField != null) passwordField.SetTextWithoutNotify(string.Empty);
        }

        string Name => field != null ? field.text.Trim() : string.Empty;

        string Password => passwordField != null ? passwordField.text : string.Empty;

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
                Say(result.Problem);
                return;
            }

            // The name the account was made with, not whatever is in the box now: the player can go on
            // typing while the round trip is in flight, and the boards have to show the account's name.
            GameFlow.Settings.SetPlayerName(name);
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
