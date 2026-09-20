using System;

namespace YASS.Core
{
    /// <summary>
    /// What one player carries from a cleared level into the next one: their ship's condition and what they have
    /// scored so far (GDD "Core Loop": a campaign is one run through eight levels, not eight separate games).
    /// </summary>
    public readonly struct PlayerCarry
    {
        public readonly int Lives;
        public readonly float Health;
        public readonly int WeaponLevel;

        public PlayerCarry(int lives, float health, int weaponLevel)
        {
            if (lives < 0) throw new ArgumentOutOfRangeException(nameof(lives));
            if (health < 0f) throw new ArgumentOutOfRangeException(nameof(health));
            if (weaponLevel < 1) throw new ArgumentOutOfRangeException(nameof(weaponLevel));

            Lives = lives;
            Health = health;
            WeaponLevel = weaponLevel;
        }

        /// <summary>The state a player ends a level in.</summary>
        public static PlayerCarry From(PlayerShip ship)
        {
            if (ship == null) throw new ArgumentNullException(nameof(ship));

            return new PlayerCarry(ship.Vitals.Lives, ship.Vitals.Health, ship.Weapon.Level);
        }
    }

    /// <summary>
    /// A campaign run in progress: which level it is on, what has been scored, and the state each player carries
    /// forward. The run ends when the last level is cleared (victory) or every player is out of lives.
    /// </summary>
    public sealed class CampaignRun
    {
        readonly PlayerCarry[] _players;

        public CampaignRun(Difficulty difficulty, int levelCount, int playerCount = 1)
        {
            if (levelCount < 1) throw new ArgumentOutOfRangeException(nameof(levelCount));
            if (playerCount < 1) throw new ArgumentOutOfRangeException(nameof(playerCount));

            Difficulty = difficulty;
            LevelCount = levelCount;
            _players = new PlayerCarry[playerCount];
        }

        public Difficulty Difficulty { get; }

        /// <summary>How many levels this campaign has; the run is won after clearing the last.</summary>
        public int LevelCount { get; }

        /// <summary>The level being played, counting from zero.</summary>
        public int LevelIndex { get; private set; }

        /// <summary>The level being played, as the player sees it.</summary>
        public int LevelNumber => LevelIndex + 1;

        /// <summary>Score and kills carried from the levels already cleared, before this level's own.</summary>
        public long CarriedScore { get; private set; }
        public int CarriedKills { get; private set; }

        /// <summary>The best chain of the whole run, not only of the current level.</summary>
        public int BestChainSteps { get; private set; }

        /// <summary>True once the last level has been cleared.</summary>
        public bool IsComplete => LevelIndex >= LevelCount;

        /// <summary>True when the level just cleared was the last one.</summary>
        public bool IsFinalLevel => LevelIndex == LevelCount - 1;

        /// <summary>What a player carries into the current level, or null on the first one.</summary>
        public PlayerCarry? CarryFor(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));

            return LevelIndex == 0 ? (PlayerCarry?)null : _players[playerIndex];
        }

        /// <summary>
        /// Records a cleared level and moves to the next. The session's totals are absorbed here, so the next
        /// level starts its own scoring from zero while the run's total keeps climbing.
        /// </summary>
        public void CompleteLevel(GameSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (IsComplete) throw new InvalidOperationException("The campaign is already finished.");

            CarriedScore += session.Score.Score;
            CarriedKills += session.Score.Kills;
            BestChainSteps = Math.Max(BestChainSteps, session.Score.BestChainSteps);

            for (var i = 0; i < _players.Length && i < session.PlayerCount; i++)
                _players[i] = PlayerCarry.From(session.GetPlayer(i));

            LevelIndex++;
        }

        /// <summary>
        /// Test seam: jumps the run to its last level, so a test can reach the campaign's ending without playing
        /// all eight of them. It does not invent the levels it skipped, so what each player carries is still
        /// whatever the run has banked: call it while the level in play has already started.
        /// </summary>
        internal void SkipToFinalLevel() => LevelIndex = LevelCount - 1;

        /// <summary>The run's totals including the level being played now.</summary>
        public long TotalScore(GameSession session) =>
            CarriedScore + (session?.Score.Score ?? 0);

        public int TotalKills(GameSession session) =>
            CarriedKills + (session?.Score.Kills ?? 0);

        public int BestChain(GameSession session) =>
            Math.Max(BestChainSteps, session?.Score.BestChainSteps ?? 0);
    }
}
