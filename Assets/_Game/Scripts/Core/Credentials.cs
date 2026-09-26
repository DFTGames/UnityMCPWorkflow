namespace YASS.Core
{
    /// <summary>Whether a pilot name can be used, and what to tell the player when it cannot.</summary>
    public readonly struct CredentialCheck
    {
        public readonly bool IsUsable;

        /// <summary>What is wrong, in the player's words. Empty when nothing is.</summary>
        public readonly string Problem;

        CredentialCheck(bool usable, string problem)
        {
            IsUsable = usable;
            Problem = problem;
        }

        public static readonly CredentialCheck Fine = new CredentialCheck(true, string.Empty);

        public static CredentialCheck No(string problem) => new CredentialCheck(false, problem);
    }

    /// <summary>
    /// What a pilot name has to look like before the game bothers the service with it (GDD "Scoring": the
    /// boards show the pilot name).
    /// </summary>
    /// <remarks>
    /// Checked here so a player who has mistyped something is told which rule they broke, at once, instead
    /// of waiting for a round trip to come back with a message written for a developer. The service is
    /// still the authority, and this passing is no promise that it will accept.
    ///
    /// **There is nothing here about passwords, on purpose.** The account belongs to Unity, and so does
    /// every rule about what secures it: the game never sees a password, so it has no business having an
    /// opinion on one. Rules copied from a service go stale silently, and a stale rule here would reject a
    /// password Unity would have been perfectly happy with.
    /// </remarks>
    public static class Credentials
    {
        /// <summary>The shortest name worth showing on a board.</summary>
        public const int MinNameLength = 3;

        /// <summary>The service's own limit. Deliberately not the width of a board row: see there.</summary>
        public const int MaxNameLength = Leaderboards.MaxNameLength;

        /// <summary>
        /// The characters a pilot name may contain. Deliberately the service's set rather than a friendlier
        /// one: anything else is rejected on the round trip, and a name silently altered to fit is worse than
        /// a name refused, because it is the one the player has to type again next time.
        ///
        /// '@' is excluded deliberately. The name is a display name on a public board, and an email address
        /// must never end up being one: excluding the character that makes an address an address is the
        /// cheapest way to stop somebody pasting theirs in. A name the boards will not take means their runs
        /// never appear under it, and the only sign of that is a warning in the log.
        /// </summary>
        public static bool IsAllowedInName(char letter) =>
            (letter >= 'a' && letter <= 'z') || (letter >= 'A' && letter <= 'Z') ||
            (letter >= '0' && letter <= '9') || letter == '.' || letter == '-' || letter == '_';

        public static CredentialCheck CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return CredentialCheck.No("Choose a pilot name.");

            var trimmed = name.Trim();
            if (trimmed.Length < MinNameLength)
                return CredentialCheck.No($"A pilot name needs at least {MinNameLength} characters.");

            if (trimmed.Length > MaxNameLength)
                return CredentialCheck.No($"A pilot name can be at most {MaxNameLength} characters.");

            foreach (var letter in trimmed)
                if (!IsAllowedInName(letter))
                    return CredentialCheck.No("A pilot name can use letters, numbers and . - _ only.");

            return CredentialCheck.Fine;
        }

    }
}
