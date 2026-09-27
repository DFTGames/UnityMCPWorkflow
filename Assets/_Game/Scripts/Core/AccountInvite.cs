namespace YASS.Core
{
    /// <summary>What the title screen says about accounts, if anything.</summary>
    public readonly struct AccountInvitation
    {
        /// <summary>Whether the title shows anything about accounts at all.</summary>
        public readonly bool Shown;

        /// <summary>Whether there is a button to press. False once there is nothing left to offer.</summary>
        public readonly bool Offered;

        public readonly string Text;

        /// <summary>The button's own label, empty when there is no button.</summary>
        public readonly string Button;

        public AccountInvitation(bool shown, bool offered, string text, string button = null)
        {
            Shown = shown;
            Offered = offered;
            Text = text ?? string.Empty;
            Button = button ?? string.Empty;
        }
    }

    /// <summary>
    /// The line on the title screen inviting the player onto the leaderboards (GDD "UI Flow and Screens",
    /// Title).
    /// </summary>
    /// <remarks>
    /// **An invitation, not a gate.** It sits on the title and is ignorable: nobody is stopped, nothing is
    /// covered, and pressing Play never goes through it. That is the whole design of it. The alternative
    /// considered was putting the sign-in screen in front of the player when the menu loads, which asks for
    /// an account before they have pressed anything and overrides a player who has already said they would
    /// rather not have one.
    ///
    /// It is also the only place a sign-in can begin without a screen change, which matters more on the web
    /// than anywhere else: a browser only opens the sign-in window if the click that asked for it is still
    /// the thing being handled, so the fewer steps between the press and the window the better.
    /// </remarks>
    public static class AccountInvite
    {
        /// <summary>
        /// What to show. <paramref name="service"/> names whose account it would be, so the button says what
        /// pressing it will actually do rather than leaving the player to find out.
        /// </summary>
        public static AccountInvitation For(bool signedIn, bool canSignIn, string pilotName, string service)
        {
            var whose = string.IsNullOrWhiteSpace(service) ? "Unity" : service.Trim();

            if (signedIn)
            {
                // No button: there is nothing to press, and a live-looking button that does nothing is worse
                // than none. Changing the account is on the account screen, where it belongs.
                var named = !string.IsNullOrWhiteSpace(pilotName);

                return new AccountInvitation(true, false,
                    named
                        ? $"On the leaderboards as {pilotName.Trim()}."
                        : "Signed in. Your runs count on the leaderboards.");
            }

            // A build that cannot sign anybody in says nothing at all rather than advertising something it
            // will then refuse. WebGL was in exactly that state until itch.io sign-in existed.
            if (!canSignIn) return new AccountInvitation(false, false, string.Empty);

            return new AccountInvitation(true, true,
                "Sign in to be on the leaderboards.", $"Sign in with {whose}");
        }
    }
}
