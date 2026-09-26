namespace YASS.Core
{
    /// <summary>What the account screen offers, and what it says at the top.</summary>
    public readonly struct AccountOffer
    {
        public readonly bool CanSignIn;
        public readonly bool CanSignOut;
        public readonly bool CanSwitch;
        public readonly bool CanManage;
        public readonly bool CanPlayOffline;

        /// <summary>The one line above the buttons, in the player's words.</summary>
        public readonly string Summary;

        public AccountOffer(bool canSignIn, bool canSignOut, bool canSwitch, bool canManage,
            bool canPlayOffline, string summary)
        {
            CanSignIn = canSignIn;
            CanSignOut = canSignOut;
            CanSwitch = canSwitch;
            CanManage = canManage;
            CanPlayOffline = canPlayOffline;
            Summary = summary ?? string.Empty;
        }

        /// <summary>True when there is something the player can actually press.</summary>
        public bool IsIdle => CanSignIn || CanSignOut || CanSwitch || CanManage || CanPlayOffline;
    }

    /// <summary>
    /// The account screen's rules (GDD "UI Flow and Screens", Account): which of its buttons are live, and
    /// what the line above them says.
    /// </summary>
    /// <remarks>
    /// Here rather than in the screen because it is the part worth being sure of. Signing in leaves the game
    /// while a page opens somewhere else and comes back an unknown time later, possibly never: the states
    /// that go wrong are "the player pressed it twice", "the player pressed sign out while a sign-in was in
    /// flight", and "this build cannot open a browser at all", and none of those are comfortable to reach
    /// through a scene.
    /// </remarks>
    public static class AccountScreen
    {
        /// <summary>
        /// What to offer. <paramref name="busy"/> is true from the moment the player asks for something
        /// until the answer arrives; while it is, nothing else may be pressed, because a second sign-in
        /// started on top of the first is how two half-finished sessions end up racing each other.
        /// </summary>
        /// <param name="signedIn">Whether somebody is signed in now.</param>
        /// <param name="account">The account, for showing them: an email address, or empty. Never a board name.</param>
        /// <param name="pilotName">The handle the boards show. Told to the player on every state.</param>
        /// <param name="busy">Whether an attempt is in flight.</param>
        /// <param name="canOpenSignIn">Whether this build can open the sign-in page at all.</param>
        /// <param name="canOpenPortal">Whether Unity's account portal can be opened from here.</param>
        public static AccountOffer Offer(bool signedIn, string account, string pilotName, bool busy,
            bool canOpenSignIn, bool canOpenPortal)
        {
            if (busy)
                return new AccountOffer(false, false, false, false, false,
                    "Finish in the page that opened, then come back.");

            // Said in every state, because it is the thing the player actually sees on a board and the one
            // thing no screen was telling them. The account is who they are to Unity; this is who they are
            // to everybody else.
            var named = !string.IsNullOrWhiteSpace(pilotName);
            var playingAs = named
                ? $"Your runs show as {pilotName.Trim()}."
                : "You have no pilot name yet, so your runs would show as Pilot.";

            if (signedIn)
            {
                // An account always has a name: the service assigns one to anybody who has not chosen. So
                // an empty name here does not mean there is none, it means the service has not answered
                // yet, and saying "you have no pilot name" would be asserting something untrue.
                if (!named) playingAs = "Checking the name on your account...";

                // No account label means no email in the token, which is the ordinary case on any launch
                // after the one they signed in on. "Signed in with your Unity account" is true and useful;
                // the player id that used to stand in here meant nothing to anybody.
                var who = string.IsNullOrWhiteSpace(account)
                    ? "Signed in with your Unity account."
                    : $"Signed in as {account.Trim()}.";

                return new AccountOffer(
                    canSignIn: false,
                    canSignOut: true,
                    canSwitch: canOpenSignIn,
                    canManage: canOpenPortal,
                    canPlayOffline: false,
                    summary: $"{who} {playingAs}");
            }

            if (!canOpenSignIn)
                return new AccountOffer(false, false, false, false, true,
                    $"{playingAs} Signing in is not available in this version, so your scores stay on this device.");

            return new AccountOffer(
                canSignIn: true,
                canSignOut: false,
                canSwitch: false,
                canManage: false,
                canPlayOffline: true,
                summary: $"{playingAs} You are not signed in, so they stay on this device. " +
                         "Sign in to put them on the online boards.");
        }

        /// <summary>
        /// What to say when an attempt comes back. Cancelling is not a failure and must not be dressed as
        /// one: a player who changed their mind gets the ordinary invitation back, not an error.
        /// </summary>
        public static string Explain(AccountResult result)
        {
            switch (result.Status)
            {
                case AccountStatus.Succeeded:
                    return string.Empty;

                case AccountStatus.Cancelled:
                    return "Signing in was not finished.";

                case AccountStatus.Unreachable:
                    return "Could not reach the service. Check your connection and try again.";

                case AccountStatus.Unsupported:
                    return "Signing in is not available in this version.";

                case AccountStatus.Working:
                    // Not a failure: an attempt is still open somewhere else. Telling the player it did
                    // not work, while the page they are meant to finish in is sitting behind the game,
                    // is how somebody ends up signing in twice and racing themselves.
                    return "Finish in the page that opened, then come back.";

                default:
                    return string.IsNullOrWhiteSpace(result.Problem)
                        ? "That did not work. Please try again."
                        : result.Problem;
            }
        }
    }
}
