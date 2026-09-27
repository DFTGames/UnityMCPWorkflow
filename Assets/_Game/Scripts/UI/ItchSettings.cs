namespace YASS.UI
{
    /// <summary>
    /// What the web build needs to know about this game's itch.io OAuth application.
    /// </summary>
    /// <remarks>
    /// **The client id is not a secret.** It is in the address of the page every player is sent to, and
    /// itch.io's implicit flow has no secret to go with it: it identifies the application and nothing more.
    /// The thing that *is* secret, the service account key that mints Unity sessions, lives in Secret Manager
    /// and is only ever read by Cloud Code. Nothing that can impersonate a player is in the built game.
    ///
    /// In source rather than in an asset because it belongs to the build, not to a scene: one value, changed
    /// about as often as the game is renamed, and needed before any scene has loaded.
    /// </remarks>
    public static class ItchSettings
    {
        /// <summary>
        /// The OAuth application's client id, from its page under itch.io user settings. Empty turns the
        /// sign-in button off rather than offering a button that sends the player to an itch error page,
        /// which is what shipped before the application was registered.
        /// </summary>
        /// <remarks>
        /// It travels beside <see cref="ItchAccounts.CallbackPage"/>, and itch checks the pair: a redirect
        /// that does not match the one registered against this id is refused at itch's end, before the game
        /// is involved, and reads as a broken application rather than a mismatched setting. Changing either
        /// means changing both.
        /// </remarks>
        public const string ClientId = "abb1e3705f2719d11e3cff64da8bc289";

        /// <summary>Whether this build has been given what it needs to offer an itch sign-in at all.</summary>
        public static bool Configured => !string.IsNullOrWhiteSpace(ClientId);

        /// <summary>
        /// Puts the underlying fault on the account screen as well as in the log.
        /// </summary>
        /// <remarks>
        /// **On while the web flow is being proved, and meant to go off afterwards.** The log is the right
        /// place for this, but on the web the log is a browser console somebody has to be talked into
        /// opening, and three quite different faults all reach the player as "Could not reach the service".
        /// What is shown is the exception type, the service's error code and a trimmed message, with the
        /// itch token removed: enough to tell the faults apart, and nothing a player could misuse.
        /// </remarks>
        public const bool ShowSignInFaults = false;
    }
}
