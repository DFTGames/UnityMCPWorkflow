using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The itch.io sign-in rules. Worth testing closely because the interesting inputs are hostile ones: a
    /// redirect that did not come from the page we opened, a token planted in a query string, a state that
    /// nearly matches. None of those is convenient to produce in a browser, and all of them decide who the
    /// game thinks the player is.
    /// </summary>
    public class ItchOAuthTests
    {
        const string ClientId = "yass-2026";
        const string Redirect = "https://dftgames.github.io/UnityMCPWorkflow/itch-callback.html";
        const string State = "abc123";

        [Test]
        public void ThePageToOpen_IsItchOwn_AndAsksForTheImplicitFlow()
        {
            var page = ItchOAuth.PageToOpen(ClientId, Redirect, State);

            Assert.That(page, Does.StartWith("https://itch.io/user/oauth?"),
                "the player types their password into itch's page, so it must be itch's page");
            Assert.That(page, Does.Contain("response_type=token"),
                "itch offers the implicit flow only; anything else is refused by them");
            Assert.That(page, Does.Contain("client_id=yass-2026"));
            Assert.That(page, Does.Contain("state=abc123"));
        }

        /// <summary>
        /// The redirect contains `:` and `/`, which arrive truncated if they are not escaped. itch then
        /// rejects the request as a mismatch against the registered URI, which reads as a misconfigured app
        /// rather than as an encoding bug, so this is worth pinning rather than eyeballing once.
        /// </summary>
        [Test]
        public void TheRedirect_IsEscaped()
        {
            var page = ItchOAuth.PageToOpen(ClientId, Redirect, State);

            Assert.That(page, Does.Contain("redirect_uri=https%3A%2F%2Fdftgames.github.io"));
            Assert.That(page, Does.Not.Contain("redirect_uri=https://"), "an unescaped redirect is truncated");
        }

        [Test]
        public void TheScope_IsOnlyTheProfile()
        {
            Assert.That(ItchOAuth.Scope, Is.EqualTo("profile:me"),
                "a wider scope is one the player is made to grant for nothing, and they see the list");

            var page = ItchOAuth.PageToOpen(ClientId, Redirect, State);
            Assert.That(page, Does.Contain("scope=profile%3Ame"));
        }

        [Test]
        public void WithoutAClientId_ItSaysSo_RatherThanOpeningABrokenPage()
        {
            Assert.Throws<ArgumentException>(() => ItchOAuth.PageToOpen("", Redirect, State));
            Assert.Throws<ArgumentException>(() => ItchOAuth.PageToOpen(ClientId, " ", State));
        }

        /// <summary>
        /// An empty state turns the check below off, so it is refused at both ends rather than honoured.
        /// </summary>
        [Test]
        public void WithoutAState_NothingIsBuiltAndNothingIsBelieved()
        {
            Assert.Throws<ArgumentException>(() => ItchOAuth.PageToOpen(ClientId, Redirect, ""));
            Assert.Throws<ArgumentException>(() => ItchOAuth.PageToOpen(ClientId, Redirect, null));

            // And if one is somehow lost by the time the answer arrives, the answer is not trusted.
            Assert.That(ItchOAuth.Parse("#access_token=tok123&state=abc123", "").Status,
                Is.EqualTo(AccountStatus.Refused));
            Assert.That(ItchOAuth.Parse("#access_token=tok123&state=abc123", null).Status,
                Is.EqualTo(AccountStatus.Refused));
        }

        /// <summary>
        /// The token lives in the fragment. A query string survives being pasted into a link, logged by a
        /// server or sent in a referrer, so a token found there did not come from itch's redirect.
        /// </summary>
        [Test]
        public void AQueryStringToken_IsNotAToken()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb?access_token=planted&state=" + State, State);

            Assert.That(answer.Succeeded, Is.False);
            Assert.That(answer.AccessToken, Is.Empty);
        }

        [Test]
        public void AState_IsDifferentEveryTime()
        {
            var random = new Random(1);
            var first = ItchOAuth.NewState(random);
            var second = ItchOAuth.NewState(random);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Has.Length.GreaterThanOrEqualTo(16), "a guessable state is not worth having");
            Assert.That(first, Does.Match("^[a-z0-9]+$"), "it travels in a URL, so it stays unescaped");
        }

        [Test]
        public void AGoodRedirect_YieldsTheToken()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb#access_token=tok123&state=" + State, State);

            Assert.That(answer.Succeeded, Is.True);
            Assert.That(answer.AccessToken, Is.EqualTo("tok123"));
        }

        /// <summary>A bare fragment, which is how the hosted callback page sends it on.</summary>
        [Test]
        public void ABareFragment_IsReadTheSameWay()
        {
            var answer = ItchOAuth.Parse("access_token=tok123&state=" + State, State);

            Assert.That(answer.Succeeded, Is.True);
            Assert.That(answer.AccessToken, Is.EqualTo("tok123"));
        }

        /// <summary>
        /// The reason the state exists. A redirect from somebody else's sign-in, finished in this player's
        /// browser, would otherwise sign the game in as them.
        /// </summary>
        [Test]
        public void ARedirect_WithTheWrongState_IsRefused_TokenOrNot()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb#access_token=tok123&state=somebody-else", State);

            Assert.That(answer.Succeeded, Is.False);
            Assert.That(answer.Status, Is.EqualTo(AccountStatus.Refused));
            Assert.That(answer.AccessToken, Is.Empty, "a token from an unverified redirect must not escape");
        }

        [Test]
        public void ARedirect_WithNoStateAtAll_IsRefused()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb#access_token=tok123", State);

            Assert.That(answer.Status, Is.EqualTo(AccountStatus.Refused));
            Assert.That(answer.AccessToken, Is.Empty);
        }

        /// <summary>
        /// Cancelling is not a failure. Telling somebody that something went wrong when they have just
        /// decided it should not happen is both untrue and irritating.
        /// </summary>
        [Test]
        public void ThePlayerSayingNo_IsCancelled_NotAnError()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb#error=access_denied&state=" + State, State);

            Assert.That(answer.Status, Is.EqualTo(AccountStatus.Cancelled));
            Assert.That(answer.Problem, Is.Empty, "there is nothing to apologise for");
        }

        [Test]
        public void ClosingTheWindow_IsCancelled()
        {
            Assert.That(ItchOAuth.Parse(null, State).Status, Is.EqualTo(AccountStatus.Cancelled));
            Assert.That(ItchOAuth.Parse("", State).Status, Is.EqualTo(AccountStatus.Cancelled));
            Assert.That(ItchOAuth.Parse("   ", State).Status, Is.EqualTo(AccountStatus.Cancelled));
        }

        [Test]
        public void AnotherError_IsRefused_AndPassesOnWhatItchSaid()
        {
            var answer = ItchOAuth.Parse(
                "https://example.org/cb#error=invalid_scope&error_description=That+scope+is+not+allowed&state=" + State,
                State);

            Assert.That(answer.Status, Is.EqualTo(AccountStatus.Refused));
            Assert.That(answer.Problem, Is.EqualTo("That scope is not allowed"), "'+' is a space in a URL");
        }

        [Test]
        public void AnErrorWithNoDescription_StillSaysSomethingUseful()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb#error=server_error&state=" + State, State);

            Assert.That(answer.Status, Is.EqualTo(AccountStatus.Refused));
            Assert.That(answer.Problem, Is.Not.Empty);
        }

        [Test]
        public void ARedirectWithNeitherTokenNorError_IsRefused()
        {
            var answer = ItchOAuth.Parse("https://example.org/cb#state=" + State, State);

            Assert.That(answer.Status, Is.EqualTo(AccountStatus.Refused));
            Assert.That(answer.Problem, Is.Not.Empty);
        }

        /// <summary>
        /// The token lives in the fragment. A second one bolted into the query string must not be the one
        /// that is believed, because the query is the half an attacker can put anywhere a link goes.
        /// </summary>
        [Test]
        public void TheFragmentWins_OverAQueryStringClaimingOtherwise()
        {
            var answer = ItchOAuth.Parse(
                "https://example.org/cb?access_token=planted#access_token=real&state=" + State, State);

            Assert.That(answer.AccessToken, Is.EqualTo("real"));
        }

        [Test]
        public void ARepeatedField_TakesTheFirst()
        {
            var answer = ItchOAuth.Parse(
                "https://example.org/cb#access_token=real&access_token=planted&state=" + State, State);

            Assert.That(answer.AccessToken, Is.EqualTo("real"));
        }

        /// <summary>A state that is a prefix of the real one, or longer than it, is not the real one.</summary>
        [Test]
        public void ANearlyRightState_IsStillWrong()
        {
            Assert.That(ItchOAuth.Parse("#access_token=t&state=abc12", State).Status,
                Is.EqualTo(AccountStatus.Refused));
            Assert.That(ItchOAuth.Parse("#access_token=t&state=abc1234", State).Status,
                Is.EqualTo(AccountStatus.Refused));
        }

        [Test]
        public void EscapedValues_AreDecoded()
        {
            var answer = ItchOAuth.Parse("#access_token=a%2Fb%2Bc&state=" + State, State);

            Assert.That(answer.AccessToken, Is.EqualTo("a/b+c"));
        }

        /// <summary>
        /// A malformed escape is somebody poking at the redirect. The answer is a value that fails the checks,
        /// never an exception out of a parser that the caller has no way to handle.
        /// </summary>
        /// <summary>
        /// What the browser says, and what the player is told about it. This mapping lived in the plugin's
        /// C# wrapper for one revision, where it flattened every non-token answer to the empty string: a
        /// blocked popup, which is the most likely failure on itch, came out as "Signing in was not
        /// finished", and a timeout came out as a security warning. Both were the wrong way round.
        /// </summary>
        [Test]
        public void ABlockedWindow_IsSaidPlainly_AndIsNotACancellation()
        {
            var blocked = ItchOAuth.ReadAnswer("blocked", State);

            Assert.That(blocked.Status, Is.EqualTo(AccountStatus.Unsupported));
            Assert.That(blocked.Problem, Is.Not.Empty, "the player can act on this one, so they are told");
            Assert.That(blocked.Problem.ToLowerInvariant(), Does.Contain("pop-up").Or.Contain("popup"));
        }

        [Test]
        public void TheOtherAnswers_MeanWhatTheySay()
        {
            Assert.That(ItchOAuth.ReadAnswer("cancelled", State).Status, Is.EqualTo(AccountStatus.Cancelled));
            Assert.That(ItchOAuth.ReadAnswer("", State).Status, Is.EqualTo(AccountStatus.Cancelled));
            Assert.That(ItchOAuth.ReadAnswer(null, State).Status, Is.EqualTo(AccountStatus.Cancelled));

            // An attempt already open is not a failure: the player is pointed at the window they have.
            Assert.That(ItchOAuth.ReadAnswer("busy", State).Status, Is.EqualTo(AccountStatus.Working));

            // Anything unrecognised is a failure, not a silent success.
            Assert.That(ItchOAuth.ReadAnswer("something else entirely", State).Status,
                Is.EqualTo(AccountStatus.Refused));
        }

        [Test]
        public void ATokenAnswer_IsReadAsARedirect()
        {
            var good = ItchOAuth.ReadAnswer("token access_token=tok123&state=" + State, State);

            Assert.That(good.Succeeded, Is.True);
            Assert.That(good.AccessToken, Is.EqualTo("tok123"));

            // And the state is still checked when it arrives this way.
            Assert.That(ItchOAuth.ReadAnswer("token access_token=tok123&state=wrong", State).Status,
                Is.EqualTo(AccountStatus.Refused));
        }

        [Test]
        public void AMalformedRedirect_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ItchOAuth.Parse("#access_token=%zz%&state=" + State, State));
            Assert.DoesNotThrow(() => ItchOAuth.Parse("############", State));
            Assert.DoesNotThrow(() => ItchOAuth.Parse("=&=&=", State));
            Assert.DoesNotThrow(() => ItchOAuth.Parse("#&&&&", State));
        }
    }
}
