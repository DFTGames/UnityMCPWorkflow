using System;

namespace YASS.Core
{
    public enum Difficulty
    {
        Cadet = 0,
        Pilot = 1,
        Ace = 2
    }

    /// <summary>Per-difficulty tuning from the GDD page "Difficulty and Balancing".</summary>
    public sealed class DifficultySettings
    {
        public static readonly DifficultySettings Cadet =
            new DifficultySettings(Difficulty.Cadet, 5, 100f, 0.75f, 0.85f, 0.75f, 0.7f, 1.25f, 1.0f);

        public static readonly DifficultySettings Pilot =
            new DifficultySettings(Difficulty.Pilot, 3, 100f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.5f);

        public static readonly DifficultySettings Ace =
            new DifficultySettings(Difficulty.Ace, 2, 100f, 1.5f, 1.2f, 1.3f, 1.3f, 0.75f, 2.5f);

        public Difficulty Difficulty { get; }
        public int StartingLives { get; }
        public float HealthPerLife { get; }
        public float DamageTakenMultiplier { get; }
        public float EnemySpeedMultiplier { get; }
        public float EnemyCountMultiplier { get; }
        public float EnemyFireRateMultiplier { get; }
        public float PickupDropChanceMultiplier { get; }
        public float ScoreMultiplier { get; }

        DifficultySettings(Difficulty difficulty, int startingLives, float healthPerLife, float damageTakenMultiplier,
            float enemySpeedMultiplier, float enemyCountMultiplier, float enemyFireRateMultiplier,
            float pickupDropChanceMultiplier, float scoreMultiplier)
        {
            Difficulty = difficulty;
            StartingLives = startingLives;
            HealthPerLife = healthPerLife;
            DamageTakenMultiplier = damageTakenMultiplier;
            EnemySpeedMultiplier = enemySpeedMultiplier;
            EnemyCountMultiplier = enemyCountMultiplier;
            EnemyFireRateMultiplier = enemyFireRateMultiplier;
            PickupDropChanceMultiplier = pickupDropChanceMultiplier;
            ScoreMultiplier = scoreMultiplier;
        }

        public static DifficultySettings For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Cadet: return Cadet;
                case Difficulty.Pilot: return Pilot;
                case Difficulty.Ace: return Ace;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }
    }
}
