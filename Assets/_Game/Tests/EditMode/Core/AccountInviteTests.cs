using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The line on the title inviting the player onto the leaderboards.
    /// </summary>
    /// <remarks>
    /// Worth testing away from the scene because the states it gets wrong are the quiet ones: a build that
    /// cannot sign anybody in advertising that it can, and a player who already signed in still being asked
    /// to. Neither throws, neither looks broken, and both are only noticed by somebody reading the screen.
    /// </remarks>
    public class AccountInviteTests
    {
        [Test]
        public void SignedOut_InvitesThemOntoTheBoards_AndSaysWhoseAccount()
        {
            var invite = AccountInvite.For(signedIn: false, canSignIn: true, pilotName: "", service: "itch.io");

            Assert.That(invite.Shown, Is.True);
            Assert.That(invite.Offered, Is.True, "there is something to press");
            Assert.That(invite.Text.ToLowerInvariant(), Does.Contain("leaderboard"),
                "it has to say what signing in is for, or it is just a demand");
            Assert.That(invite.Button, Is.EqualTo("Sign in with itch.io"),
                "the button says what pressing it will do, rather than leaving them to find out");
        }

        [Test]
        public void OnADesktopBuild_ItNamesUnityInstead()
        {
            var invite = AccountInvite.For(false, true, "", "Unity");

            Assert.That(invite.Button, Is.EqualTo("Sign in with Unity"));
        }

        /// <summary>
        /// A build with no way to sign in says nothing rather than offering something it will then refuse.
        /// WebGL was in exactly that state until itch.io sign-in existed, and a dead button there would have
        /// been the player's first impression of the game.
        /// </summary>
        [Test]
        public void WhenTheBuildCannotSignAnybodyIn_TheLineIsNotThere()
        {
            var invite = AccountInvite.For(signedIn: false, canSignIn: false, pilotName: "", service: "Unity");

            Assert.That(invite.Shown, Is.False);
            Assert.That(invite.Offered, Is.False);
        }

        [Test]
        public void SignedIn_ConfirmsItAndStopsAsking()
        {
            var invite = AccountInvite.For(signedIn: true, canSignIn: true, pilotName: "Ace", service: "itch.io");

            Assert.That(invite.Shown, Is.True, "it is worth telling them their runs count");
            Assert.That(invite.Offered, Is.False, "there is nothing left to press");
            Assert.That(invite.Button, Is.Empty);
            Assert.That(invite.Text, Does.Contain("Ace"));
            Assert.That(invite.Text.ToLowerInvariant(), Does.Not.Contain("sign in to"),
                "a signed-in player must not still be invited to sign in");
        }

        /// <summary>
        /// The name arrives from the service a moment after the session does, so there is a window where the
        /// player is signed in and nameless. The line has to read properly in it rather than trailing off.
        /// </summary>
        [Test]
        public void SignedInBeforeTheNameArrives_StillReadsProperly()
        {
            foreach (var name in new[] { null, "", "   " })
            {
                var invite = AccountInvite.For(true, true, name, "itch.io");

                Assert.That(invite.Shown, Is.True);
                Assert.That(invite.Offered, Is.False);
                Assert.That(invite.Text, Is.Not.Empty);
                Assert.That(invite.Text.Trim(), Does.Not.EndWith("as."), "a name-shaped hole in the sentence");
            }
        }

        [Test]
        public void WithNoServiceNamed_ItDoesNotLeaveAGapInTheButton()
        {
            foreach (var service in new[] { null, "", "  " })
                Assert.That(AccountInvite.For(false, true, "", service).Button, Is.EqualTo("Sign in with Unity"));
        }
    }
}
