using System;

namespace YASS.Core
{
    /// <summary>How an attempt on the player's account ended.</summary>
    public enum AccountStatus
    {
        /// <summary>The attempt has not finished.</summary>
        Working = 0,

        Succeeded,

        /// <summary>The player closed the sign-in page, or came back without finishing. Not an error.</summary>
        Cancelled,

        /// <summary>The service declined. Rare, and nothing the player can fix by trying harder.</summary>
        Refused,

        /// <summary>No network, or the service is down. Not the player's fault and not a fault in the game.</summary>
        Unreachable,

        /// <summary>This build cannot open the sign-in page at all, so the option should not be offered.</summary>
        Unsupported
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
    /// The player's Unity account, which is what makes their scores theirs across devices (GDD "Scoring",
    /// Leaderboards).
    /// </summary>
    /// <remarks>
    /// **The game never sees a password.** Signing in hands the player to Unity's own page and waits for the
    /// answer; changing a password, recovering a forgotten one and deleting the account all happen there too,
    /// through <see cref="OpenAccountPortal"/>. Nothing on this interface takes a credential, and that is the
    /// point of it: credentials the game cannot see are credentials the game cannot leak.
    ///
    /// **The account is not the pilot name.** The account is identified by an email address, which must never
    /// appear on a public leaderboard; the pilot name is a separate handle, and once somebody is signed in it
    /// belongs to the service rather than to this machine. That matters because the service's name is the one
    /// the boards actually print: a name kept only here would be what the player was shown while their runs
    /// appeared under something else entirely.
    ///
    /// Callback-based and never throwing, for the same reason as <see cref="ILeaderboardService"/>: every
    /// caller is a MonoBehaviour that may be gone before the answer arrives, and no failure here may stop
    /// somebody playing. A player with no account, or no network, still gets the whole game; they simply
    /// have no scores on the online boards.
    /// </remarks>
    public interface IPlayerAccounts
    {
        bool IsSignedIn { get; }

        /// <summary>
        /// Who is signed in, for showing them their own account: an email address, usually. **Never put this
        /// on a leaderboard.** What the boards show is the pilot name, which the player chooses.
        /// </summary>
        string AccountLabel { get; }

        /// <summary>Whether this build can open the sign-in page at all.</summary>
        bool CanSignIn { get; }

        /// <summary>
        /// The pilot name the service holds for whoever is signed in, which is the name the boards print.
        /// Empty when nobody is signed in, or when the service has not said yet. The service keeps names
        /// unique by appending a number; this is the part before it, which is what the player typed.
        /// </summary>
        string PilotName { get; }

        /// <summary>
        /// Asks the service for the pilot name, and answers with it. The service assigns one to a player
        /// who has never chosen a name, and that assigned name is what the boards print, so this always
        /// answers with something for a signed-in player. Cheap after the first call: the service caches
        /// the name, in this session and between them.
        /// </summary>
        void FetchPilotName(Action<string> done);

        /// <summary>
        /// Sets the pilot name on the service, which is what changes the name on the boards. The service
        /// may refuse: it rate-limits renames, and it has its own rules about what a name may contain.
        /// </summary>
        void SetPilotName(string name, Action<AccountResult> done);

        /// <summary>Whether Unity's account portal can be opened from here.</summary>
        bool CanManageAccount { get; }

        /// <summary>
        /// Signs in again from whatever this machine remembers, so a returning player is never asked twice.
        /// Answers false when there is nothing to resume, which is when the player has to be asked.
        /// </summary>
        void Resume(Action<bool> signedIn);

        /// <summary>
        /// Hands the player to Unity's sign-in page and answers when they come back. The same page both
        /// signs in and signs up, so the game does not need to ask which they meant.
        /// </summary>
        void SignIn(Action<AccountResult> done);

        /// <summary>
        /// Signs out and forgets this machine's session, so the next start asks again and a different
        /// person can sign in on a shared machine.
        /// </summary>
        void SignOut(Action<AccountResult> done);

        /// <summary>
        /// Signs out and immediately offers the sign-in page again, which is what "use a different account"
        /// means. Separate from calling the two in turn because a half-finished switch must still leave the
        /// player signed out rather than back where they started.
        /// </summary>
        void SwitchAccount(Action<AccountResult> done);

        /// <summary>
        /// Opens Unity's account portal, where the player changes their password, recovers a forgotten one,
        /// or closes the account. The game has no part in any of it.
        /// </summary>
        void OpenAccountPortal();
    }
}
