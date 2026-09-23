using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using YASS.Core;
using YASS.Feedback;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>What the HUD needs to know about the level beyond the rules session.</summary>
    public readonly struct LevelHudState
    {
        public readonly bool BossWarning;
        public readonly float BossHealthFraction;
        public readonly bool BossActive;

        /// <summary>The Endless cycle being played, or 0 in the campaign.</summary>
        public readonly int Cycle;

        public LevelHudState(bool bossWarning, bool bossActive, float bossHealthFraction, int cycle = 0)
        {
            BossWarning = bossWarning;
            BossActive = bossActive;
            BossHealthFraction = bossHealthFraction;
            Cycle = cycle;
        }
    }

    /// <summary>
    /// Composition root for one level. Owns the <see cref="GameSession"/> and the <see cref="WaveDirector"/> and
    /// drives both from FixedUpdate (the one fixed-step loop required by the Technical Design). Entities report
    /// collisions here; this class turns them into rules events and spawns whatever results, from object pools.
    /// Players are handled by index throughout, ready for co-op; the scene has one.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>How far outside the playfield an entity may travel before it is removed.</summary>
        public const float DespawnMargin = 2f;

        /// <summary>Approximate half-height of an enemy, used to keep wave-flying enemies on screen.</summary>
        const float EnemyHalfExtent = 0.5f;

        /// <summary>How far beyond the right edge the boss appears before flying in.</summary>
        const float BossSpawnOffset = 3f;

        const int PoolCapacity = 64;
        const int PoolMaxSize = 512;
        const int ProjectilePrewarm = 48;
        const int HazardPrewarm = 8;

        static readonly NVector2 EnemyFallbackAim = -NVector2.UnitX;

        [Header("Scene")]
        [SerializeField, Tooltip("Used when the scene is played directly; a real run uses the difficulty chosen on the menu.")]
        Difficulty difficulty = Difficulty.Pilot;
        [SerializeField, Tooltip("0 picks a time-based seed.")] int randomSeed;
        [SerializeField] Camera worldCamera;
        [SerializeField] PlayerShipView[] players = Array.Empty<PlayerShipView>();
        [SerializeField, Tooltip("One per player, in the same order.")]
        PlayerInputReader[] inputs = Array.Empty<PlayerInputReader>();
        [SerializeField] HudView hud;
        [SerializeField] Transform spawnRoot;

        [Header("Player")]
        [SerializeField] PlayerDefinition playerDefinition;
        [SerializeField] Projectile playerProjectilePrefab;

        [Header("Level")]
        [SerializeField, Tooltip("The campaign's levels in order. Which one plays comes from the run.")]
        Campaign campaign;

        [SerializeField, Tooltip("Played when the scene is opened directly, outside a run started from the menus.")]
        LevelDefinition level;
        [SerializeField] Projectile enemyProjectilePrefab;
        [SerializeField] MeteorView meteorPrefab;
        [SerializeField, Min(0f)] float spawnEdgeMargin = 1f;

        [SerializeField, Tooltip("Dropped by Mine Layers.")] MineView minePrefab;

        [Header("Endless")]
        [SerializeField, Tooltip("What an Endless run is made of. The run says whether this scene is one.")]
        EndlessDefinition endless;

        [SerializeField, Tooltip("Which mode to play when the scene is opened directly, outside a run.")]
        GameMode modeWhenPlayedDirectly = GameMode.Campaign;
        [SerializeField, Tooltip("Paints the sky: a campaign level's own backdrop, or the ring an Endless run drifts through.")]
        SkyView sky;
        [SerializeField, Min(0f), Tooltip("Endless only: the pause before a cycle's first wave.")]
        float endlessFirstWaveDelay = 1f;

        [Header("Pickups and flow")]
        [SerializeField] PickupView pickupPrefab;
        [SerializeField, Min(0f)] float pickupDriftSpeed = 2f;

        readonly List<ShotSpec> _shots = new List<ShotSpec>();
        readonly List<WaveSpawnRequest> _spawns = new List<WaveSpawnRequest>();
        readonly Dictionary<EnemyView, ObjectPool<EnemyView>> _enemyPools = new Dictionary<EnemyView, ObjectPool<EnemyView>>();
        readonly Dictionary<EnemyView, Action<EnemyView>> _enemyReleasers = new Dictionary<EnemyView, Action<EnemyView>>();

        GameSession _session;
        DifficultySettings _settings;

        /// <summary>The settings hazards are spawned with: the difficulty, escalated by the Endless cycle.</summary>
        DifficultySettings _spawnSettings;
        EndlessDirector _endless;
        WaveDirector _director;
        PlayerCommand[] _commands;
        PlayerCommand?[] _commandOverrides;
        Vector2[] _shipTargets;
        NVector2[] _shipMovement;
        NVector2[] _shipPull;
        IRandomSource _random;
        ObjectPool<Projectile> _playerProjectiles;
        ObjectPool<Projectile> _enemyProjectiles;
        ObjectPool<MeteorView> _meteors;
        ObjectPool<PickupView> _pickups;
        ObjectPool<MineView> _mines;
        Action<MeteorView> _releaseMeteor;
        Action<PickupView> _releasePickup;
        Action<MineView> _releaseMine;
        BossView _boss;
        bool _bossSpawned;
        bool _bossDefeated;
        bool _cycleTurning;
        LevelDefinition _level;

        /// <summary>
        /// What this level is holding in memory. Assets are loaded by path when they are needed and given
        /// back when they are not, rather than referenced from the scene, which would make the scene's
        /// dependency graph the whole game (see <see cref="ContentCache"/>).
        /// </summary>
        readonly ContentCache _content = new ContentCache();

        /// <summary>Test seam: what this level is holding in memory right now.</summary>
        internal ContentCache Content => _content;
        float _endTime = -1f;
        bool _sectorClear;

        public GameSession Session => _session;

        /// <summary>Which game this is: a campaign level or an Endless run (GDD "Core Loop").</summary>
        public GameMode Mode { get; private set; }
        public WaveDirector Director => _director;

        /// <summary>True when this is an Endless run rather than a campaign level.</summary>
        public bool IsEndless => _endless != null;

        /// <summary>The Endless run in progress, or null in the campaign.</summary>
        public EndlessRun EndlessRun => _endless?.Run;
        public BossView Boss => _boss;
        public Playfield Playfield { get; private set; }
        public int PlayerCount => players.Length;
        public bool IsSectorClear => _sectorClear;

        /// <summary>True while the level is being played: not game over and not yet cleared.</summary>
        public bool IsRunning => _session != null && !_session.IsGameOver && !_sectorClear;

        /// <summary>The difficulty this run is being played on.</summary>
        public Difficulty Difficulty { get; private set; }

        /// <summary>
        /// Seconds since the run ended, or -1 while it is still going. The flow layer waits a beat on this before
        /// covering the screen with a results panel.
        /// </summary>
        public float SecondsSinceRunEnded => _endTime < 0f ? -1f : Time.time - _endTime;

        /// <summary>
        /// Once the boss is down the level is won: players can no longer be hurt during the short delay before
        /// Sector Clear (they can still collect the boss's drops).
        /// </summary>
        public bool IsBossDefeated => _bossDefeated;

        public PlayerShipView GetPlayerView(int playerIndex) => players[playerIndex];

        /// <summary>The boss has arrived and is still alive (the prewarmed instance does not count until then).</summary>
        bool BossInPlay => _bossSpawned && _boss != null && _boss.IsAlive;

        public LevelHudState HudState => new LevelHudState(
            IsRunning && _director.Phase == LevelPhase.BossWarning,
            BossInPlay,
            BossInPlay ? _boss.Brain.Health.Current / _boss.Brain.Health.Max : 0f,
            _endless != null ? _endless.Run.Cycle : 0);

        /// <summary>Test seam: while set, replaces the input device for that player.</summary>
        internal void SetCommandOverride(int playerIndex, PlayerCommand? command) => _commandOverrides[playerIndex] = command;

        /// <summary>Test seam: pauses the wave script so a test controls every hazard.</summary>
        internal bool SpawningEnabled { get; set; } = true;

        /// <summary>Test seam: skips the waves and brings the boss in now.</summary>
        internal void StartBossFightNow()
        {
            _director.SkipToBoss();
            SpawnBoss();
        }

        internal int ActivePlayerProjectiles => _playerProjectiles.CountActive;

        void Awake()
        {
            // A run started from the menus carries its difficulty and its mode; opening the scene directly
            // uses the fields, so the level is still playable on its own in the Editor. Resolved before the
            // check below, which needs to know which game this is before it can say what is missing.
            Difficulty = RunContext.IsConfigured ? RunContext.Difficulty : difficulty;
            Mode = RunContext.IsConfigured ? RunContext.Mode : modeWhenPlayedDirectly;
            _level = ResolveLevel();

            if (!HasValidSetup())
            {
                enabled = false;
                return;
            }
            _settings = DifficultySettings.For(Difficulty);
            _spawnSettings = _settings;
            _random = new SystemRandomSource(randomSeed != 0 ? randomSeed : Environment.TickCount);
            _session = new GameSession(_settings, _random, players.Length);

            // One scene plays every level and Endless too, so the run says which game this is, not the scene.
            if (Mode == GameMode.Endless) _endless = new EndlessDirector(endless, Difficulty, _random);
            else _director = new WaveDirector(_level.ToSpecs(), _settings.EnemyCountMultiplier, _level.FirstWaveDelay);

            RestoreCampaignProgress();
            _commands = new PlayerCommand[players.Length];
            _commandOverrides = new PlayerCommand?[players.Length];
            _shipTargets = new Vector2[players.Length];
            _shipMovement = new NVector2[players.Length];
            _shipPull = new NVector2[players.Length];

            _playerProjectiles = CreatePool(playerProjectilePrefab, ProjectilePrewarm);
            _enemyProjectiles = CreatePool(enemyProjectilePrefab, ProjectilePrewarm);
            _meteors = CreatePool(meteorPrefab, HazardPrewarm);
            _pickups = CreatePool(pickupPrefab, HazardPrewarm);
            _mines = CreatePool(minePrefab, HazardPrewarm);
            _releaseMeteor = m =>
            {
                _director.NotifyHazardGone(m.WaveIndex);
                _meteors.Release(m);
            };
            _releasePickup = p => _pickups.Release(p);
            _releaseMine = m => _mines.Release(m);

            if (_endless != null) RegisterEndlessPools();
            else RegisterLevelPools();

            UpdatePlayfield();
            for (var i = 0; i < players.Length; i++) players[i].Init(this, i);
        }

        /// <summary>The enemies and boss of one campaign level, and whatever that boss launches.</summary>
        void RegisterLevelPools()
        {
            // One scene plays every level, so the backdrop comes from the level's own data rather than from
            // whichever sprite a scene happened to be saved with, and only this level's is held.
            if (sky != null) sky.Show(_content, _level.BackdropPath);

            // Only the boss this level actually has: prewarming a pool of Darts for a boss that launches
            // nothing would cost every level eight instances it never uses.
            var bossMinion = _level.BossPrefab != null && _level.BossPrefab.Definition != null
                ? _level.BossPrefab.Definition.MinionPrefab
                : null;
            if (bossMinion != null) RegisterEnemyPool(bossMinion);
            foreach (var wave in _level.Waves)
            foreach (var group in wave.groups)
                if (group.enemy != null) RegisterEnemyPool(group.enemy);

            // Created up front (inactive) so its particle systems do not hitch the boss's entrance.
            _boss = Instantiate(_level.BossPrefab, spawnRoot);
            _boss.gameObject.SetActive(false);
        }

        /// <summary>
        /// Everything any cycle could ask for. Endless draws its waves and its boss fresh each cycle, so the
        /// pools have to cover the whole campaign rather than one level's worth.
        /// </summary>
        void RegisterEndlessPools()
        {
            foreach (var enemy in _endless.AllEnemies()) RegisterEnemyPool(enemy);
            foreach (var boss in _endless.AllBosses())
                if (boss.Definition != null && boss.Definition.MinionPrefab != null)
                    RegisterEnemyPool(boss.Definition.MinionPrefab);

            BeginEndlessCycle(advance: false);

            // Opened on any of the skies, so two runs do not begin under the same one. Only the sky showing
            // and the one arriving are ever held, not the whole ring.
            if (sky != null)
                sky.Begin(_content, endless.SkyPaths, (int)(_random.NextFloat() * endless.SkyPaths.Count));
        }

        /// <summary>
        /// Starts a cycle: its ten waves, its boss and its sky (GDD "Core Loop", Endless mode). Called once at
        /// the start of the run and again each time a boss goes down. The run's cycle number moves here rather
        /// than when the boss dies, so it always names the cycle actually being played.
        /// </summary>
        void BeginEndlessCycle(bool advance)
        {
            if (advance) _endless.CompleteCycle();

            // Hazards still on screen belong to the cycle that is over. Their release callbacks read the
            // director at the time they fire, and both cycles number their waves 0 to 9, so a straggler would
            // otherwise tell the new script that one of its waves had been cleared.
            DisownLiveHazards();

            var specs = _endless.BeginCycle();
            var run = _endless.Run;

            _spawnSettings = _settings.Escalated(run.EnemySpeedMultiplier, run.EnemyCountMultiplier);
            _director = new WaveDirector(specs, _spawnSettings.EnemyCountMultiplier, endlessFirstWaveDelay);
            _session.SetEndlessMultiplier(run.ScoreMultiplier);

            // The sky is deliberately not touched here. It drifts on its own clock (see SkyView and SkyCycle):
            // changing it with the cycle put a hard cut at the moment a boss died, which is the busiest moment
            // of the run and the one place a seam is certain to be seen.

            // The boss warning switched the music over; the waves of the next cycle are not a boss fight.
            MusicPlayer.PlayIfPresent(Track.Level);

            // Each cycle brings its own boss, built inactive now so its entrance does not hitch later.
            if (_boss != null) Destroy(_boss.gameObject);
            _boss = Instantiate(_endless.BossPrefab, spawnRoot);
            _boss.gameObject.SetActive(false);
            _bossSpawned = false;
            _bossDefeated = false;
        }

        /// <summary>
        /// Takes the leftovers of a finished cycle out of the wave accounting: they still fly, still hurt and
        /// still score, but they no longer belong to a wave (GDD "Wave System": a wave is cleared when the
        /// hazards it spawned are gone).
        /// </summary>
        void DisownLiveHazards()
        {
            foreach (var enemy in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
                if (enemy.IsAlive) enemy.WaveIndex = -1;

            foreach (var meteor in FindObjectsByType<MeteorView>(FindObjectsSortMode.None))
                if (meteor.IsAlive) meteor.WaveIndex = -1;
        }

        void OnDestroy()
        {
            _playerProjectiles?.Dispose();
            _enemyProjectiles?.Dispose();
            _meteors?.Dispose();
            _pickups?.Dispose();
            _mines?.Dispose();
            foreach (var pool in _enemyPools.Values) pool.Dispose();

            // Leaving the level: nothing it was holding is wanted any more. The next level asks for its own.
            if (sky != null) sky.ReleaseAll();
            _content.Clear();
        }

        void FixedUpdate()
        {
            UpdatePlayfield();

            if (_cycleTurning)
            {
                _cycleTurning = false;

                // Not if the run ended in the same batch of collisions that killed the boss: a new cycle would
                // raise the cycle count and restart the level music behind the Game Over screen.
                if (IsRunning) BeginEndlessCycle(advance: true);
            }

            // Before the end-of-run check: the sky keeps drifting under the Game Over screen, which would
            // otherwise freeze mid-dissolve behind it.
            if (sky != null) sky.Tick(Time.fixedDeltaTime);

            CheckEndOfRun();
            if (!IsRunning) return;

            var deltaTime = Time.fixedDeltaTime;
            var bounds = Playfield.Inset(playerDefinition.EdgeMargin);
            for (var i = 0; i < players.Length; i++)
            {
                var shipPosition = players[i].Position.ToNumerics();
                _commands[i] = _commandOverrides[i] ?? inputs[i].ReadCommand(shipPosition, worldCamera);

                if (_session.GetPlayer(i).IsGameOver) continue;
                var next = ShipMotor.Step(shipPosition, _commands[i].Move, playerDefinition.Speed, deltaTime, bounds);

                // The engine follows what the player asked for, so it is read before anything drags the ship:
                // being pulled by a gravity well is not thrust, and the exhaust must not claim it is.
                var maxStep = playerDefinition.Speed * deltaTime;
                _shipMovement[i] = maxStep > 0f ? (next - shipPosition) / maxStep : NVector2.Zero;

                // A pull asked for last step is applied with this one, so the ship is still moved exactly once
                // per fixed update and still cannot be dragged off the playfield.
                if (_shipPull[i] != NVector2.Zero)
                {
                    next = bounds.Clamp(next + _shipPull[i] * deltaTime);
                    _shipPull[i] = NVector2.Zero;
                }

                _shipTargets[i] = next.ToUnity();
                players[i].MoveTo(_shipTargets[i]);
            }

            _shots.Clear();
            _session.Tick(deltaTime, _commands, _shots);
            foreach (var shot in _shots) SpawnPlayerShot(shot);

            // The test seam only pauses the wave script; boss arrival and Sector Clear still run.
            if (SpawningEnabled || _director.Phase != LevelPhase.Waves) TickWaves(deltaTime);

            CheckEndOfRun();
        }

        void Update()
        {
            if (_session == null) return;

            hud.Refresh(_session, HudState);
            for (var i = 0; i < players.Length; i++)
            {
                if (_session.GetPlayer(i).IsGameOver) continue;
                players[i].Present(_session.GetPlayer(i));
                players[i].DriveEngine(_shipMovement[i], Time.deltaTime);
                players[i].PresentAim(_commands[i].Fire, _commands[i].AimDirection, Time.deltaTime);
            }
        }

        public bool IsInsidePlayfield(Vector2 position, float margin) =>
            Playfield.Inset(-margin).Contains(position.ToNumerics());

        /// <summary>Unit vector from <paramref name="origin"/> towards the nearest player still in the game.</summary>
        public Vector2 AimAtNearestPlayer(Vector2 origin) =>
            EnemyMotion.AimAt(origin.ToNumerics(), NearestPlayerPosition(origin).ToNumerics(), EnemyFallbackAim).ToUnity();

        // ---- Events reported by entities ------------------------------------------------------------------

        public void OnEnemyDestroyed(EnemyView enemy, int playerIndex)
        {
            if (!IsRunning)
            {
                enemy.Despawn();
                return;
            }

            _session.ReportKill(playerIndex, enemy.Points);
            TryDropPickup(DropSource.Enemy, playerIndex, enemy.Position);
            ShowEnemyDeath(enemy);
            enemy.Despawn();
        }

        public void OnMeteorDestroyed(MeteorView meteor, int playerIndex)
        {
            if (!IsRunning)
            {
                meteor.Despawn();
                return;
            }

            var definition = meteor.Definition;
            _session.ReportKill(playerIndex, meteor.Points);
            TryDropPickup(MeteorRules.DropSourceFor(definition.Splitting), playerIndex, meteor.Position);
            ShowMeteorDeath(definition, meteor.Position);

            if (definition.Splitting && definition.Fragment != null && MeteorRules.TrySplit(definition.Size, out _))
            {
                var parentVelocity = meteor.Velocity.sqrMagnitude > 1e-6f ? meteor.Velocity : Vector2.left;
                for (var i = 0; i < MeteorRules.FragmentsPerSplit; i++)
                {
                    var direction = NVector2.Normalize(MeteorRules.FragmentVelocity(parentVelocity.ToNumerics(), i));
                    // Fragments join the parent's wave before the parent leaves, so the wave never looks cleared early.
                    SpawnMeteor(definition.Fragment, meteor.Position, direction.ToUnity() * RandomSpeed(definition.Fragment),
                        meteor.WaveIndex);
                    _director.NotifyHazardAdded(meteor.WaveIndex);
                }
            }

            meteor.Despawn();
        }

        public void OnBossDefeated(BossView boss, int playerIndex)
        {
            _session.ReportBossDefeated(playerIndex, BossLevelNumber(boss));
            foreach (var pickup in PickupDropper.BossDrops) SpawnPickup(pickup, boss.Position);

            Cue.Spawn(Effect.BossExplosion, boss.Position, Sfx.BossExplosion);
            Cue.Shake(ScreenShake.BossDefeatedTrauma);
            boss.Despawn();

            if (_endless != null)
            {
                // Endless has no Sector Clear: the next cycle starts where this one ended, faster and fuller.
                // This is reached from a trigger callback, so the work itself waits for the next fixed step:
                // destroying and building a boss in the middle of physics is how a frame hitches.
                _boss = null; // despawned above; the next cycle builds its own
                _cycleTurning = true;
                return;
            }

            _director.NotifyBossDefeated();
            _bossDefeated = true;
        }

        /// <summary>What a boss is worth: its own level's number, whichever mode it turns up in (GDD "Scoring").</summary>
        int BossLevelNumber(BossView boss)
        {
            if (boss != null && boss.Definition != null) return boss.Definition.LevelNumber;

            // In Endless the level is only the scene's fallback, so a boss met there is worth its own number
            // or one; a boss without a definition is an authoring error.
            return _level != null ? _level.LevelNumber : 1;
        }

        /// <summary>Returns true when the projectile was used up (hit or absorbed); false lets it fly on.</summary>
        public bool OnPlayerHitByProjectile(PlayerShipView ship, Projectile projectile)
        {
            if (!IsRunning || IsBossDefeated) return false;

            var outcome = _session.ReportPlayerHit(ship.PlayerIndex, projectile.Damage);
            ShowPlayerHurt(ship, outcome);
            return outcome != HitOutcome.Ignored;
        }

        public void OnPlayerRammedEnemy(PlayerShipView ship, EnemyView enemy)
        {
            if (!IsRunning || IsBossDefeated || !enemy.IsAlive) return;

            var result = _session.ReportPlayerRam(ship.PlayerIndex, false, enemy.ContactDamage, enemy.Points);
            ShowPlayerHurt(ship, result.Outcome);
            if (!result.EnemyDestroyed) return;

            TryDropPickup(DropSource.Enemy, ship.PlayerIndex, enemy.Position);
            ShowEnemyDeath(enemy);
            enemy.Despawn();
        }

        /// <summary>A rammed meteor is destroyed without splitting (provisional rule), so fragments cannot chain-hit the ship.</summary>
        public void OnPlayerRammedMeteor(PlayerShipView ship, MeteorView meteor)
        {
            if (!IsRunning || IsBossDefeated || !meteor.IsAlive) return;

            var definition = meteor.Definition;
            var result = _session.ReportPlayerRam(ship.PlayerIndex, false, definition.ContactDamage, meteor.Points);
            ShowPlayerHurt(ship, result.Outcome);
            if (!result.EnemyDestroyed) return;

            TryDropPickup(MeteorRules.DropSourceFor(definition.Splitting), ship.PlayerIndex, meteor.Position);
            ShowMeteorDeath(definition, meteor.Position);
            meteor.Despawn();
        }

        public void OnPlayerRammedBoss(PlayerShipView ship, BossView boss)
        {
            if (!IsRunning || IsBossDefeated || !boss.IsAlive) return;

            var outcome = _session.ReportPlayerRam(ship.PlayerIndex, true, boss.ContactDamage, 0).Outcome;
            ShowPlayerHurt(ship, outcome);
        }

        public void OnPickupCollected(PlayerShipView ship, PickupView pickup)
        {
            if (!IsRunning || !pickup.IsActive) return;

            _session.ReportPickupCollected(ship.PlayerIndex, pickup.Type);
            Cue.Spawn(Effect.PickupSparkle, pickup.transform.position,
                pickup.Type == PickupType.WeaponUpgrade ? Sfx.WeaponUpgrade : Sfx.Pickup);
            pickup.Despawn();
        }

        /// <summary>Fires an enemy bullet at the nearest player still in the game.</summary>
        public void FireEnemyProjectile(Vector2 origin, float speed, float damage) =>
            FireEnemyProjectile(origin, AimAtNearestPlayer(origin), speed, damage);

        public void FireEnemyProjectile(Vector2 origin, Vector2 direction, float speed, float damage)
        {
            if (!IsRunning) return;

            _enemyProjectiles.Get().Launch(this, _enemyProjectiles, origin, direction, speed, damage, false, -1);
            Cue.Play(Sfx.EnemyShot);
        }

        /// <summary>
        /// Drops a proximity mine (Mine Layer). Mines belong to no wave: a wave must not wait on mines nobody
        /// goes near, and they clear themselves after <see cref="MineSpec.LifetimeSeconds"/>.
        /// </summary>
        public void DropMine(Vector2 position)
        {
            if (!IsRunning) return;
            _mines.Get().Init(this, position, _releaseMine);
        }

        /// <summary>
        /// A mine goes off: everyone inside the blast is hurt, wherever it was triggered from.
        /// <paramref name="playerIndex"/> is who shot it down, or -1 when a player simply came too close.
        /// </summary>
        public void OnMineDetonated(MineView mine, int playerIndex)
        {
            if (!mine.IsAlive) return;

            if (IsRunning && !IsBossDefeated)
                for (var i = 0; i < players.Length; i++)
                {
                    if (_session.GetPlayer(i).IsGameOver) continue;
                    if (Vector2.Distance(players[i].Position, mine.Position) > MineSpec.TriggerRadius) continue;

                    ShowPlayerHurt(players[i], _session.ReportPlayerHit(i, MineSpec.Damage));
                }

            if (IsRunning && playerIndex >= 0) _session.ReportKill(playerIndex, mine.Points);

            Cue.Spawn(Effect.SmallExplosion, mine.Position, Sfx.SmallExplosion);
            Cue.Shake(ScreenShake.PlayerHitTrauma * 0.5f);
            mine.Despawn();
        }

        /// <summary>
        /// A beam: a line, not a projectile, so it hits the instant it fires. The warning or the sweep before it
        /// is the player's only chance to be somewhere else (GDD "Enemies and Hazards", "Levels").
        /// Returns how many players it caught, which is how a sweeping beam knows a burn landed.
        /// </summary>
        public int FireBeam(Vector2 origin, Vector2 aim, float length, float halfWidth, float damage)
        {
            if (!IsRunning || IsBossDefeated) return 0;

            var hits = 0;
            for (var i = 0; i < players.Length; i++)
            {
                if (_session.GetPlayer(i).IsGameOver) continue;
                if (!SniperShot.HitsPoint(origin.ToNumerics(), aim.ToNumerics(), players[i].Position.ToNumerics(),
                        halfWidth, length)) continue;

                ShowPlayerHurt(players[i], _session.ReportPlayerHit(i, damage));
                hits++;
            }

            return hits;
        }

        /// <summary>An enemy launched by a boss. Like the boss's own shots, it belongs to no wave.</summary>
        public void LaunchBossMinion(EnemyView prefab, Vector2 position)
        {
            if (!IsRunning || prefab == null) return;

            // A boss the level did not declare can still launch: register its pool the first time it does.
            if (!_enemyPools.ContainsKey(prefab)) RegisterEnemyPool(prefab);
            SpawnEnemy(prefab, position, -1);
        }

        /// <summary>A rock thrown by a boss (the Rock Crusher). It belongs to no wave and does not split on a ram.</summary>
        public void HurlBossMeteor(MeteorDefinition definition, Vector2 position, Vector2 velocity)
        {
            if (!IsRunning || definition == null) return;
            SpawnMeteor(definition, position, velocity, -1);
        }

        /// <summary>
        /// Drags every player towards <paramref name="origin"/> for this step (the Singularity Engine). The pull
        /// is applied with the ship's own movement so it cannot fight it for the transform, and it fades with
        /// distance, so the edges of the screen stay a refuge.
        /// </summary>
        public void PullPlayers(Vector2 origin, float strength, float radius)
        {
            if (!IsRunning || IsBossDefeated) return;

            for (var i = 0; i < players.Length; i++)
            {
                if (_session.GetPlayer(i).IsGameOver) continue;

                var toOrigin = origin - players[i].Position;
                var distance = toOrigin.magnitude;
                var pull = BossPatterns.PullStrength(distance, radius, strength);
                if (pull <= 0f || distance < 1e-4f) continue;

                _shipPull[i] += (toOrigin / distance).ToNumerics() * pull;
            }
        }

        // ---- Internals ---------------------------------------------------------------------------------------

        void TickWaves(float deltaTime)
        {
            _spawns.Clear();
            var levelEvent = _director.Tick(deltaTime, _spawns);
            foreach (var request in _spawns) SpawnFromWave(request);
            if (levelEvent == LevelEvent.BossWarning)
            {
                Cue.Play(Sfx.BossWarning);
                MusicPlayer.PlayIfPresent(Track.Boss); // the warning is where the fight starts to feel different
            }
            else if (levelEvent == LevelEvent.BossArrives) SpawnBoss();
            else if (levelEvent == LevelEvent.SectorClear) ClearSector();
        }

        void SpawnFromWave(WaveSpawnRequest request)
        {
            var group = _endless != null
                ? _endless.GroupFor(request.WaveIndex, request.GroupIndex)
                : _level.GetGroup(request.WaveIndex, request.GroupIndex);
            var margin = SpawnMarginFor(group.enemy);
            var position = new Vector2(Playfield.MaxX + spawnEdgeMargin + request.XOffset,
                Playfield.Inset(margin).YAt(request.NormalisedY));

            if (group.enemy != null)
            {
                SpawnEnemy(group.enemy, position, request.WaveIndex);
            }
            else
            {
                var definition = group.meteor;
                var vertical = (_random.NextFloat() * 2f - 1f) * definition.MaxVerticalSpeed *
                               _spawnSettings.EnemySpeedMultiplier;
                SpawnMeteor(definition, position, new Vector2(-RandomSpeed(definition), vertical), request.WaveIndex);
            }
        }

        void SpawnBoss()
        {
            if (_bossSpawned) return;
            _bossSpawned = true;

            var centreY = (Playfield.MinY + Playfield.MaxY) * 0.5f;
            _boss.gameObject.SetActive(true);
            // The boss escalates with everything else: an Endless cycle's "+10% enemy speed" is not a rule
            // about small ships (GDD "Core Loop", Endless mode).
            _boss.Init(this, _spawnSettings, new Vector2(Playfield.MaxX + BossSpawnOffset, centreY));
            _session.BeginBossFight();
        }

        void SpawnEnemy(EnemyView prefab, Vector2 position, int waveIndex)
        {
            var enemy = _enemyPools[prefab].Get();
            enemy.Init(this, _spawnSettings, position, _enemyReleasers[prefab]);
            enemy.WaveIndex = waveIndex;
        }

        void RegisterEnemyPool(EnemyView prefab)
        {
            if (_enemyPools.ContainsKey(prefab)) return;

            var pool = CreatePool(prefab, HazardPrewarm);
            _enemyPools.Add(prefab, pool);
            _enemyReleasers.Add(prefab, e =>
            {
                _director.NotifyHazardGone(e.WaveIndex);
                pool.Release(e);
            });
        }

        /// <summary>
        /// Mid-campaign, players keep the lives, health and weapon they finished the last level with
        /// (GDD "Core Loop": a campaign is one run, not eight separate games).
        /// </summary>
        void RestoreCampaignProgress()
        {
            var run = RunContext.Campaign;
            if (run == null) return;

            for (var i = 0; i < players.Length; i++)
            {
                var carry = run.CarryFor(i);
                if (carry.HasValue) _session.RestorePlayer(i, carry.Value);
            }
        }

        void UpdatePlayfield()
        {
            // The steady position, not the current one: a screen shake must not drag the playfield (and with it
            // the ships clamped to its edges) around the screen.
            var cameraPosition = CameraShaker.SteadyPosition(worldCamera);
            Playfield = Playfield.FromCamera(cameraPosition.ToNumerics(), worldCamera.orthographicSize, worldCamera.aspect);
        }

        /// <summary>
        /// Hides ships whose game is over and records when the run ended. Called only from FixedUpdate, never from
        /// trigger callbacks, so ships are not deactivated while physics is still reporting their contacts.
        /// </summary>
        void CheckEndOfRun()
        {
            for (var i = 0; i < players.Length; i++)
                if (_session.GetPlayer(i).IsGameOver && players[i].gameObject.activeSelf)
                    players[i].SetAlive(false);

            if (_endTime < 0f && (_session.IsGameOver || _sectorClear))
            {
                _endTime = Time.time;
                // Input is no longer read, so drop the last command: ships level off and engines idle.
                Array.Clear(_commands, 0, _commands.Length);
                Array.Clear(_shipMovement, 0, _shipMovement.Length);
                Array.Clear(_shipPull, 0, _shipPull.Length);
            }
        }

        /// <summary>The Sector Clear delay after the boss has passed: award each player's level-clear bonus.</summary>
        void ClearSector()
        {
            if (_sectorClear || _session.IsGameOver) return;

            for (var i = 0; i < players.Length; i++) _session.ReportLevelClear(i);
            _sectorClear = true;
        }

        /// <summary>
        /// The position of the nearest player still in the game, or a point to the left when there is none:
        /// what everything that aims at or dives at the player uses. Anything that measures the distance instead
        /// must use <see cref="TryGetNearestPlayer"/>, because the fallback is only one unit away.
        /// </summary>
        public Vector2 NearestPlayerPosition(Vector2 from) =>
            TryGetNearestPlayer(from, out var position) ? position : from + Vector2.left;

        /// <summary>The nearest player still in the game, or false when every player is out.</summary>
        public bool TryGetNearestPlayer(Vector2 from, out Vector2 position)
        {
            position = default;
            var bestDistance = float.MaxValue;
            var found = false;
            for (var i = 0; i < players.Length; i++)
            {
                if (_session.GetPlayer(i).IsGameOver) continue;
                var distance = (players[i].Position - from).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                position = players[i].Position;
                found = true;
            }

            return found;
        }

        void SpawnPlayerShot(ShotSpec shot)
        {
            var direction = shot.Direction.ToUnity();
            var position = players[shot.PlayerIndex].MuzzleAt(_shipTargets[shot.PlayerIndex], direction) +
                           shot.Offset.ToUnity();
            _playerProjectiles.Get().Launch(this, _playerProjectiles, position, direction,
                playerDefinition.ProjectileSpeed, playerDefinition.ProjectileDamage, shot.Piercing, shot.PlayerIndex);

            Cue.Spawn(Effect.MuzzleFlash, position, Sfx.PlayerShot);
        }

        /// <summary>
        /// One place for every way the player can be hurt, so a shot, a ram and a boss collision cannot drift
        /// apart: an absorbed hit ripples off the shield, a real hit shakes the screen, and losing a life
        /// shakes it harder.
        /// </summary>
        void ShowPlayerHurt(PlayerShipView ship, HitOutcome outcome)
        {
            switch (outcome)
            {
                case HitOutcome.Absorbed:
                    Cue.Spawn(Effect.ShieldRipple, ship.Position, Sfx.ShieldHit);
                    break;
                case HitOutcome.Damaged:
                    Cue.Play(Sfx.PlayerHit);
                    Cue.Shake(ScreenShake.PlayerHitTrauma);
                    ship.Flash();
                    break;
                case HitOutcome.LifeLost:
                case HitOutcome.GameOver:
                    Cue.Spawn(Effect.LargeExplosion, ship.Position, Sfx.LifeLost);
                    Cue.Shake(ScreenShake.LifeLostTrauma);
                    break;
            }
        }

        /// <summary>Bigger enemies go out with a bigger bang.</summary>
        static void ShowEnemyDeath(EnemyView enemy)
        {
            var large = enemy.Size >= EnemySize.Large;
            Cue.Spawn(large ? Effect.LargeExplosion : Effect.SmallExplosion, enemy.Position,
                large ? Sfx.LargeExplosion : Sfx.SmallExplosion);
        }

        /// <summary>
        /// One death for a meteor however it died: shooting one and flying into the same rock used to sound
        /// different. Splitting rocks crack; solid ones blow up, and big ones do it loudly.
        /// </summary>
        static void ShowMeteorDeath(MeteorDefinition definition, Vector2 position)
        {
            var large = definition.Size >= MeteorSize.Large;
            var effect = large ? Effect.LargeExplosion : Effect.SmallExplosion;
            var sound = definition.Splitting
                ? Sfx.MeteorBreak
                : large ? Sfx.LargeExplosion : Sfx.SmallExplosion;

            Cue.Spawn(effect, position, sound);
        }

        float RandomSpeed(MeteorDefinition definition) =>
            Mathf.Lerp(definition.MinSpeed, definition.MaxSpeed, _random.NextFloat()) *
            _spawnSettings.EnemySpeedMultiplier;

        void SpawnMeteor(MeteorDefinition definition, Vector2 position, Vector2 velocity, int waveIndex)
        {
            var spin = (_random.NextFloat() * 2f - 1f) * definition.MaxSpinDegrees;
            var meteor = _meteors.Get();
            meteor.Init(this, definition, position, velocity, spin, _releaseMeteor);
            meteor.WaveIndex = waveIndex;
        }

        void TryDropPickup(DropSource source, int playerIndex, Vector2 position)
        {
            if (_session.TryRollDrop(source, playerIndex, out var type)) SpawnPickup(type, position);
        }

        void SpawnPickup(PickupType type, Vector2 position) =>
            _pickups.Get().Init(this, type, pickupDriftSpeed, position, _releasePickup);

        /// <summary>Wave-flying enemies need a wider margin so their whole path stays on screen.</summary>
        float SpawnMarginFor(EnemyView enemy)
        {
            if (enemy == null || enemy.Definition.Pattern != MotionPattern.SineWave) return spawnEdgeMargin;
            return Mathf.Max(spawnEdgeMargin, enemy.Definition.WaveAmplitude + EnemyHalfExtent);
        }

        ObjectPool<T> CreatePool<T>(T prefab, int prewarm) where T : Component
        {
            var pool = new ObjectPool<T>(
                () => Instantiate(prefab, spawnRoot),
                item => item.gameObject.SetActive(true),
                item => item.gameObject.SetActive(false),
                item => { if (item != null) Destroy(item.gameObject); },
                collectionCheck: false, defaultCapacity: PoolCapacity, maxSize: PoolMaxSize);

            var warm = new T[prewarm];
            for (var i = 0; i < prewarm; i++) warm[i] = pool.Get();
            foreach (var item in warm) pool.Release(item);
            return pool;
        }

        /// <summary>
        /// Which level to play. A run started from the menus says where it has got to and the campaign says
        /// what that step is; a scene opened directly in the Editor falls back to its own field, so a level is
        /// still playable on its own without going through the title screen.
        /// </summary>
        LevelDefinition ResolveLevel()
        {
            // No run: the scene was opened directly in the Editor, so its own field is the answer.
            var run = RunContext.Campaign;
            if (run == null) return level;

            // A run in progress must get the level it has reached or nothing at all. Falling back here would
            // quietly replay level 1 for the rest of the campaign, which is the failure the old model could
            // not have: loading a scene by a name that did not exist was loud.
            return campaign != null ? campaign.LevelFor(run.LevelIndex) : null;
        }

        bool HasValidSetup()
        {
            var valid = true;

            void Fail(string message)
            {
                Debug.LogError($"{nameof(GameRunner)}: {message}", this);
                valid = false;
            }

            void Require(UnityEngine.Object reference, string fieldName)
            {
                if (reference == null) Fail($"'{fieldName}' is not assigned.");
            }

            Require(worldCamera, nameof(worldCamera));
            Require(hud, nameof(hud));
            Require(spawnRoot, nameof(spawnRoot));
            Require(playerDefinition, nameof(playerDefinition));
            Require(playerProjectilePrefab, nameof(playerProjectilePrefab));
            Require(sky, nameof(sky)); // one scene, so the backdrop is painted rather than saved with it
            if (Mode == GameMode.Endless)
            {
                if (endless == null) Fail("an Endless run was started but 'endless' is not assigned.");
                else if (endless.Validate() is string endlessProblem) Fail($"Endless: {endlessProblem}.");
            }
            else if (_level == null)
            {
                var run = RunContext.Campaign;
                if (run == null) Fail("no level to play: 'level' is not assigned.");
                else if (campaign == null) Fail("a campaign run is in progress but 'campaign' is not assigned.");
                else Fail($"the campaign has no level {run.LevelNumber} to play.");
            }
            Require(enemyProjectilePrefab, nameof(enemyProjectilePrefab));
            Require(meteorPrefab, nameof(meteorPrefab));
            Require(pickupPrefab, nameof(pickupPrefab));
            Require(minePrefab, nameof(minePrefab));

            if (worldCamera != null && !worldCamera.orthographic) Fail("the world camera must be orthographic.");
            if (Mode != GameMode.Endless && _level != null && _level.Validate() is string problem)
                Fail($"level '{_level.name}': {problem}.");

            if (players.Length < 1 || players.Length > 4) Fail("there must be between 1 and 4 players.");
            if (inputs.Length != players.Length) Fail("there must be one input reader per player.");
            for (var i = 0; i < players.Length; i++) Require(players[i], $"{nameof(players)}[{i}]");
            // Readers validate their own actions asset in Awake, which runs after this one.
            for (var i = 0; i < inputs.Length; i++) Require(inputs[i], $"{nameof(inputs)}[{i}]");

            return valid;
        }
    }
}
