using System;
using System.Collections.Generic;

namespace YASS.Core
{
    /// <summary>
    /// Rules state for one run: players, score and pickup drops. This is the only way presentation code changes
    /// rules state: it reports events (kills, hits, pickups) and reads the results.
    /// Call <see cref="Tick"/> and all Report methods from the same fixed-step loop (FixedUpdate, later Fusion's
    /// FixedUpdateNetwork) so timers advance in constant steps and event order is deterministic.
    /// </summary>
    public sealed class GameSession
    {
        readonly PlayerShip[] _players;
        readonly float[] _bossContactCooldown;

        bool _bossFightActive;
        bool _damagedDuringBossFight;

        public DifficultySettings Settings { get; }
        public ScoreKeeper Score { get; }
        public PickupDropper Drops { get; }
        public int PlayerCount => _players.Length;

        public bool IsGameOver
        {
            get
            {
                foreach (var player in _players)
                    if (!player.IsGameOver) return false;
                return true;
            }
        }

        public GameSession(DifficultySettings settings, IRandomSource random, int playerCount = 1)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (playerCount < 1 || playerCount > 4) throw new ArgumentOutOfRangeException(nameof(playerCount));

            _players = new PlayerShip[playerCount];
            _bossContactCooldown = new float[playerCount];
            for (var i = 0; i < playerCount; i++)
                _players[i] = new PlayerShip(i, settings);

            Score = new ScoreKeeper(settings.ScoreMultiplier);
            Drops = new PickupDropper(random, settings.PickupDropChanceMultiplier);
        }

        public PlayerShip GetPlayer(int playerIndex) => _players[CheckIndex(playerIndex)];

        /// <summary>
        /// Advances all timers by one step and applies each player's command. Shots fired are appended to
        /// <paramref name="shots"/> (tagged with the firing player's index). Game-over players do not fire.
        /// </summary>
        public void Tick(float deltaTime, IReadOnlyList<PlayerCommand> commands, List<ShotSpec> shots)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (commands.Count != _players.Length)
                throw new ArgumentException($"Expected {_players.Length} commands, got {commands.Count}.", nameof(commands));
            if (shots == null) throw new ArgumentNullException(nameof(shots));

            Score.Tick(deltaTime);
            Drops.Tick(deltaTime);
            for (var i = 0; i < _players.Length; i++)
            {
                _players[i].Tick(deltaTime, commands[i], shots);
                if (_bossContactCooldown[i] > 0f) _bossContactCooldown[i] = Math.Max(0f, _bossContactCooldown[i] - deltaTime);
            }
        }

        /// <summary>
        /// A player destroyed an enemy or meteor. Returns the points awarded. Kills credited to a player who is
        /// already out (for example by bullets still in flight at game over) score nothing.
        /// </summary>
        public long ReportKill(int playerIndex, int basePoints)
        {
            if (_players[CheckIndex(playerIndex)].IsGameOver) return 0;
            return Score.RegisterKill(basePoints);
        }

        public bool TryRollDrop(DropSource source, int playerIndex, out PickupType pickup)
        {
            var player = _players[CheckIndex(playerIndex)];
            if (player.IsGameOver)
            {
                pickup = PickupType.None;
                return false;
            }

            return Drops.TryRollDrop(source, player.Weapon.Level, out pickup);
        }

        public HitOutcome ReportPlayerHit(int playerIndex, float damage)
        {
            var outcome = _players[CheckIndex(playerIndex)].TakeHit(damage);
            OnPlayerHit(outcome);
            return outcome;
        }

        /// <summary>
        /// A player's ship collided with an enemy body. If the enemy is destroyed it counts as that player's kill
        /// and <paramref name="enemyBasePoints"/> are awarded, unless the collision itself ended the player's game.
        /// Boss collisions report every physics step while overlapping, so after one lands the player ignores boss
        /// contact for <see cref="GameTuning.BossContactCooldownSeconds"/>.
        /// </summary>
        public RamResult ReportPlayerRam(int playerIndex, bool isBoss, float contactDamage, int enemyBasePoints)
        {
            var player = _players[CheckIndex(playerIndex)];
            if (isBoss && _bossContactCooldown[playerIndex] > 0f) return new RamResult(HitOutcome.Ignored, false);

            var result = player.Ram(isBoss, contactDamage);
            if (isBoss && result.Outcome != HitOutcome.Ignored)
                _bossContactCooldown[playerIndex] = GameTuning.BossContactCooldownSeconds;
            OnPlayerHit(result.Outcome);
            if (result.EnemyDestroyed && !player.IsGameOver) Score.RegisterKill(enemyBasePoints);
            return result;
        }

        public void ReportPickupCollected(int playerIndex, PickupType pickup)
        {
            var player = _players[CheckIndex(playerIndex)];
            if (player.IsGameOver) return;

            if (pickup == PickupType.WeaponUpgrade) Drops.NotifyWeaponUpgradeCollected();
            if (player.Collect(pickup)) Score.RegisterBonus(PointValues.MaxLevelWeaponUpgrade);
        }

        /// <summary>A boss has appeared. Damage taken from now until it is defeated forfeits the no-damage bonus.</summary>
        public void BeginBossFight()
        {
            _bossFightActive = true;
            _damagedDuringBossFight = false;
        }

        /// <summary>
        /// The boss of <paramref name="levelNumber"/> was destroyed by a player. Awards the boss kill and, if no
        /// damage was taken since <see cref="BeginBossFight"/>, the no-damage bonus. Returns the points awarded.
        /// </summary>
        public long ReportBossDefeated(int playerIndex, int levelNumber)
        {
            if (_players[CheckIndex(playerIndex)].IsGameOver)
            {
                _bossFightActive = false;
                return 0;
            }

            var points = Score.RegisterKill(PointValues.Boss(levelNumber));
            if (_bossFightActive && !_damagedDuringBossFight)
                points += Score.RegisterBonus(PointValues.NoDamageBossBonus);

            _bossFightActive = false;
            return points;
        }

        /// <summary>Awards the level-clear bonus from the player's remaining health. Returns the points awarded.</summary>
        public long ReportLevelClear(int playerIndex)
        {
            var player = _players[CheckIndex(playerIndex)];
            if (player.IsGameOver) return 0;
            return Score.RegisterBonus(PointValues.LevelClear(player.Vitals.HealthFraction));
        }

        public void SetEndlessMultiplier(float multiplier) => Score.SetEndlessMultiplier(multiplier);

        void OnPlayerHit(HitOutcome outcome)
        {
            if (!outcome.TookDamage()) return;

            Score.NotifyPlayerDamaged();
            if (_bossFightActive) _damagedDuringBossFight = true;
        }

        int CheckIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _players.Length)
                throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null);
            return playerIndex;
        }
    }
}
