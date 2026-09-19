using System;

namespace YASS.Core
{
    public enum EnemySize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    public enum MeteorSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    /// <summary>Base point values from the GDD page "Scoring".</summary>
    public static class PointValues
    {
        public const int SolidMeteor = 150;
        public const int NoDamageBossBonus = 5000;
        public const int MaxLevelWeaponUpgrade = 500;
        public const int LevelClearMaximum = 2000;

        public static int Enemy(EnemySize size)
        {
            switch (size)
            {
                case EnemySize.Small: return 100;
                case EnemySize.Medium: return 250;
                case EnemySize.Large: return 500;
                default: throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        public static int SplittingMeteor(MeteorSize size)
        {
            switch (size)
            {
                case MeteorSize.Small: return 20;
                case MeteorSize.Medium: return 30;
                case MeteorSize.Large: return 50;
                default: throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        public static int Boss(int levelNumber)
        {
            if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            return 10000 * levelNumber;
        }

        /// <summary>Remaining health % x 20 (the percentage is not rounded first), so 0 to 2,000 points.</summary>
        public static int LevelClear(float healthFraction)
        {
            var clamped = Math.Max(0.0, Math.Min(1.0, healthFraction));
            return (int)Math.Round(clamped * LevelClearMaximum, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Running score with the kill chain multiplier (GDD page "Scoring").</summary>
    public sealed class ScoreKeeper
    {
        /// <summary>Absorbs float drift when the window is accumulated from many fixed ticks.</summary>
        const float WindowTolerance = 1e-4f;

        readonly double _difficultyMultiplier;
        readonly float _chainWindow;
        readonly int _chainMaxSteps;

        double _endlessMultiplier = 1.0;
        int _chainSteps;
        float _timeSinceLastKill;
        bool _chainLive;

        public long Score { get; private set; }
        public int Kills { get; private set; }

        /// <summary>For display. Scores are computed from the exact step count, not from this float.</summary>
        public float ChainMultiplier => (10 + _chainSteps) / 10f;

        public int ChainSteps => _chainSteps;

        /// <summary>Endless-mode cycle multiplier; 1.0 in Campaign.</summary>
        public float EndlessMultiplier => (float)_endlessMultiplier;

        internal ScoreKeeper(float difficultyMultiplier, float chainWindow = GameTuning.ChainWindowSeconds,
            int chainMaxSteps = GameTuning.ChainMaxSteps)
        {
            if (difficultyMultiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(difficultyMultiplier));
            if (chainWindow <= 0f) throw new ArgumentOutOfRangeException(nameof(chainWindow));
            if (chainMaxSteps < 0) throw new ArgumentOutOfRangeException(nameof(chainMaxSteps));

            _difficultyMultiplier = difficultyMultiplier;
            _chainWindow = chainWindow;
            _chainMaxSteps = chainMaxSteps;
        }

        internal void SetEndlessMultiplier(float value)
        {
            if (value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
            _endlessMultiplier = value;
        }

        /// <summary>
        /// Registers a destroyed enemy or meteor. A kill within the chain window of the previous kill
        /// raises the chain before the points are awarded. Returns the points awarded.
        /// </summary>
        internal long RegisterKill(int basePoints)
        {
            if (basePoints < 0) throw new ArgumentOutOfRangeException(nameof(basePoints));

            if (_chainLive && _chainSteps < _chainMaxSteps) _chainSteps++;
            _chainLive = true;
            _timeSinceLastKill = 0f;
            Kills++;

            return Award(basePoints * (10.0 + _chainSteps) / 10.0);
        }

        /// <summary>Registers a bonus (level clear, no-damage boss, max-level upgrade). Not affected by the chain.</summary>
        internal long RegisterBonus(int basePoints)
        {
            if (basePoints < 0) throw new ArgumentOutOfRangeException(nameof(basePoints));
            return Award(basePoints);
        }

        /// <summary>The player took damage (not a shield-absorbed hit): the chain resets.</summary>
        internal void NotifyPlayerDamaged() => ResetChain();

        internal void Tick(float deltaTime)
        {
            if (!_chainLive) return;

            _timeSinceLastKill += deltaTime;
            if (_timeSinceLastKill > _chainWindow + WindowTolerance) ResetChain();
        }

        void ResetChain()
        {
            _chainSteps = 0;
            _chainLive = false;
            _timeSinceLastKill = 0f;
        }

        // Multipliers are exact binary fractions (x1.5, x2.5, x1.25...), so double arithmetic rounds .5 results
        // exactly and identically on every platform.
        long Award(double points)
        {
            var awarded = (long)Math.Round(points * _difficultyMultiplier * _endlessMultiplier,
                MidpointRounding.AwayFromZero);
            Score += awarded;
            return awarded;
        }
    }
}
