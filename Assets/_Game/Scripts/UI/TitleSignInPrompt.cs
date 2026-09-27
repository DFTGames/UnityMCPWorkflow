using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The invitation onto the leaderboards, on the title screen (GDD "UI Flow and Screens", Title).
    /// </summary>
    /// <remarks>
    /// **The button signs in directly, and that is deliberate.** On the web the sign-in window only opens if
    /// the click that asked for it is still being handled, so putting a screen change between the press and
    /// the request is exactly how a popup gets blocked. One press, one window.
    ///
    /// It repaints whenever it is shown and whenever an attempt answers, because a returning player is signed
    /// back in a moment after the title appears (<c>TitleMenu</c> starts that) and the line has to stop
    /// inviting them to do something they have already done.
    /// </remarks>
    public sealed class TitleSignInPrompt : MonoBehaviour
    {
        [SerializeField, Tooltip("The whole row, hidden when there is nothing to say.")]
        GameObject row;

        [SerializeField] TMP_Text label;
        [SerializeField] Button signInButton;
        [SerializeField] TMP_Text buttonLabel;

        int _asked;

        void OnEnable()
        {
            Refresh();

            // The resume started at the title takes a round trip, so the first paint is usually "not signed
            // in" even for somebody who is. Asking again when the answer lands is what corrects it.
            GameFlow.Accounts.Resume(_ => Refresh());
        }

        /// <summary>Test seam: what the line currently says, or empty when the row is hidden.</summary>
        internal string Showing =>
            row != null && row.activeSelf && label != null ? label.text : string.Empty;

        void Refresh()
        {
            // A callback can arrive after the title has gone: this object may already be destroyed.
            if (this == null || label == null) return;

            var accounts = GameFlow.Accounts;
            var invitation = AccountInvite.For(accounts.IsSignedIn, accounts.CanSignIn,
                accounts.PilotName, accounts.ServiceName);

            if (row != null) row.SetActive(invitation.Shown);

            label.text = invitation.Text;

            if (signInButton != null) signInButton.gameObject.SetActive(invitation.Offered);
            if (buttonLabel != null) buttonLabel.text = invitation.Button;
        }

        /// <summary>Wired to the button in the scene.</summary>
        public void SignIn()
        {
            if (signInButton != null) signInButton.interactable = false;

            var asked = ++_asked;
            GameFlow.Accounts.SignIn(_ => Answered(asked));
        }

        void Answered(int asked)
        {
            // Only the newest attempt may speak. A second press while the first window is open would
            // otherwise have the loser repaint over the winner.
            if (this == null || asked != _asked) return;

            if (signInButton != null) signInButton.interactable = true;

            // Whatever happened, including a refusal: the line reports the state rather than the attempt.
            // What went wrong belongs on the account screen, which has room to say it.
            Refresh();
        }
    }
}
