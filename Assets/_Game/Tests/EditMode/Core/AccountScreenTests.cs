using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// What the account screen offers (GDD "UI Flow and Screens", Account).
    /// </summary>
    /// <remarks>
    /// Worth testing away from a scene because signing in leaves the game entirely: a page opens somewhere
    /// else and comes back at an unknown time, or never. The awkward states are a player pressing a button
    /// twice, a player signing out while a sign-in is still in flight, and a build that cannot open a page
    /// at all, and none of those are comfortable to produce by clicking.
    /// </remarks>
    public class AccountScreenTests
    {
        const string Pilot = "Ace";

        static AccountOffer SignedOut(bool canOpen = true) =>
            AccountScreen.Offer(false, null, Pilot, false, canOpen, true);

        static AccountOffer SignedIn(string who = "pilot@example.com") =>
            AccountScreen.Offer(true, who, Pilot, false, true, true);

        [Test]
        public void SignedOut_OffersSignInAndPlayingWithout()
        {
            var offer = SignedOut();

            Assert.That(offer.CanSignIn, Is.True);
            Assert.That(offer.CanPlayOffline, Is.True);
            Assert.That(offer.CanSignOut, Is.False, "there is nobody to sign out");
            Assert.That(offer.CanSwitch, Is.False);
            Assert.That(offer.CanManage, Is.False, "there is no account to manage yet");
        }

        [Test]
        public void SignedIn_OffersSigningOutSwitchingAndManaging()
        {
            var offer = SignedIn();

            Assert.That(offer.CanSignOut, Is.True);
            Assert.That(offer.CanSwitch, Is.True);
            Assert.That(offer.CanManage, Is.True);
            Assert.That(offer.CanSignIn, Is.False, "already signed in");
            Assert.That(offer.CanPlayOffline, Is.False, "that choice is behind them");
        }

        /// <summary>The player is told who they are, so a shared machine cannot quietly be somebody else.</summary>
        [Test]
        public void SignedIn_SaysWhoIsSignedIn()
        {
            Assert.That(SignedIn("pilot@example.com").Summary, Does.Contain("pilot@example.com"));

            // A signed-in session with no label is still a signed-in session, and must not read as blank.
            var nameless = AccountScreen.Offer(true, "   ", Pilot, false, true, true);
            Assert.That(nameless.Summary, Is.Not.Empty);
            Assert.That(nameless.CanSignOut, Is.True);
        }

        /// <summary>
        /// The pilot name is the one thing the player sees on a board, and no screen was telling them what
        /// theirs was. It is said whether or not they are signed in, because it applies either way.
        /// </summary>
        [Test]
        public void EveryState_TellsThePlayerTheirPilotName()
        {
            Assert.That(SignedIn().Summary, Does.Contain(Pilot));
            Assert.That(SignedOut().Summary, Does.Contain(Pilot));
            Assert.That(SignedOut(canOpen: false).Summary, Does.Contain(Pilot));
        }

        /// <summary>
        /// A player with no name is told what will happen rather than shown a blank, because "Pilot" is
        /// what the boards will actually print for them.
        /// </summary>
        [Test]
        public void WithNoPilotName_TheDefaultIsExplained()
        {
            var offer = AccountScreen.Offer(false, null, "  ", false, true, true);

            Assert.That(offer.Summary, Is.Not.Empty);
            Assert.That(offer.Summary, Does.Contain("Pilot"));
        }

        /// <summary>
        /// Signing out is not the only reason a player is not on a board: they may never have signed in.
        /// The screen has to say what to do about it, not merely that it is so.
        /// </summary>
        [Test]
        public void SignedOut_SaysHowToGetOntoTheBoards()
        {
            var said = SignedOut().Summary.ToLowerInvariant();

            Assert.That(said, Does.Contain("sign in"));
            Assert.That(said, Does.Contain("this device").Or.Contain("online"));
        }

        /// <summary>
        /// The player id used to stand in for a name here, which reads as "Signed in as player G4Vq9j..."
        /// and tells nobody anything. With no email in the token the screen says what is actually true.
        /// </summary>
        [Test]
        public void SignedInWithNoEmail_DoesNotInventAName()
        {
            var summary = AccountScreen.Offer(true, string.Empty, Pilot, false, true, true).Summary;

            Assert.That(summary, Does.Contain("Unity account"));
            Assert.That(summary, Does.Not.Contain("player "));
        }

        /// <summary>
        /// A second attempt started on top of the first is how two half-finished sessions end up racing,
        /// and the player cannot see that happening because the page is not in the game.
        /// </summary>
        [Test]
        public void WhileAnAttemptIsInFlight_NothingCanBePressed()
        {
            var busy = AccountScreen.Offer(false, null, Pilot, true, true, true);

            Assert.That(busy.CanSignIn, Is.False);
            Assert.That(busy.CanSignOut, Is.False);
            Assert.That(busy.CanSwitch, Is.False);
            Assert.That(busy.CanManage, Is.False);
            Assert.That(busy.CanPlayOffline, Is.False);
            Assert.That(busy.IsIdle, Is.False);
            Assert.That(busy.Summary, Is.Not.Empty, "and it says why everything is dead");

            // Being signed in already does not make it safe to press things mid-attempt either.
            Assert.That(AccountScreen.Offer(true, "someone", Pilot, true, true, true).CanSignOut, Is.False);
        }

        /// <summary>
        /// A build that cannot open the page must not offer a button that does nothing. It still has to let
        /// the player past, or the game would be unreachable.
        /// </summary>
        [Test]
        public void WhenTheBuildCannotOpenThePage_SignInIsNotOffered()
        {
            var offer = SignedOut(canOpen: false);

            Assert.That(offer.CanSignIn, Is.False);
            Assert.That(offer.CanPlayOffline, Is.True, "the player must still be able to get into the game");
            Assert.That(offer.Summary, Is.Not.Empty);
        }

        /// <summary>Switching accounts needs the sign-in page, so it goes when the page does.</summary>
        [Test]
        public void WhenTheBuildCannotOpenThePage_SwitchingIsNotOfferedEither()
        {
            var offer = AccountScreen.Offer(true, "someone", Pilot, false, canOpenSignIn: false, canOpenPortal: true);

            Assert.That(offer.CanSwitch, Is.False);
            Assert.That(offer.CanSignOut, Is.True, "signing out needs no page");
            Assert.That(offer.CanManage, Is.True);
        }

        [Test]
        public void WhenThePortalIsUnavailable_ManagingIsNotOffered() =>
            Assert.That(AccountScreen.Offer(true, "someone", Pilot, false, true, canOpenPortal: false).CanManage,
                Is.False);

        /// <summary>
        /// Changing your mind is not an error. Dressing a cancellation as a failure teaches the player that
        /// the game is broken when they simply closed a tab.
        /// </summary>
        [Test]
        public void Cancelling_IsNotReportedAsAFailure()
        {
            var said = AccountScreen.Explain(new AccountResult(AccountStatus.Cancelled));

            Assert.That(said, Is.Not.Empty);
            Assert.That(said.ToLowerInvariant(), Does.Not.Contain("error"));
            Assert.That(said.ToLowerInvariant(), Does.Not.Contain("fail"));
        }

        [Test]
        public void Success_SaysNothing() =>
            Assert.That(AccountScreen.Explain(AccountResult.Ok), Is.Empty);

        [Test]
        public void EveryFailure_SaysSomethingThePlayerCanRead()
        {
            foreach (var status in new[]
                     {
                         AccountStatus.Refused, AccountStatus.Unreachable, AccountStatus.Unsupported
                     })
                Assert.That(AccountScreen.Explain(new AccountResult(status)), Is.Not.Empty,
                    $"{status} left the player with a blank line");
        }

        /// <summary>
        /// Still in flight is not a failure. It used to be explained as "That did not work", while the page
        /// the player was meant to finish in sat behind the game, which invites signing in twice.
        /// </summary>
        [Test]
        public void StillWorking_IsNotReportedAsAFailure()
        {
            var said = AccountScreen.Explain(new AccountResult(AccountStatus.Working));

            Assert.That(said, Is.Not.Empty);
            Assert.That(said.ToLowerInvariant(), Does.Not.Contain("did not work"));
            Assert.That(said.ToLowerInvariant(), Does.Not.Contain("fail"));
        }

        /// <summary>
        /// The service assigns a name to anybody who has not chosen one, so a signed-in player always has
        /// one. An empty name means the answer has not arrived, and claiming they have none put a sentence
        /// on screen that was simply false while their account said SeriousForgottenSnowflake.
        /// </summary>
        [Test]
        public void SignedInWithNoNameYet_DoesNotClaimTheyHaveNone()
        {
            var summary = AccountScreen.Offer(true, "pilot@example.com", "", false, true, true).Summary;

            Assert.That(summary.ToLowerInvariant(), Does.Not.Contain("no pilot name"));
            Assert.That(summary, Is.Not.Empty);
        }

        /// <summary>The service's own wording is used when there is some, because it is more specific.</summary>
        [Test]
        public void ARefusalWithAReason_ShowsTheReason() =>
            Assert.That(AccountScreen.Explain(new AccountResult(AccountStatus.Refused, "Account is locked.")),
                Is.EqualTo("Account is locked."));
    }
}
