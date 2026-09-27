using NUnit.Framework;
using YASS.Core;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// What the web build was given so it can sign anybody in.
    /// </summary>
    /// <remarks>
    /// These two constants are registered as a pair with itch.io, and itch checks them as a pair: a redirect
    /// that does not match the one registered against this client id is refused at itch's end, before the
    /// game is involved. That failure reads as a broken application rather than a mismatched setting, and it
    /// happens on a live page rather than anywhere a test can see, so pinning them here is the only warning
    /// available on this side. Changing either means changing the other, and re-registering.
    /// </remarks>
    public class ItchSettingsTests
    {
        [Test]
        public void TheWebBuild_HasAnOAuthApplication()
        {
            // The one switch that turns the sign-in button on. It shipped empty for a while on purpose, so
            // that a build with no application registered hid the button instead of sending players to an
            // itch error page; this fails if it is ever blanked by accident.
            Assert.That(ItchSettings.Configured, Is.True,
                "with no client id the web build silently offers no sign-in at all");
        }

        [Test]
        public void TheClientId_IsSomethingItchWillAccept()
        {
            Assert.That(ItchSettings.ClientId, Does.Not.Contain(" "),
                "whitespace in the id would be escaped into the URL and itch would not recognise it");

            // Catches a placeholder pasted in during setup, which would otherwise look configured and fail
            // only at itch's page.
            Assert.That(ItchSettings.ClientId.ToLowerInvariant(),
                Does.Not.Contain("your").And.Not.Contain("todo").And.Not.Contain("xxx"));
        }

        /// <summary>
        /// The registered redirect. Pinned because the trusted origin for the token is derived from it: a
        /// change here silently changes which page the game will accept a sign-in from.
        /// </summary>
        [Test]
        public void TheCallbackPage_IsTheOneRegisteredWithItch()
        {
            Assert.That(ItchAccounts.CallbackPage,
                Is.EqualTo("https://dftgames.github.io/UnityMCPWorkflow/itch-callback.html"));

            Assert.That(ItchAccounts.CallbackPage, Does.StartWith("https://"),
                "a token arrives on this page, so it may not travel in the clear");
        }

        /// <summary>The two go out together, escaped, in the page the player is actually sent to.</summary>
        [Test]
        public void TheSignInPage_CarriesBoth()
        {
            var page = ItchOAuth.PageToOpen(ItchSettings.ClientId, ItchAccounts.CallbackPage, "somestate");

            Assert.That(page, Does.Contain("client_id=" + ItchSettings.ClientId));
            Assert.That(page, Does.Contain("redirect_uri=https%3A%2F%2Fdftgames.github.io"));
            Assert.That(page, Does.StartWith("https://itch.io/user/oauth?"));
        }
    }
}
