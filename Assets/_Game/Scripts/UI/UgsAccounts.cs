using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The player's account, through Unity Player Accounts (GDD "Scoring", Leaderboards).
    /// </summary>
    /// <remarks>
    /// **The game never sees a password.** Signing in opens Unity's own page; the game waits for a token and
    /// exchanges it for a Unity Gaming Services session. Changing a password, recovering a forgotten one and
    /// closing the account all happen in Unity's portal, reached through <see cref="OpenAccountPortal"/>.
    /// That is the whole point of this over the username and password it replaced: credentials the game
    /// cannot see are credentials the game cannot leak, and a password reset is somebody else's problem.
    ///
    /// **The account is not the pilot name.** Unity's token carries an email address and no display name, so
    /// what the boards show stays a handle the player chooses, kept with the settings. An email address must
    /// never reach a public leaderboard.
    ///
    /// Nothing here may interrupt a game: every call is wrapped, every failure becomes an
    /// <see cref="AccountResult"/> the screen can read out, and a player who never signs in still gets the
    /// whole game.
    ///
    /// The service's own exception messages are written for developers. Only the error code is logged, which
    /// is what identifies the fault; <c>Player.log</c> is the file somebody attaches to a bug report.
    /// </remarks>
    public sealed class UgsAccounts : IPlayerAccounts
    {
        /// <summary>
        /// How long to wait for the service. A captive portal or a black-holed connection does not refuse,
        /// it simply never answers, and the screen must not wait for ever.
        /// </summary>
        public const float TimeoutSeconds = 15f;

        /// <summary>
        /// How long to wait for the player to finish on Unity's page. Generous, because they may have to
        /// find a password, confirm an email or switch application to do it.
        /// </summary>
        public const float SignInTimeoutSeconds = 300f;

        /// <summary>
        /// Remembers that this machine's session came from a Unity account. Player Accounts itself keeps
        /// nothing across a launch (verified in the package: no cache, no PlayerPrefs, and the state starts
        /// SignedOut), so on a cold start the only session that can be resumed is the Gaming Services one.
        /// That session is the right player, but nothing in it says whether an account is behind it, and
        /// without this flag a purely anonymous session looks exactly like a signed-in one.
        /// </summary>
        const string LinkedKey = "account.linked";

        readonly List<Action<bool>> _resuming = new List<Action<bool>>();
        readonly ISettingsStore _store;

        bool _busy;

        /// <summary>
        /// Bumped by anything that changes who is signed in. An attempt that finishes after the player has
        /// moved on must not write its answer over the newer truth: signing out while a resume was still in
        /// flight used to sign them straight back in a moment later.
        /// </summary>
        int _generation;

        /// <summary>
        /// The last name the service gave us. The package does cache the name into PlayerName as well, but
        /// the whole screen hangs off this value, so it is worth holding rather than assuming.
        /// </summary>
        string _knownName = string.Empty;

        public UgsAccounts(ISettingsStore store = null) => _store = store;

        public bool IsSignedIn { get; private set; }

        public string AccountLabel { get; private set; } = string.Empty;

        /// <summary>
        /// WebGL cannot open Unity's page in a way that comes back to the game, so the option is not
        /// offered there rather than offered and then failing. Everywhere else the flow is a system browser
        /// on desktop and a deep link back on mobile.
        /// </summary>
        public bool CanSignIn =>
#if UNITY_WEBGL && !UNITY_EDITOR
            false;
#else
            true;
#endif

        /// <summary>
        /// Only while Player Accounts itself has a session. The portal is reached with that token, and it is
        /// not kept across a launch, so a returning player is signed in to the boards without being able to
        /// manage the account from here. Offering the button anyway pointed at an account that, as far as
        /// this run of the game is concerned, does not exist.
        /// </summary>
        public bool CanManageAccount
        {
            get
            {
                if (!CanSignIn || !IsSignedIn) return false;

                try
                {
                    return UnityServices.State == ServicesInitializationState.Initialized &&
                           PlayerAccountService.Instance.IsSignedIn;
                }
                catch (Exception problem)
                {
                    Log("check the account session", problem);
                    return false;
                }
            }
        }

        /// <summary>
        /// The name the service holds, without the number it appends to keep names unique. Read from the
        /// service's own cache, which survives a restart, so this is right without a round trip.
        /// </summary>
        public string PilotName
        {
            get
            {
                if (!IsSignedIn) return string.Empty;

                try
                {
                    if (UnityServices.State != ServicesInitializationState.Initialized) return _knownName;

                    var live = WithoutTheNumber(AuthenticationService.Instance.PlayerName);
                    return string.IsNullOrEmpty(live) ? _knownName : live;
                }
                catch (Exception problem)
                {
                    Log("read the pilot name", problem);
                    return _knownName;
                }
            }
        }

        public async void FetchPilotName(Action<string> done)
        {
            if (!IsSignedIn)
            {
                Answer(done, string.Empty);
                return;
            }

            try
            {
                // Auto-generating, which is the default and the right thing: a player who has never chosen
                // a name still has one on the service, and that generated name is what every board prints.
                // Asking with autoGenerate off was a mistake: it returned nothing, so the game had no name
                // to show and displayed something of its own instead, guaranteeing that the screen and the
                // boards disagreed. Whatever the service says is the answer, invented by it or not.
                var name = await WithDeadline(
                    AuthenticationService.Instance.GetPlayerNameAsync(), TimeoutSeconds);

                _knownName = WithoutTheNumber(name);
                Answer(done, _knownName);
            }
            catch (Exception problem)
            {
                Log("fetch the pilot name", problem);
                Answer(done, PilotName);
            }
        }

        public async void SetPilotName(string name, Action<AccountResult> done)
        {
            if (!IsSignedIn)
            {
                Answer(done, new AccountResult(AccountStatus.Refused,
                    "Sign in first: the name on the boards belongs to your account."));
                return;
            }

            var wanted = Leaderboards.CleanName(name);

            try
            {
                await WithDeadline(AuthenticationService.Instance.UpdatePlayerNameAsync(wanted), TimeoutSeconds);

                _knownName = wanted;
                Answer(done, AccountResult.Ok);
            }
            catch (Exception problem)
            {
                Log("set the pilot name", problem);

                // The service rate-limits renames and has its own rules, so a refusal here is ordinary and
                // must be said rather than swallowed: the player has just watched their name not change.
                Answer(done, new AccountResult(AccountStatus.Refused,
                    "The service would not take that name. Try another, or try again shortly."));
            }
        }

        /// <summary>Drops the "#1234" the service appends to keep player names unique.</summary>
        static string WithoutTheNumber(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var hash = name.IndexOf('#');
            return hash > 0 ? name.Substring(0, hash) : name;
        }

        public void Resume(Action<bool> signedIn)
        {
            if (IsSignedIn)
            {
                Answer(signedIn, true);
                return;
            }

            if (signedIn != null) _resuming.Add(signedIn);
            if (_busy) return;

            ResumeNow();
        }

        /// <summary>
        /// Signs in again from whatever this machine remembers, without opening anything. Callers that
        /// arrive while it is in flight are queued and answered together: the title starts one as it opens
        /// and the board screen asks moments later, and answering the second "not signed in" on the spot
        /// would be wrong on a perfectly good connection.
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
                        // Somebody has already established the session: the live leaderboard test signs in
                        // as its own user, and a second sign-in on top would be refused. Nothing to do.
                        ok = true;
                    }
                    else if (PlayerAccountService.Instance.IsSignedIn &&
                             !string.IsNullOrEmpty(PlayerAccountService.Instance.AccessToken))
                    {
                        await WithDeadline(AuthenticationService.Instance.SignInWithUnityAsync(
                            PlayerAccountService.Instance.AccessToken), TimeoutSeconds);
                        ok = AuthenticationService.Instance.IsSignedIn;
                    }
                    else if (WasLinkedToAnAccount && AuthenticationService.Instance.SessionTokenExists)
                    {
                        // Only for a machine that has signed in with an account before. Player Accounts
                        // keeps no token across a launch, so this is the only branch a returning player can
                        // take; without the flag it would also catch somebody who never had an account and
                        // present their device-local identity as a signed-in one.
                        await WithDeadline(AuthenticationService.Instance.SignInAnonymouslyAsync(),
                            TimeoutSeconds);
                        ok = AuthenticationService.Instance.IsSignedIn;
                    }
                }
            }
            catch (Exception problem)
            {
                Log("resume", problem);
                ok = false;
            }

            _busy = false;

            // Only if nothing newer has happened. Signing out while this was in flight used to be undone a
            // moment later by this line, putting the player back online without them touching anything.
            if (mine == _generation) Remember(ok);

            Drain(mine == _generation && ok);
        }

        /// <summary>
        /// Answers everyone queued on a resume. Called from every path that clears <c>_busy</c>, not just
        /// the resume: a caller queued while a sign-in held the flag was never answered at all, and the
        /// leaderboard screen it came from then waited for an answer that could not arrive.
        /// </summary>
        void Drain(bool signedIn)
        {
            if (_resuming.Count == 0) return;

            var waiting = _resuming.ToArray();
            _resuming.Clear();
            foreach (var caller in waiting) Answer(caller, signedIn);
        }

        /// <summary>Whether this machine has ever completed a Unity account sign-in.</summary>
        bool WasLinkedToAnAccount => _store != null && _store.GetBool(LinkedKey, false);

        public async void SignIn(Action<AccountResult> done)
        {
            if (!CanSignIn)
            {
                Answer(done, new AccountResult(AccountStatus.Unsupported));
                return;
            }

            if (_busy)
            {
                // The screen already refuses this, but a second page opened on top of the first is worth
                // refusing twice: the two would race and the loser would leave a half-made session behind.
                Answer(done, new AccountResult(AccountStatus.Working));
                return;
            }

            _busy = true;
            var mine = ++_generation;
            var answer = new AccountResult(AccountStatus.Cancelled);

            try
            {
                if (!await Started())
                {
                    answer = new AccountResult(AccountStatus.Unreachable,
                        "Could not reach the service. Check your connection and try again.");
                    return;
                }

                var attempt = await OpenTheSignInPage();
                if (mine != _generation) return; // the player has moved on; do not write over what they did

                if (attempt.Succeeded) Remember(true);
                answer = attempt;
            }
            catch (Exception problem)
            {
                Log("sign in", problem);
                answer = Classify(problem);
            }
            finally
            {
                _busy = false;
                Drain(IsSignedIn);

                // Outside the try on purpose. The callback redraws a screen and can navigate, and when it
                // threw in here the catch answered a second time with a failure, so the player both moved
                // on and was told it had not worked.
                Answer(done, answer);
            }
        }

        /// <summary>
        /// What to tell the player about a failure. Not everything is the connection: a build with no client
        /// id configured, or a session the package thinks is still open, are faults of ours, and blaming the
        /// player's network for them sends them off to restart a router that was never the problem.
        /// </summary>
        static AccountResult Classify(Exception problem)
        {
            if (problem is PlayerAccountsException accounts)
            {
                if (accounts.ErrorCode == PlayerAccountsErrorCodes.MissingClientId)
                    return new AccountResult(AccountStatus.Unsupported,
                        "Signing in is not set up in this version.");

                if (accounts.ErrorCode == PlayerAccountsErrorCodes.InvalidState)
                    return new AccountResult(AccountStatus.Refused,
                        "Signing in is already under way. Finish in the page that opened, or try again.");
            }

            return new AccountResult(AccountStatus.Unreachable,
                "Could not reach the service. Check your connection and try again.");
        }

        /// <summary>
        /// Opens Unity's page and waits for one of its three answers. The package reports the outcome
        /// through events rather than by completing the task it hands back, so the events are what is
        /// waited on; the task only starts the flow.
        /// </summary>
        async Task<AccountResult> OpenTheSignInPage()
        {
            // A previous attempt that timed out leaves the package Authorized with nobody listening, and
            // StartSignInAsync then throws InvalidState for the rest of the session. Clearing it first is
            // what makes a second attempt possible at all after the player took too long the first time.
            if (PlayerAccountService.Instance.IsSignedIn && !IsSignedIn)
            {
                try { PlayerAccountService.Instance.SignOut(); }
                catch (Exception problem) { Log("clear the stranded sign-in", problem); }
            }

            var finished = new TaskCompletionSource<AccountResult>();

            void OnSignedIn() => finished.TrySetResult(AccountResult.Ok);
            void OnFailed(RequestFailedException problem)
            {
                Log("sign in", problem);
                finished.TrySetResult(new AccountResult(AccountStatus.Refused,
                    "Signing in did not work. Please try again."));
            }

            PlayerAccountService.Instance.SignedIn += OnSignedIn;
            PlayerAccountService.Instance.SignInFailed += OnFailed;

            try
            {
                // The deadline covers the whole attempt, including starting it, because starting it is not
                // quick everywhere. On desktop and in the editor StartSignInAsync awaits an HttpListener
                // waiting for the browser to come back, so it does not return until the player has finished;
                // a deadline placed after it could never fire, and a closed tab left this stuck for the rest
                // of the session. On Android it returns at once and the answer arrives by deep link later.
                // One budget, raced against both, behaves the same either way.
                var start = PlayerAccountService.Instance.StartSignInAsync();
                var deadline = Task.Delay(TimeSpan.FromSeconds(SignInTimeoutSeconds));

                var first = await Task.WhenAny(finished.Task, start, deadline);
                if (first == deadline)
                {
                    Observe(start);
                    return new AccountResult(AccountStatus.Cancelled);
                }

                // Awaited for its exceptions: InvalidState and MissingClientId arrive this way, and they
                // are faults worth telling apart from a connection that is simply down.
                if (first == start) await start;

                if (!finished.Task.IsCompleted &&
                    await Task.WhenAny(finished.Task, deadline) != finished.Task)
                {
                    Observe(start);
                    return new AccountResult(AccountStatus.Cancelled);
                }

                var outcome = await finished.Task;
                if (!outcome.Succeeded) return outcome;

                // Unity says who the player is; the boards still need a Gaming Services session, and that
                // is what the token buys. Until this lands the player is signed in to nothing useful.
                await WithDeadline(AuthenticationService.Instance.SignInWithUnityAsync(
                    PlayerAccountService.Instance.AccessToken), TimeoutSeconds);

                return AuthenticationService.Instance.IsSignedIn
                    ? AccountResult.Ok
                    : new AccountResult(AccountStatus.Refused, "Signing in did not work. Please try again.");
            }
            finally
            {
                PlayerAccountService.Instance.SignedIn -= OnSignedIn;
                PlayerAccountService.Instance.SignInFailed -= OnFailed;
            }
        }

        public void SignOut(Action<AccountResult> done)
        {
            // Before anything else: this is the newest word on who is signed in, and a resume still in
            // flight must not be allowed to answer after it and put the player back online.
            _generation++;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized)
                {
                    // Both halves, and the credentials with them. Leaving the session token behind would
                    // sign the same person straight back in on the next start, which is not what somebody
                    // handing the machine to a friend meant by signing out. Unconditional, because a
                    // half-finished sign-in can leave a token behind while this thinks nobody is signed in.
                    AuthenticationService.Instance.SignOut(clearCredentials: true);

                    if (PlayerAccountService.Instance.IsSignedIn)
                        PlayerAccountService.Instance.SignOut();
                }
            }
            catch (Exception problem)
            {
                Log("sign out", problem);
            }
            finally
            {
                // Signed out locally whatever the service said. A sign-out that reports failure and leaves
                // the player apparently signed in is worse than one that forgets slightly too eagerly.
                Remember(false);
                _busy = false;
                Drain(false);
                Answer(done, AccountResult.Ok);
            }
        }

        public void SwitchAccount(Action<AccountResult> done) =>
            SignOut(_ => SignIn(done));

        public void OpenAccountPortal()
        {
            try
            {
                var portal = PlayerAccountService.Instance.AccountPortalUrl;
                if (!string.IsNullOrEmpty(portal)) Application.OpenURL(portal);
                else Debug.LogWarning($"{nameof(UgsAccounts)}: no account portal url to open.");
            }
            catch (Exception problem)
            {
                Log("open the account portal", problem);
            }
        }

        void Remember(bool signedIn)
        {
            IsSignedIn = signedIn;
            AccountLabel = signedIn ? LabelForTheSignedInAccount() : string.Empty;

            if (!signedIn) _knownName = string.Empty;
            else if (string.IsNullOrEmpty(_knownName)) FetchPilotName(null); // warm it; the answer lands later

            // Written only when an account was actually involved, and cleared on the way out. This is what
            // lets the next launch tell a resumable account session from a device-local one.
            if (_store == null) return;

            if (signedIn)
            {
                if (IsAccountSession()) _store.SetBool(LinkedKey, true);
            }
            else
            {
                _store.SetBool(LinkedKey, false);
            }

            _store.Save();
        }

        static bool IsAccountSession()
        {
            try
            {
                return UnityServices.State == ServicesInitializationState.Initialized &&
                       PlayerAccountService.Instance.IsSignedIn;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Something the player recognises about their own account: the email address, when the token
        /// carries one. Empty otherwise, and deliberately so. The player id was shown here for a while,
        /// which reads as "Signed in as player G4Vq9jP6BtGasjTWE1NAc8KWKMFj" and tells nobody anything;
        /// what the screen says with no label is that they are signed in with their Unity account, which is
        /// both true and useful. The id is not an identity the player has ever seen.
        /// </summary>
        static string LabelForTheSignedInAccount()
        {
            try
            {
                var claims = PlayerAccountService.Instance.IdTokenClaims;
                return claims != null && !string.IsNullOrWhiteSpace(claims.Email) ? claims.Email : string.Empty;
            }
            catch (Exception problem)
            {
                Log("read the account details", problem);
                return string.Empty;
            }
        }

        static Task<bool> Started() => UgsSession.Start(work => WithDeadline(work, TimeoutSeconds));

        static void Answer(Action<bool> caller, bool value) => caller?.Invoke(value);

        static void Answer(Action<string> caller, string value) => caller?.Invoke(value);

        static void Answer(Action<AccountResult> caller, AccountResult result) => caller?.Invoke(result);

        /// <summary>
        /// Waits for the service, but not for ever. The timer is cancelled when the work wins, so a call
        /// does not leave a timer behind it, and work that is abandoned is still observed, so a failure
        /// arriving late does not surface as an unobserved task exception.
        /// </summary>
        /// <summary>As below, for work that answers with something.</summary>
        static async Task<T> WithDeadline<T>(Task<T> work, float seconds)
        {
            await WithDeadline((Task)work, seconds);
            return work.Result;
        }

        static async Task WithDeadline(Task work, float seconds)
        {
            using (var timer = new CancellationTokenSource())
            {
                var waiting = Task.Delay(TimeSpan.FromSeconds(seconds), timer.Token);
                if (await Task.WhenAny(work, waiting) != work)
                {
                    Observe(work);
                    throw new TimeoutException("the service did not answer");
                }

                timer.Cancel();
                await work;
            }
        }

        static void Observe(Task work) =>
            work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

        /// <summary>
        /// The code, never the message. The service writes its messages for developers and they can carry
        /// details of the request; the code is what identifies the fault in a log somebody may send on.
        /// </summary>
        static void Log(string what, Exception problem)
        {
            var code = problem is RequestFailedException failed ? failed.ErrorCode.ToString() : problem.GetType().Name;
            Debug.LogWarning($"{nameof(UgsAccounts)}: could not {what} (code {code}).");
        }
    }
}
