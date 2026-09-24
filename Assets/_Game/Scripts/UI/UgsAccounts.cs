using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Player accounts through Unity Authentication (GDD "Scoring", Leaderboards). The pilot name is the
    /// username, so a player has one name to remember and the boards show it.
    /// </summary>
    /// <remarks>
    /// Nothing here may interrupt a game: every call is wrapped, every failure becomes an
    /// <see cref="AccountResult"/> the screen can read out, and a player who never signs up still gets the
    /// whole game.
    ///
    /// The service's own exception messages are written for developers and, on these paths, describe a
    /// request that carried a password. They are not logged: the error code is, which is what identifies the
    /// fault. <c>Player.log</c> is the file somebody attaches to a bug report.
    ///
    /// Unity Authentication keeps a session token on the machine once somebody has signed in, which is what
    /// <see cref="Resume"/> uses: a returning player is not asked again, on this device.
    /// </remarks>
    public sealed class UgsAccounts : IPlayerAccounts
    {
        /// <summary>
        /// How long to wait before giving up on the service. A captive portal or a black-holed connection
        /// does not refuse, it simply never answers, and the screen must not wait for ever.
        /// </summary>
        public const float TimeoutSeconds = 15f;

        /// <summary>Everyone waiting on the resume in flight, answered together when it lands.</summary>
        readonly List<Action<bool>> _resuming = new List<Action<bool>>();

        bool _busy;

        public bool IsSignedIn { get; private set; }

        public string SignedInAs { get; private set; } = string.Empty;

        /// <summary>
        /// Signs in again from whatever this machine remembers. Callers that arrive while that is in flight
        /// are queued and answered with it: the title starts one as it opens and the board screen asks for
        /// another moments later, and the service refuses a second sign-in while one is running, which would
        /// otherwise be reported as "not signed in" on a perfectly good connection.
        /// </summary>
        public async void Resume(Action<bool> signedIn)
        {
            if (IsSignedIn)
            {
                Answer(signedIn, true);
                return;
            }

            if (signedIn != null) _resuming.Add(signedIn);
            if (_busy) return;

            _busy = true;
            try
            {
                if (await Started())
                {
                    // Somebody already signed in, through this or anything else sharing the service: adopt
                    // that rather than signing in over the top, which the service refuses. Otherwise only
                    // when this machine remembers somebody, because signing in is the player's to start and
                    // a silent anonymous sign-in is exactly what this replaced. The session token restores
                    // whatever account it belongs to, however that account was made.
                    if (!AuthenticationService.Instance.IsSignedIn &&
                        AuthenticationService.Instance.SessionTokenExists)
                        await WithDeadline(AuthenticationService.Instance.SignInAnonymouslyAsync());
                }
            }
            catch (Exception problem)
            {
                Warn("could not resume a session", problem);
            }
            finally
            {
                _busy = false;

                // Believe the service rather than this object: a call that ran out of time locally may still
                // have landed, and thinking we are signed out while the service thinks otherwise makes every
                // later attempt fail with a client-state error nobody can act on.
                Remember();
                AnswerEveryoneResuming();
            }
        }

        public void SignUp(string name, string password, Action<AccountResult> done) =>
            Attempt(name, password, done, cleaned =>
                AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(cleaned, password));

        public void SignIn(string name, string password, Action<AccountResult> done) =>
            Attempt(name, password, done, cleaned =>
                AuthenticationService.Instance.SignInWithUsernamePasswordAsync(cleaned, password));

        public void SignOut()
        {
            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut(clearCredentials: true);
            }
            catch (Exception problem)
            {
                Warn("could not sign out", problem);
            }

            IsSignedIn = false;
            SignedInAs = string.Empty;
        }

        /// <summary>
        /// One attempt on the service, with exactly one answer. The result is worked out inside the try and
        /// handed over outside it: an exception thrown by the caller's own callback must not be caught here,
        /// reported as a failure of the service, and answered a second time.
        /// </summary>
        async void Attempt(string name, string password, Action<AccountResult> done, Func<string, Task> call)
        {
            // Trimmed once, here, and that one value is what is checked, what the service is given and what
            // is remembered. Checking a trimmed name and sending the raw one had the service refuse a name
            // that looked perfectly legal to the player, a round trip later.
            var cleaned = (name ?? string.Empty).Trim();

            var result = Check(cleaned, password);
            if (result.Succeeded)
            {
                if (_busy)
                {
                    Answer(done, new AccountResult(AccountStatus.Refused, "Still working on the last attempt."));
                    return;
                }

                _busy = true;
                try
                {
                    if (!await Started())
                    {
                        result = new AccountResult(AccountStatus.Unreachable,
                            "Could not reach the service. Check your connection and try again.");
                    }
                    else
                    {
                        await WithDeadline(call(cleaned));
                        result = AccountResult.Ok;
                    }
                }
                catch (Exception problem)
                {
                    Warn("could not sign in or sign up", problem);
                    result = Explain(problem);
                }
                finally
                {
                    _busy = false;
                    Remember();

                    // The name the account was made with, not whatever the box says now: the player can go
                    // on typing while the round trip is in flight.
                    if (IsSignedIn) SignedInAs = cleaned;
                }

                // A timeout that the service went on to honour: the account is there, so say so rather than
                // sending the player round again to be told they are already signed in.
                if (!result.Succeeded && IsSignedIn) result = AccountResult.Ok;
                if (result.Succeeded && !IsSignedIn)
                    result = new AccountResult(AccountStatus.Unreachable, "Could not sign in. Try again.");
            }

            Answer(done, result);
        }

        /// <summary>The rules that need no network, so a mistyped name is answered without a round trip.</summary>
        static AccountResult Check(string name, string password)
        {
            var checkedName = Credentials.CheckName(name);
            if (!checkedName.IsUsable) return new AccountResult(AccountStatus.Refused, checkedName.Problem);

            var checkedPassword = Credentials.CheckPassword(password);
            return checkedPassword.IsUsable
                ? AccountResult.Ok
                : new AccountResult(AccountStatus.Refused, checkedPassword.Problem);
        }

        /// <summary>
        /// Turns the service's exception into something worth putting on a screen. Wrong details are looked
        /// for first: Unity maps a failed username-and-password sign-in to the same code as a malformed
        /// request, so testing that code first told a player who had mistyped their password that their name
        /// was not allowed, and sent them off to fix the wrong thing.
        /// </summary>
        static AccountResult Explain(Exception problem)
        {
            var text = problem.Message ?? string.Empty;

            if (Mentions(text, "WRONG_USERNAME_PASSWORD") || Mentions(text, "not found") ||
                Mentions(text, "incorrect"))
                return new AccountResult(AccountStatus.WrongDetails, "No pilot by that name with that password.");

            if (Mentions(text, "already exists") || Mentions(text, "ALREADY_EXISTS") || Mentions(text, "taken"))
                return new AccountResult(AccountStatus.NameTaken, "That pilot name is taken. Try another.");

            // if rather than switch: the service's codes are static readonly ints, not constants.
            if (problem is AuthenticationException failure)
            {
                if (failure.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                    return new AccountResult(AccountStatus.NameTaken, "That pilot name is taken. Try another.");

                if (failure.ErrorCode == AuthenticationErrorCodes.BannedUser)
                    return new AccountResult(AccountStatus.Refused, "That account has been banned.");

                if (failure.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
                    return new AccountResult(AccountStatus.WrongDetails,
                        "No pilot by that name with that password.");
            }

            return new AccountResult(AccountStatus.Unreachable,
                "Could not reach the service. Check your connection and try again.");
        }

        static bool Mentions(string text, string what) =>
            text.IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Starts Unity Services if they are not already running. False when they cannot be.</summary>
        static async Task<bool> Started()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return true;

            await WithDeadline(UnityServices.InitializeAsync());
            return UnityServices.State == ServicesInitializationState.Initialized;
        }

        /// <summary>
        /// Waits for the service, but not for ever. The timer is cancelled when the work wins, so a call does
        /// not leave a quarter-minute timer behind it, and work that is abandoned is still observed, so a
        /// failure arriving late does not surface as an unobserved task exception.
        /// </summary>
        static async Task WithDeadline(Task work)
        {
            using (var timer = new CancellationTokenSource())
            {
                var waiting = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds), timer.Token);
                if (await Task.WhenAny(work, waiting) != work)
                {
                    Observe(work);
                    throw new TimeoutException("the service did not answer");
                }

                timer.Cancel();
            }

            await work; // so a failure is thrown here rather than swallowed
        }

        /// <summary>Keeps an abandoned task's failure from surfacing later as an unobserved exception.</summary>
        static void Observe(Task work) =>
            work.ContinueWith(finished => { _ = finished.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

        void Remember()
        {
            IsSignedIn = AuthenticationService.Instance.IsSignedIn;
            if (!IsSignedIn)
            {
                SignedInAs = string.Empty;
                return;
            }

            // The username is the pilot name; the service's PlayerName is the display name, which is empty
            // until a score has been filed and is not necessarily the same string.
            var username = AuthenticationService.Instance.PlayerInfo?.Username;
            if (!string.IsNullOrEmpty(username)) SignedInAs = username;
        }

        void AnswerEveryoneResuming()
        {
            if (_resuming.Count == 0) return;

            // Copied out first: answering one of them can start another request that arrives back here.
            var waiting = _resuming.ToArray();
            _resuming.Clear();

            foreach (var callback in waiting) Answer(callback, IsSignedIn);
        }

        /// <summary>
        /// Hands the answer over. A callback that throws is logged here rather than being allowed to reach
        /// this class's own error handling, where it would be reported to somebody as a failure of the
        /// service and answered twice.
        /// </summary>
        static void Answer<T>(Action<T> callback, T answer)
        {
            if (callback == null) return;

            try
            {
                callback(answer);
            }
            catch (Exception problem)
            {
                Debug.LogException(problem);
            }
        }

        static void Warn(string what, Exception problem)
        {
            var code = problem is AuthenticationException failure
                ? failure.ErrorCode.ToString()
                : problem.GetType().Name;

            Debug.LogWarning($"{nameof(UgsAccounts)}: {what} ({code}).");
        }
    }
}
