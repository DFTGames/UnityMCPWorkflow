using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The account screen (GDD "UI Flow and Screens", Account): who is signed in, and how to sign out, use a
    /// different account, or manage the account itself. Reached from Settings.
    /// </summary>
    /// <remarks>
    /// <see cref="AccountScreen"/> in Core decides what may be pressed and what the line at the top says;
    /// this wires that to the buttons. The awkward part is that signing in leaves the game altogether: a
    /// page opens elsewhere and comes back at an unknown time, or never, so every button is dead while an
    /// attempt is in flight and every answer is checked against the attempt the screen is still waiting for.
    ///
    /// **Managing the account is a link, not a feature.** Changing a password, recovering a forgotten one and
    /// closing the account happen on Unity's own portal. The game does not implement any of it and never
    /// sees a credential, which is the point of using Unity accounts at all.
    /// </remarks>
    public sealed class AccountMenu : MonoBehaviour
    {
        [SerializeField] MenuRouter router;

        [SerializeField, Tooltip("Who is signed in, or the invitation to. Keeps its row when empty.")]
        TMP_Text summaryText;

        [SerializeField, Tooltip("What went wrong, in the player's words. Keeps its row when empty.")]
        TMP_Text messageText;

        [SerializeField] Button signInButton;
        [SerializeField] Button signOutButton;
        [SerializeField] Button switchButton;
        [SerializeField] Button manageButton;

        [SerializeField, Tooltip("The account's screen name, as the boards print it.")]
        TMP_InputField nameField;

        [SerializeField, Tooltip("The row the field sits in. Hiding the field itself leaves an empty gap.")]
        GameObject nameRow;

        [SerializeField, Tooltip("Commits the typed name to the account. Nothing changes until it is pressed.")]
        Button changeNameButton;

        bool _waiting;

        /// <summary>
        /// Whether the service has been asked for the name since this screen last needed it. The answer
        /// arrives after the screen has already drawn, so the screen has to ask and then draw again; this
        /// stops it asking once per repaint, which would be a network call every frame.
        /// </summary>
        bool _nameAsked;

        /// <summary>
        /// Which attempt the screen is waiting for. Every answer arrives later than the question and the
        /// player can leave in between, so an answer for an attempt nobody is waiting on is dropped.
        /// </summary>
        int _asked;

        void OnEnable()
        {
            _waiting = false;
            Say(string.Empty);

            // Blank before anything is known, so a name from the previous screen, the previous player or a
            // value left in the scene cannot sit there looking like an answer while the service is asked.
            if (nameField != null)
            {
                // Set here rather than left in the scene: the limit follows the service's rule, and a value
                // baked into the scene silently went stale when that rule turned out to be 50 and not 12.
                nameField.characterLimit = Credentials.MaxNameLength;
                nameField.SetTextWithoutNotify(string.Empty);
            }
            if (summaryText != null) summaryText.text = string.Empty;

            _nameAsked = false;
            Refresh();
        }

        /// <summary>Leaving abandons whatever was in flight, so a late answer cannot act on this screen.</summary>
        void OnDisable()
        {
            _asked++;
            _waiting = false;
        }

        public void SignIn()
        {
            if (_waiting) return;

            var asked = Begin();
            GameFlow.Accounts.SignIn(result => Answered(asked, result));
        }

        public void SignOut()
        {
            if (_waiting) return;

            var asked = Begin();
            GameFlow.Accounts.SignOut(result =>
            {
                ForgetTheStoredName();
                Answered(asked, result);
            });
        }

        /// <summary>
        /// Drops this machine's copy of the name when an account leaves. The name belongs to the account,
        /// so keeping a copy after signing out means the next person to open Settings sees the last
        /// person's name sitting in their own box, which is exactly what it looked like.
        /// </summary>
        static void ForgetTheStoredName()
        {
            GameFlow.Settings.SetPlayerName(string.Empty);
            GameFlow.Settings.Flush();
        }

        /// <summary>
        /// Signing out and straight back in, which is what somebody handing the machine to a friend means.
        /// A switch abandoned halfway leaves nobody signed in rather than the previous player, which is the
        /// safe end to stop at: the alternative is a friend quietly posting scores under someone else's name.
        /// </summary>
        public void SwitchAccount()
        {
            if (_waiting) return;

            var asked = Begin();
            GameFlow.Accounts.SwitchAccount(result =>
            {
                ForgetTheStoredName();
                Answered(asked, result);
            });
        }

        /// <summary>Opens Unity's portal. Nothing to wait for: the player leaves and may not come back.</summary>
        public void ManageAccount()
        {
            if (_waiting) return;

            GameFlow.Accounts.OpenAccountPortal();
            Say("Your account opens in a browser.");
        }

        /// <summary>
        /// Commits the typed name to the account. Deliberately a button rather than something that happens
        /// when the box loses focus: this is a change to the account itself, it is what every board will
        /// print, and the service rate-limits renames, so it should happen when the player says so and not
        /// as a side effect of clicking elsewhere.
        /// </summary>
        public void ChangeName()
        {
            if (_waiting || nameField == null) return;

            var wanted = nameField.text.Trim();

            var check = Credentials.CheckName(wanted);
            if (!check.IsUsable)
            {
                Say(check.Problem);
                return;
            }

            if (wanted == GameFlow.Accounts.PilotName)
            {
                Say("That is already your name.");
                return;
            }

            var asked = Begin();
            Say("Changing your name...");

            GameFlow.Accounts.SetPilotName(wanted, result =>
            {
                if (this == null || !isActiveAndEnabled || asked != _asked) return;

                _waiting = false;

                if (result.Succeeded)
                {
                    // No copy kept here. The service holds the name, and a second copy on this machine is
                    // only something to fall out of step or outlive the account it belonged to.
                    Say($"Your runs now show as {wanted}.");
                }
                else
                {
                    Say(AccountScreen.Explain(result));
                }

                Refresh();
            });
        }

        public void Back()
        {
            if (router != null) router.Back();
        }

        int Begin()
        {
            _waiting = true;
            Say(string.Empty);
            Refresh();
            return ++_asked;
        }

        void Answered(int asked, AccountResult result)
        {
            if (this == null || !isActiveAndEnabled || asked != _asked) return;

            _waiting = false;
            Say(AccountScreen.Explain(result));

            // Somebody has just signed in or out, so whatever was known about the name no longer applies:
            // ask again. Without this, signing in from this screen left it saying "you have no pilot name
            // yet" for ever, because the screen was already open and nothing asked the service a second
            // time after the sign-in completed.
            if (result.Succeeded) _nameAsked = false;

            Refresh();
        }

        /// <summary>
        /// Puts the Core rules on the screen. Buttons that cannot be used are hidden rather than greyed:
        /// this screen is a short list, and a dead row in it is just a question the player cannot answer.
        /// </summary>
        void Refresh()
        {
            var accounts = GameFlow.Accounts;

            // No falling back to the name kept on this machine once somebody is signed in. That fallback
            // meant a player whose account had no name yet was shown the old local one and had no way to
            // tell: the screen said one thing while the boards would have printed another. An account with
            // no name says so, which is the only honest thing to show.
            var shown = accounts.IsSignedIn ? accounts.PilotName : GameFlow.Settings.PlayerName;

            var offer = AccountScreen.Offer(accounts.IsSignedIn, accounts.AccountLabel,
                shown, _waiting, accounts.CanSignIn, accounts.CanManageAccount);

            if (summaryText != null) summaryText.text = offer.Summary;

            Show(signInButton, offer.CanSignIn);
            Show(signOutButton, offer.CanSignOut);
            Show(switchButton, offer.CanSwitch);
            Show(manageButton, offer.CanManage);

            // Changing the name is only meaningful for an account to change it on.
            var canRename = accounts.IsSignedIn && !_waiting;
            Show(changeNameButton, canRename);

            // The row, not the field: the input sits inside a container that the layout measures, so
            // hiding the input alone left the container behind as an empty gap the width of the panel.
            var row = nameRow != null ? nameRow : (nameField == null ? null : nameField.gameObject);
            if (row != null && row.activeSelf != canRename) row.SetActive(canRename);

            // Refilled only when the player is not part-way through typing, or this would rewrite the box
            // under their hands every time the screen redrew.
            if (nameField != null && canRename && !nameField.isFocused)
                nameField.SetTextWithoutNotify(accounts.PilotName);

            KeepTheSelectionSomewhereUsable();
            AskForTheNameIfItIsNotKnown();
        }

        /// <summary>
        /// Asks the service for the name when the screen has none to show, and redraws when it answers.
        /// Once per opening, and once more after signing in or out: the service is the only place the name
        /// lives, and it answers later than the screen draws.
        /// </summary>
        void AskForTheNameIfItIsNotKnown()
        {
            if (_nameAsked || _waiting) return;

            var accounts = GameFlow.Accounts;
            if (!accounts.IsSignedIn || !string.IsNullOrEmpty(accounts.PilotName)) return;

            _nameAsked = true;
            var asked = _asked;

            accounts.FetchPilotName(_ =>
            {
                if (this == null || !isActiveAndEnabled || asked != _asked) return;
                Refresh();
            });
        }

        /// <summary>
        /// The buttons on this screen come and go with who is signed in, and the one the screen opened with
        /// is hidden as soon as somebody is: the EventSystem will hold a selection on a hidden object, and
        /// then refuse to deliver anything to it, so keyboard and gamepad go dead with no way back.
        /// </summary>
        /// <remarks>
        /// Done here rather than in <see cref="MenuScreenView"/>, which every screen in both scenes shares.
        /// This screen is the only one whose buttons appear and disappear as state changes, so the rule that
        /// copes with that belongs to it: the shared view can go on assuming the button it was given is the
        /// one to select.
        /// </remarks>
        void KeepTheSelectionSomewhereUsable()
        {
            var events = EventSystem.current;
            if (events == null) return;

            var chosen = events.currentSelectedGameObject;
            if (chosen != null && chosen.activeInHierarchy) return; // still somewhere the player can use

            foreach (var candidate in GetComponentsInChildren<Selectable>())
            {
                if (!candidate.gameObject.activeInHierarchy || !candidate.IsInteractable()) continue;

                events.SetSelectedGameObject(null); // clear first, or the old highlight can linger
                events.SetSelectedGameObject(candidate.gameObject);
                return;
            }
        }

        static void Show(Button button, bool shown)
        {
            if (button == null) return;
            if (button.gameObject.activeSelf != shown) button.gameObject.SetActive(shown);
        }

        void Say(string message)
        {
            if (messageText != null) messageText.text = message ?? string.Empty;
        }
    }
}
