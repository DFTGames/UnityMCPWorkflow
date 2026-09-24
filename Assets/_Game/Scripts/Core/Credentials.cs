namespace YASS.Core
{
    /// <summary>Whether a pilot name or a password can be used, and what to tell the player when it cannot.</summary>
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
    /// What a pilot name and a password have to look like before the game bothers the service with them
    /// (GDD "Scoring", Leaderboards: the pilot name is the account).
    /// </summary>
    /// <remarks>
    /// These are Unity Authentication's own rules, checked here so a player who has mistyped something is told
    /// which rule they broke, at once, instead of waiting for a round trip to come back with a message written
    /// for a developer. The service is still the authority: it decides whether a name is already taken, and
    /// this passing is no promise that it will accept.
    /// </remarks>
    public static class Credentials
    {
        /// <summary>Unity Authentication's limits on a username.</summary>
        public const int MinNameLength = 3;

        /// <summary>Unity Authentication allows 20; the boards are laid out for fewer.</summary>
        public const int MaxNameLength = Leaderboards.MaxNameLength;

        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 30;

        /// <summary>
        /// The characters a pilot name may contain. Deliberately the service's set rather than a friendlier
        /// one: anything else is rejected on the round trip, and a name silently altered to fit is worse than
        /// a name refused, because it is the one the player has to type again next time.
        ///
        /// Narrower than the service's username set by one character: '@' is allowed in a username but this
        /// name is also the display name the boards show, and that has its own, unconfirmed, set. A name
        /// refused here costs the player one character; a name the boards will not take means their runs
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

        /// <summary>
        /// Unity Authentication wants a mixture, and says so only after the round trip. The rule is spelled
        /// out in one sentence rather than four, because a list of failures is a wall to read and the player
        /// only has to satisfy all of it anyway.
        /// </summary>
        public static CredentialCheck CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return CredentialCheck.No("Choose a password.");

            if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
                return CredentialCheck.No(
                    $"A password is {MinPasswordLength} to {MaxPasswordLength} characters.");

            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (var letter in password)
            {
                if (letter >= 'A' && letter <= 'Z') upper = true;
                else if (letter >= 'a' && letter <= 'z') lower = true;
                else if (letter >= '0' && letter <= '9') digit = true;
                else if (!char.IsWhiteSpace(letter)) symbol = true; // a space is not the symbol it asks for
            }

            return upper && lower && digit && symbol
                ? CredentialCheck.Fine
                : CredentialCheck.No("A password needs a capital, a small letter, a digit and a symbol.");
        }
    }
}
