using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The player's account on the web build, through itch.io (GDD "Scoring", Leaderboards).
    /// </summary>
    /// <remarks>
    /// **Only the web build has this.** Unity Player Accounts has no browser implementation on WebGL at all,
    /// so that build could offer no sign-in and its players were stuck on boards kept in their own browser.
    /// The game is published on itch.io, so itch is the identity its web players already have.
    ///
    /// **The game never sees a password**, exactly as with Unity's own accounts: itch's page takes it, in a
    /// window of itch's, and hands back a token. Changing a password or closing the account is itch's, through
    /// <see cref="OpenAccountPortal"/>.
    ///
    /// **The token this gets is not proof of anything.** It comes out of a browser, where anybody can put
    /// anything in a URL. It is worth only what the Cloud Code module makes of it, and that module spends it
    /// against itch's own API and believes nothing else. Nothing here treats it as an identity, which is why
    /// this class never reads a username out of it.
    ///
    /// **The anonymous sign-in below is not a fallback.** Cloud Code cannot be called by nobody, so the game
    /// has to be signed in as *someone* before it can ask who the player really is. That throwaway player is
    /// then handed to the module, which links the itch account onto it where it can, so a player who has been
    /// playing the web build without signing in keeps the scores they have already set.
    /// </remarks>
    public sealed class ItchAccounts : IPlayerAccounts
    {
        /// <summary>The Cloud Code script that does the half of this a client is not allowed to do.</summary>
        public const string SignInScript = "SignInWithItch";

        /// <summary>
        /// How long to wait for the player to finish on itch's page. Generous: they may have to find a
        /// password, or make an itch account from scratch, before they come back.
        /// </summary>
        public const float SignInTimeoutSeconds = 300f;

        /// <summary>Where itch sends them back to. Ours, and registered with itch as the redirect.</summary>
        public const string CallbackPage =
            "https://dftgames.github.io/UnityMCPWorkflow/itch-callback.html";

        /// <summary>
        /// Remembers that this browser's session came from an itch account, so a returning player is not
        /// presented with a device-local identity dressed up as a signed-in one. Same reasoning as the Unity
        /// account flow: the session token alone cannot say what established it.
        /// </summary>
        const string LinkedKey = "account.itch";

        /// <summary>
        /// The itch handle, kept so a returning player sees whose account they are signed in with. The
        /// session token restores the player but carries no handle, so without this the account screen
        /// greeted everyone who came back with a blank where their name had been.
        /// </summary>
        const string NameKey = "account.itch.name";

        readonly List<Action<bool>> _resuming = new List<Action<bool>>();
        readonly ISettingsStore _store;
        readonly UgsPilotName _pilot = new UgsPilotName(nameof(ItchAccounts));
        readonly IItchBrowser _given;

        bool _busy;

        /// <summary>What the server said when it would not sign the player in, for telling them which
        /// kind of failure it was. Cleared at the start of every attempt.</summary>
        ItchSignInAnswer _refusal;

        /// <summary>
        /// Which step failed and how, for showing as well as logging. Three unrelated faults reach the
        /// player as the same "could not reach the service", and on the web the log is a console nobody
        /// has open, so without this a failure is indistinguishable from a flat network.
        /// </summary>
        string _fault = string.Empty;

        /// <summary>
        /// Bumped by anything that changes who is signed in, so an attempt finishing after the player has
        /// moved on cannot write its answer over the newer truth.
        /// </summary>
        int _generation;

        public ItchAccounts(ISettingsStore store = null, IItchBrowser browser = null)
        {
            _store = store;
            _given = browser;
        }

        /// <summary>
        /// The injected browser, or the shared one made on first use. Resolved here rather than in the
        /// constructor because the shared one creates a DontDestroyOnLoad GameObject: in edit mode that
        /// dirties the open scene, and a dirty scene when a test run starts puts up the modal that wedges
        /// the Editor. Constructing this class must stay free of side effects.
        /// </summary>
        IItchBrowser Browser
        {
            get
            {
                // An explicit null check, not ??: the fallback is a MonoBehaviour, and Unity's fake null
                // defeats the null-coalescing operators.
                if (_given != null) return _given;

                return ItchBrowser.Shared;
            }
        }

        public bool IsSignedIn { get; private set; }

        /// <summary>
        /// The itch username. Unlike the Unity account flow, this one is safe to show and safe on a board:
        /// it is the handle the player already publishes on itch, not an email address. It is still not what
        /// the boards read from, which is the pilot name the service holds.
        /// </summary>
        public string AccountLabel { get; private set; } = string.Empty;

        public string ServiceName => "itch.io";

        /// <summary>
        /// Only where a second window can be opened, and only once the OAuth application has been registered.
        /// A sandboxed iframe without <c>allow-popups</c> blocks it outright, and offering a button that
        /// silently does nothing is worse than not offering one. The real answer only arrives when it is
        /// pressed; this is the best guess available before that.
        /// </summary>
        public bool CanSignIn => ItchSettings.Configured && Browser.CanOpenWindows;

        /// <summary>itch's own settings page, which is where a password is changed or an account closed.</summary>
        public bool CanManageAccount => IsSignedIn;

        public string PilotName => _pilot.Current(IsSignedIn);

        public void FetchPilotName(Action<string> done) => _pilot.Fetch(IsSignedIn, done);

        public void SetPilotName(string name, Action<AccountResult> done) => _pilot.Set(IsSignedIn, name, done);

        public void Resume(Action<bool> signedIn)
        {
            if (IsSignedIn)
            {
                signedIn?.Invoke(true);
                return;
            }

            if (signedIn != null) _resuming.Add(signedIn);
            if (_busy) return;

            ResumeNow();
        }

        /// <summary>
        /// Signs in again from what this browser remembers, without opening anything. The session token is
        /// what carries a custom-id player across a reload, and signing in anonymously is what redeems it:
        /// the service returns whoever that token belongs to, which is the itch player, not a new anonymous
        /// one. The flag is what stops this presenting a genuinely anonymous session as a signed-in account.
        /// </summary>
        async void ResumeNow()
        {
            _busy = true;
            var mine = ++_generation;
            var ok = false;

            try
            {
                if (await Started())
                {
                    if (AuthenticationService.Instance.IsSignedIn)
                    {
                        ok = WasLinkedToItch;
                    }
                    else if (WasLinkedToItch && AuthenticationService.Instance.SessionTokenExists)
                    {
                        await UgsCalls.WithDeadline(
                            AuthenticationService.Instance.SignInAnonymouslyAsync(), UgsCalls.TimeoutSeconds);
                        ok = AuthenticationService.Instance.IsSignedIn;
                    }
                }
            }
            catch (Exception problem)
            {
                UgsCalls.Log(nameof(ItchAccounts), "resume", problem);
                ok = false;
            }

            _busy = false;

            // The handle comes back from the store, because a resume is the one path that never learns
            // it from the server. A failed resume keeps whatever was saved rather than erasing it.
            if (mine == _generation) Remember(ok, ok ? RememberedName : AccountLabel, forget: false);
            Drain(mine == _generation && ok);
        }

        bool WasLinkedToItch => _store != null && _store.GetBool(LinkedKey, false);

        public async void SignIn(Action<AccountResult> done)
        {
            if (!CanSignIn)
            {
                Answer(done, new AccountResult(AccountStatus.Unsupported,
                    "This page will not let the game open a sign-in window."));
                return;
            }

            if (_busy)
            {
                Answer(done, new AccountResult(AccountStatus.Working));
                return;
            }

            _busy = true;
            _fault = string.Empty;
            var mine = ++_generation;
            var answer = new AccountResult(AccountStatus.Cancelled);
            ItchSignInAnswer established = null;

            try
            {
                // **The window is opened before anything is awaited, and that ordering is the feature.**
                // `window.open` needs the transient activation that belongs to the click, and an awaited
                // network call spends it: every current browser blocks a popup opened afterwards. The one
                // player it would have blocked is the one this exists for, because a returning player is
                // already signed in and takes a path with no suspension in it, so it works by hand on the
                // second press and on every machine that has signed in once.
                //
                // Nothing the URL needs is asynchronous, so none of it has to wait. The services and the
                // anonymous session are started alongside and are ready long before the player comes back.
                var state = ItchOAuth.NewState();
                var browsing = Browser.OpenSignIn(
                    ItchOAuth.PageToOpen(ItchClientId, CallbackPage, state),
                    CallbackOrigin, SignInTimeoutSeconds);

                var preparing = PrepareTheSession();

                var callback = ItchOAuth.ReadAnswer(await browsing, state);
                if (!callback.Succeeded)
                {
                    answer = new AccountResult(callback.Status, callback.Problem);
                    return;
                }

                if (!await preparing)
                {
                    answer = new AccountResult(AccountStatus.Unreachable, Unreachable());
                    return;
                }

                established = await Establish(callback.AccessToken);
                if (established == null)
                {
                    answer = new AccountResult(AccountStatus.Refused, ExplainRefusal(_refusal));
                    return;
                }

                answer = AccountResult.Ok;
            }
            catch (Exception problem)
            {
                UgsCalls.Log(nameof(ItchAccounts), "sign in", problem);

                // Only if nothing closer to the fault has already named it: Establish tags which of its two
                // steps threw, and that is more use than "somewhere inside signing in".
                if (string.IsNullOrEmpty(_fault)) _fault = Describe("signing in", problem, null);

                answer = new AccountResult(AccountStatus.Unreachable, Unreachable());
            }
            finally
            {
                _busy = false;

                // Only now, and only if nothing newer has happened. Signing out while the popup was open
                // bumps the generation, and writing the session in anyway would put the player back online
                // moments after they asked not to be: the same fault UgsAccounts carries a comment about.
                if (mine == _generation)
                {
                    if (established != null)
                    {
                        Remember(true, established.pilotName);

                        // Only when the server says this is the first time this itch account has been
                        // attached to a player. A returning player has had every chance to choose a name,
                        // and taking their itch handle again each time would undo that choice on every
                        // sign-in. Not awaited: the session is already good, and the name is a nicety.
                        if (established.linked) AdoptTheItchName(established.pilotName);
                    }
                }
                else
                {
                    // Whatever this attempt achieved belongs to nobody now. Say it was abandoned rather
                    // than reporting a success the player has already walked away from.
                    answer = new AccountResult(AccountStatus.Cancelled);
                }

                Drain(IsSignedIn);

                // Outside the try on purpose: the callback redraws a screen and can navigate, and a throw in
                // there must not be caught above and reported as a second, contradictory answer.
                Answer(done, answer);
            }
        }

        /// <summary>
        /// Gets the services up and this browser signed in as somebody. Anonymously, because Cloud Code
        /// cannot be called by nobody, and being signed in as a throwaway is what buys the right to ask who
        /// the player really is. Runs while the player is on itch's page, so it costs them no waiting.
        /// </summary>
        async Task<bool> PrepareTheSession()
        {
            try
            {
                if (!await Started())
                {
                    _fault = "the services would not start";
                    return false;
                }

                if (AuthenticationService.Instance.IsSignedIn) return true;

                await UgsCalls.WithDeadline(
                    AuthenticationService.Instance.SignInAnonymouslyAsync(), UgsCalls.TimeoutSeconds);

                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception problem)
            {
                UgsCalls.Log(nameof(ItchAccounts), "prepare the session", problem);
                _fault = Describe("preparing the session", problem, null);
                return false;
            }
        }

        /// <summary>
        /// Spends the itch token on a Unity session. Answers null when it could not, with
        /// <see cref="_refusal"/> holding what the server said so the player can be told which it was.
        /// </summary>
        async Task<ItchSignInAnswer> Establish(string itchToken)
        {
            _refusal = null;
            ItchSignInAnswer answer;

            // The two steps are caught separately because they fail for entirely different reasons and the
            // screen cannot tell them apart otherwise: one is the server refusing or being unreachable, the
            // other is this client's own session state.
            try
            {
                answer = await UgsCalls.WithDeadline(
                    CloudCodeService.Instance.CallEndpointAsync<ItchSignInAnswer>(
                        SignInScript, new Dictionary<string, object> { { "itchToken", itchToken } }),
                    UgsCalls.TimeoutSeconds);
            }
            catch (Exception problem)
            {
                _fault = Describe("calling " + SignInScript, problem, itchToken);
                throw;
            }

            _refusal = answer;

            if (answer == null || !answer.ok || string.IsNullOrEmpty(answer.idToken)) return null;

            try
            {
                // **The anonymous session has to go first, and only here.**
                // ProcessAuthenticationTokens will not replace a live session: it throws
                // AuthenticationException 10000, "the player is already signed in", and the whole sign-in
                // then reports itself as an unreachable service. But the sign-out cannot happen any earlier
                // than this line, because the anonymous player's own access token is what the call above
                // spends to *link* the itch account onto them, which is what preserves the scores they set
                // before signing in. Sign out too early and the link is impossible; too late and the tokens
                // are refused.
                //
                // Credentials are kept rather than cleared. If the line below fails, the player is left able
                // to resume the session they already had; clearing them would strand whoever had been
                // playing anonymously, because their runs live under that player id on the service.
                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut(clearCredentials: false);

                // The server has decided who this is. This is the line that makes it the game's session too.
                AuthenticationService.Instance.ProcessAuthenticationTokens(answer.idToken, answer.sessionToken);
            }
            catch (Exception problem)
            {
                _fault = Describe("taking the session", problem, itchToken);
                throw;
            }

            return AuthenticationService.Instance.IsSignedIn ? answer : null;
        }

        /// <summary>
        /// Takes the itch username as the pilot name for a player who has just been created, so their first
        /// board entry says who they are rather than a name the service invented for them.
        /// </summary>
        /// <remarks>
        /// Whether a name was invented or chosen is not something the service will tell you, and guessing it
        /// from the shape of the name would be wrong the first time somebody was genuinely called something
        /// that looked generated. The server's answer about a brand new link is a fact rather than a guess,
        /// so that is what this hangs off.
        /// </remarks>
        async void AdoptTheItchName(string itchName)
        {
            if (string.IsNullOrWhiteSpace(itchName)) return;

            try
            {
                await UgsCalls.WithDeadline(
                    AuthenticationService.Instance.UpdatePlayerNameAsync(Leaderboards.CleanName(itchName)),
                    UgsCalls.TimeoutSeconds);
            }
            catch (Exception problem)
            {
                // Losing this costs the player a nicer default name and nothing else, so it is never allowed
                // to fail the sign-in they actually asked for.
                UgsCalls.Log(nameof(ItchAccounts), "adopt the itch name", problem);
            }
            finally
            {
                _pilot.Fetch(IsSignedIn, null);
            }
        }

        /// <summary>The unreachable wording, with the fault appended while that is switched on.</summary>
        string Unreachable()
        {
            const string said = "Could not reach the service. Check your connection and try again.";

            return ItchSettings.ShowSignInFaults && !string.IsNullOrEmpty(_fault)
                ? said + "\n[" + _fault + "]"
                : said;
        }

        /// <summary>
        /// A fault in one line: which step, what type, the service's code, and a trimmed message.
        /// </summary>
        /// <remarks>
        /// <paramref name="secret"/> is removed from the text. A service's exception message can quote the
        /// request that caused it, and the request that caused this one carries the player's itch token; a
        /// credential on screen would be a worse bug than the one being diagnosed.
        /// </remarks>
        static string Describe(string step, Exception problem, string secret)
        {
            var code = problem is RequestFailedException failed ? " " + failed.ErrorCode : string.Empty;
            var said = problem.Message ?? string.Empty;

            if (!string.IsNullOrEmpty(secret)) said = said.Replace(secret, "<token>");
            if (said.Length > 200) said = said.Substring(0, 200) + "...";

            return $"{step}: {problem.GetType().Name}{code} {said}".Trim();
        }

        static string ExplainRefusal(ItchSignInAnswer answer)
        {
            switch (answer == null ? string.Empty : answer.reason)
            {
                case "itch-refused":
                    return "itch.io would not confirm that sign-in. Please try again.";

                case "backend-misconfigured":
                    // Ours, not theirs. Saying "check your connection" would send them to restart a router
                    // that was never the problem.
                    return "Signing in is not set up correctly in this version.";

                default:
                    return "Signing in did not work. Please try again." + Raw(answer);
            }
        }

        /// <summary>
        /// The server's own word for the refusal, while the fault display is on. `sign-in-failed` and a
        /// missing answer read identically to the player otherwise, and they mean different things: the
        /// first is the custom-id call being refused, which is what a disabled Custom ID provider looks like.
        /// </summary>
        static string Raw(ItchSignInAnswer answer)
        {
            if (!ItchSettings.ShowSignInFaults) return string.Empty;

            return answer == null
                ? "\n[no answer from " + SignInScript + "]"
                : "\n[" + SignInScript + ": " +
                  (string.IsNullOrEmpty(answer.reason) ? "no reason given" : answer.reason) + "]";
        }

        public void SignOut(Action<AccountResult> done)
        {
            _generation++;

            try
            {
                if (UgsCalls.ServicesReady)
                    AuthenticationService.Instance.SignOut(clearCredentials: true);
            }
            catch (Exception problem)
            {
                UgsCalls.Log(nameof(ItchAccounts), "sign out", problem);
            }
            finally
            {
                // Signed out locally whatever the service said. A sign-out that reports failure and leaves
                // the player apparently signed in is worse than one that forgets slightly too eagerly.
                Remember(false, string.Empty);
                _busy = false;
                Drain(false);
                Answer(done, AccountResult.Ok);
            }
        }

        /// <summary>
        /// Signing out of the game does not sign them out of itch, which is a browser session belonging to
        /// the player rather than to this game. So switching means itch's own page will very likely sign them
        /// straight back in as the same person: the way to be somebody else is to change that on itch.
        /// </summary>
        public void SwitchAccount(Action<AccountResult> done) => SignOut(_ => SignIn(done));

        public void OpenAccountPortal() => Application.OpenURL("https://itch.io/user/settings");

        string RememberedName => _store == null ? string.Empty : _store.GetString(NameKey, string.Empty);

        /// <summary>
        /// Writes down who is signed in. <paramref name="forget"/> is false for a resume that did not work:
        /// one launch with no network would otherwise erase the fact that this browser is itch-linked, and
        /// the only way back from that is another popup, which may be blocked.
        /// </summary>
        void Remember(bool signedIn, string label, bool forget = true)
        {
            IsSignedIn = signedIn;
            AccountLabel = signedIn ? label ?? string.Empty : string.Empty;

            if (!signedIn) _pilot.Forget();
            else if (!_pilot.Known) _pilot.Fetch(true, null); // warm it; the answer lands later

            if (_store == null) return;
            if (!signedIn && !forget) return;

            _store.SetBool(LinkedKey, signedIn);
            _store.SetString(NameKey, signedIn ? AccountLabel : string.Empty);
            _store.Save();
        }

        void Drain(bool signedIn)
        {
            if (_resuming.Count == 0) return;

            var waiting = _resuming.ToArray();
            _resuming.Clear();
            foreach (var caller in waiting) caller?.Invoke(signedIn);
        }

        /// <summary>
        /// The itch OAuth application's client id. Not a secret: it is in the URL every player is sent to,
        /// and itch's implicit flow has no secret to keep. It identifies the application, nothing more.
        /// </summary>
        static string ItchClientId => ItchSettings.ClientId;

        /// <summary>
        /// The origin of the callback page, which is the only origin a token is accepted from. Derived from
        /// the page itself so the two cannot drift apart.
        /// </summary>
        static string CallbackOrigin
        {
            get
            {
                var page = new Uri(CallbackPage);
                return page.Scheme + "://" + page.Authority;
            }
        }

        static Task<bool> Started() =>
            UgsSession.Start(work => UgsCalls.WithDeadline(work, UgsCalls.TimeoutSeconds));

        static void Answer(Action<AccountResult> caller, AccountResult result) => caller?.Invoke(result);

        /// <summary>What the Cloud Code script answers with. Field names match its JSON exactly.</summary>
        [Serializable]
        class ItchSignInAnswer
        {
#pragma warning disable CS0649 // filled by the deserialiser, never assigned here
            public bool ok;
            public string reason;
            public string idToken;
            public string sessionToken;
            public string pilotName;

            /// <summary>True only when this itch account has just been attached to a player for the first
            /// time, which is the one moment it is right to take their itch handle as their pilot name.</summary>
            public bool linked;
#pragma warning restore CS0649
        }
    }

    /// <summary>The browser, as this needs it. An interface so the flow above can be tested without one.</summary>
    public interface IItchBrowser
    {
        bool CanOpenWindows { get; }

        /// <summary>
        /// Opens itch's page and answers with whatever came back, which is a URL fragment on success and a
        /// word such as "cancelled" otherwise. Never throws and never waits for ever.
        /// </summary>
        Task<string> OpenSignIn(string page, string callbackOrigin, float timeoutSeconds);
    }
}
