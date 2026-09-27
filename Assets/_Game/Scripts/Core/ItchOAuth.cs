using System;
using System.Collections.Generic;

namespace YASS.Core
{
    /// <summary>What came back from itch.io's sign-in page.</summary>
    public readonly struct ItchCallback
    {
        /// <summary>The itch access token, when there is one. Never logged and never shown.</summary>
        public readonly string AccessToken;

        public readonly AccountStatus Status;

        /// <summary>What to put on the screen. Written for the player.</summary>
        public readonly string Problem;

        public ItchCallback(AccountStatus status, string accessToken = null, string problem = null)
        {
            Status = status;
            AccessToken = accessToken ?? string.Empty;
            Problem = problem ?? string.Empty;
        }

        public bool Succeeded => Status == AccountStatus.Succeeded && !string.IsNullOrEmpty(AccessToken);
    }

    /// <summary>
    /// The rules of signing in with itch.io: what page to send the player to, and what to make of what comes
    /// back (GDD "Scoring", Leaderboards). Used only by the web build, which is the one build Unity Player
    /// Accounts cannot serve at all.
    /// </summary>
    /// <remarks>
    /// **itch.io offers the implicit flow only.** There is no authorization-code exchange and no client
    /// secret: `response_type=token` is the only option, and the token arrives in the URL fragment. That is
    /// the whole reason this class deals in fragments rather than in a code to redeem.
    ///
    /// **The token this returns proves nothing yet.** Anybody can put any string in a URL fragment, so the
    /// token is worth exactly as much as the check that follows it: the Cloud Code module spends it against
    /// itch's own API and believes only what itch says back. Nothing here may be treated as an identity, and
    /// the game must never decide who somebody is from what this parsed.
    ///
    /// Engine-free and pure, so every branch below is reachable from a unit test. That matters more here than
    /// in most places: the failure modes are a hostile query string and a redirect that did not come from the
    /// page we opened, and neither is convenient to produce by hand in a browser.
    /// </remarks>
    public static class ItchOAuth
    {
        /// <summary>itch.io's sign-in page. Theirs, not ours: the player types their password into it.</summary>
        public const string AuthorizePage = "https://itch.io/user/oauth";

        /// <summary>
        /// The least we can ask for and still know who signed in. `profile:me` reads the username and id and
        /// nothing else: not their games, not their purchases, not their collections. A scope asked for and
        /// never used is one the player was made to grant for no reason, and they see the list.
        /// </summary>
        public const string Scope = "profile:me";

        /// <summary>
        /// The page the player is sent to, with the state that ties the answer back to this request.
        /// </summary>
        /// <remarks>
        /// Every value is escaped. The redirect URI in particular contains a `:` and `/` and would otherwise
        /// arrive at itch cut short, which fails as a mismatch against the registered one and reads as a
        /// misconfigured app rather than as the encoding bug it is.
        /// </remarks>
        public static string PageToOpen(string clientId, string redirectUri, string state)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("An itch.io client id is needed to sign in.", nameof(clientId));

            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new ArgumentException("A redirect page is needed to come back to.", nameof(redirectUri));

            // Refused here rather than defended against later. An empty state is the one value that turns
            // the check below off, so a caller that forgot it would get a flow that looks identical and
            // quietly accepts a redirect from anywhere.
            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("A state is what ties the answer to this request.", nameof(state));

            return AuthorizePage +
                   "?client_id=" + Uri.EscapeDataString(clientId) +
                   "&scope=" + Uri.EscapeDataString(Scope) +
                   "&response_type=token" +
                   "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                   "&state=" + Uri.EscapeDataString(state ?? string.Empty);
        }

        /// <summary>
        /// A fresh, unguessable value to tie one answer to one request. Randomness is passed in rather than
        /// taken from a static, so a test gets the same string twice and this stays engine-free.
        /// </summary>
        /// <remarks>
        /// This is not a secret and does not need to be: its whole job is to be different every time, so that
        /// a redirect somebody kept from an earlier sign-in cannot be replayed into this one.
        /// </remarks>
        public static string NewState(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            return StateFrom(next => random.Next(next));
        }

        /// <summary>
        /// The state the game actually uses, from the system's cryptographic source. <see cref="Random"/> is
        /// neither unguessable nor unpredictably seeded, and this value's only job is to be unguessable, so
        /// the overload above exists for tests and this one is what ships.
        /// </summary>
        public static string NewState()
        {
            using (var source = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                source.GetBytes(bytes);

                var next = 0;
                return StateFrom(limit => bytes[next++ % bytes.Length] % limit);
            }
        }

        static string StateFrom(Func<int, int> pick)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
            var letters = new char[24];

            for (var i = 0; i < letters.Length; i++) letters[i] = alphabet[pick(alphabet.Length)];

            return new string(letters);
        }

        /// <summary>
        /// Reads what itch.io redirected back with. Takes the whole URL or just its fragment, because the two
        /// callers differ: the page we host sends the fragment on, and a test is clearer with the full URL.
        /// </summary>
        /// <remarks>
        /// **The state is checked before the token is looked at.** A redirect whose state does not match the
        /// request we made is not ours, and taking the token out of it anyway is precisely the hole the state
        /// exists to close: somebody else's sign-in, finished in this player's browser, would sign this game
        /// in as them.
        /// </remarks>
        /// <summary>What the browser said, which is either a redirect or a word saying why there is not one.</summary>
        /// <remarks>
        /// In Core, and not in the plugin's C# wrapper, because it decides what the player is told and that
        /// is worth a test. It was in the wrapper for one revision, where it collapsed every non-token answer
        /// to the empty string: a blocked popup, which is the failure this whole flow is most likely to hit
        /// on itch, came out as "Signing in was not finished", and a timeout came out as a security warning.
        /// </remarks>
        public static ItchCallback ReadAnswer(string answer, string expectedState)
        {
            switch (answer)
            {
                case null:
                case "":
                case "cancelled":
                    // The player closed the window or walked away. Not a failure, and not apologised for.
                    return new ItchCallback(AccountStatus.Cancelled);

                case "blocked":
                    // The page would not let the game open a window. Said plainly, because the player can
                    // act on it, and because it is the answer to whether itch's iframe allows popups.
                    return new ItchCallback(AccountStatus.Unsupported,
                        problem: "Your browser blocked the sign-in window. Allow pop-ups for this page and try again.");

                case "busy":
                    return new ItchCallback(AccountStatus.Working);
            }

            const string token = "token ";
            return answer.StartsWith(token, StringComparison.Ordinal)
                ? Parse(answer.Substring(token.Length), expectedState)
                : new ItchCallback(AccountStatus.Refused, problem: "Signing in did not work. Please try again.");
        }

        public static ItchCallback Parse(string url, string expectedState)
        {
            if (string.IsNullOrWhiteSpace(url))
                // Nothing came back at all, which is what closing the window looks like from here.
                return new ItchCallback(AccountStatus.Cancelled);

            // An empty expectation would turn the check below off, so it is refused rather than honoured.
            // PageToOpen will not produce a request without a state, so reaching here without one means a
            // caller has lost it, and continuing would accept a redirect from anywhere.
            if (string.IsNullOrEmpty(expectedState))
                return new ItchCallback(AccountStatus.Refused,
                    problem: "That sign-in did not come back safely. Please try again.");

            var values = FieldsIn(url);

            // Before anything else. An answer that cannot be shown to belong to this request is discarded
            // whatever else it contains, including a perfectly well-formed token.
            {
                values.TryGetValue("state", out var state);
                if (!FixedTimeEquals(state, expectedState))
                    return new ItchCallback(AccountStatus.Refused,
                        problem: "That sign-in did not come back safely. Please try again.");
            }

            if (values.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error))
                return ForError(error, values);

            if (values.TryGetValue("access_token", out var token) && !string.IsNullOrEmpty(token))
                return new ItchCallback(AccountStatus.Succeeded, token);

            // Neither a token nor a refusal: the page came back, but not with anything to use.
            return new ItchCallback(AccountStatus.Refused,
                problem: "itch.io did not send a sign-in back. Please try again.");
        }

        /// <summary>
        /// What a refusal from itch.io means to the player. `access_denied` is the ordinary one and is not a
        /// failure at all: it is somebody pressing Cancel, and telling them something went wrong when they
        /// have just decided it should not happen is both wrong and irritating.
        /// </summary>
        static ItchCallback ForError(string error, IReadOnlyDictionary<string, string> values)
        {
            if (error == "access_denied") return new ItchCallback(AccountStatus.Cancelled);

            // itch's own description, when it sent one. It is written for a person rather than a developer,
            // which is not true of most services, so it is worth passing on instead of a message of ours.
            values.TryGetValue("error_description", out var described);

            return new ItchCallback(AccountStatus.Refused,
                problem: string.IsNullOrWhiteSpace(described)
                    ? "itch.io would not complete the sign-in. Please try again."
                    : described);
        }

        /// <summary>
        /// The name/value pairs from a redirect. **The fragment only, never the query.** itch's implicit flow
        /// puts the token after the '#', and the query is the half that survives being pasted into a link, a
        /// referrer header or a server log, so a token found there did not come from itch's redirect and is
        /// not treated as one. A string with no '#' at all is taken as a bare fragment, which is how the web
        /// plugin hands it over.
        /// </summary>
        static Dictionary<string, string> FieldsIn(string url)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            var hash = url.IndexOf('#');
            ReadPairs(hash >= 0 ? url.Substring(hash + 1) : url, values);

            return values;
        }

        static void ReadPairs(string text, IDictionary<string, string> into)
        {
            foreach (var pair in text.Split('&'))
            {
                if (pair.Length == 0) continue;

                var equals = pair.IndexOf('=');
                if (equals <= 0) continue;

                var name = Unescape(pair.Substring(0, equals));

                // First one wins. A redirect carrying "access_token" twice is somebody trying their luck,
                // and the half that was checked must be the half that is used.
                if (!into.ContainsKey(name)) into[name] = Unescape(pair.Substring(equals + 1));
            }
        }

        /// <summary>
        /// Percent-decoding, with '+' read as a space. Never throws: a malformed escape in a redirect is
        /// somebody poking at it, and the right answer is a value that fails the checks below rather than an
        /// exception out of a parser.
        /// </summary>
        static string Unescape(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value.Replace("+", "%20"));
            }
            catch (Exception)
            {
                return value;
            }
        }

        /// <summary>
        /// Compares without giving away where two values first differ. The state is not a secret, so this is
        /// belt and braces; it costs nothing and means nobody has to work out whether it mattered here.
        /// </summary>
        static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            var difference = 0;
            for (var i = 0; i < a.Length; i++) difference |= a[i] ^ b[i];

            return difference == 0;
        }
    }
}
