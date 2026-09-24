using System;

namespace YASS.Core
{
    /// <summary>How an attempt to sign up or sign in ended.</summary>
    public enum AccountStatus
    {
        /// <summary>The attempt has not finished.</summary>
        Working = 0,

        Succeeded,

        /// <summary>Somebody already has that pilot name. Names are the account, so they are unique.</summary>
        NameTaken,

        /// <summary>No account with that name and password. Deliberately not "which" of the two was wrong.</summary>
        WrongDetails,

        /// <summary>The service refused for some other reason, such as a password it considers too weak.</summary>
        Refused,

        /// <summary>No network, or the service is down. Not the player's fault and not a fault in the game.</summary>
        Unreachable
    }

    /// <summary>The result of an attempt on an account, with something the player can be shown.</summary>
    public readonly struct AccountResult
    {
        public readonly AccountStatus Status;

        /// <summary>What to put on the screen. Written for the player, not for the log.</summary>
        public readonly string Problem;

        public AccountResult(AccountStatus status, string problem = null)
        {
            Status = status;
            Problem = problem ?? string.Empty;
        }

        public bool Succeeded => Status == AccountStatus.Succeeded;

        public static readonly AccountResult Ok = new AccountResult(AccountStatus.Succeeded);
    }

    /// <summary>
    /// The player's account, which is what makes their scores theirs across devices (GDD "Scoring",
    /// Leaderboards). The pilot name is the account: one name to remember, and the boards show it.
    /// </summary>
    /// <remarks>
    /// Callback-based and never throwing, for the same reason as <see cref="ILeaderboardService"/>: every
    /// caller is a MonoBehaviour that may be gone before the answer arrives, and no failure here may stop
    /// somebody playing. A player with no account, or no network, still gets the whole game; they simply
    /// have no scores on the online boards.
    /// </remarks>
    public interface IPlayerAccounts
    {
        bool IsSignedIn { get; }

        /// <summary>The pilot name of whoever is signed in, or empty.</summary>
        string SignedInAs { get; }

        /// <summary>
        /// Signs in again from whatever this machine remembers, so a returning player is never asked twice.
        /// Answers false when there is nothing to resume, which is when the player has to be asked.
        /// </summary>
        void Resume(Action<bool> signedIn);

        void SignUp(string name, string password, Action<AccountResult> done);

        void SignIn(string name, string password, Action<AccountResult> done);

        /// <summary>Forgets this machine's session, so the next start asks again.</summary>
        void SignOut();
    }
}
