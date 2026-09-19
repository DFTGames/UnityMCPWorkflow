using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using YASS.Core;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>
    /// Composition root for one run. Owns the <see cref="GameSession"/> and drives it from FixedUpdate (the one
    /// fixed-step loop required by the Technical Design). Entities report collisions here; this class turns them
    /// into rules events and spawns whatever results (projectiles, fragments, pickups), all from object pools.
    /// Players are handled by index throughout, ready for co-op; the prototype scene has one.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>How far outside the playfield an entity may travel before it is removed.</summary>
        public const float DespawnMargin = 2f;

        /// <summary>Approximate half-height of an enemy, used to keep wave-flying enemies on screen.</summary>
        const float EnemyHalfExtent = 0.5f;

        const int PoolCapacity = 64;
        const int PoolMaxSize = 512;
        const int ProjectilePrewarm = 48;
        const int HazardPrewarm = 8;

        static readonly NVector2 EnemyFallbackAim = -NVector2.UnitX;

        [Serializable]
        public struct SpawnEntry
        {
            [Tooltip("Enemy to spawn. Leave empty to spawn the meteor instead.")]
            public EnemyView enemyPrefab;
            public MeteorDefinition meteor;
            [Min(0)] public int weight;
        }

        [Header("Scene")]
        [SerializeField] Difficulty difficulty = Difficulty.Pilot;
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

        [Header("Hazards (temporary prototype spawner)")]
        [SerializeField] Projectile enemyProjectilePrefab;
        [SerializeField] MeteorView meteorPrefab;
        [SerializeField] SpawnEntry[] spawnTable = Array.Empty<SpawnEntry>();
        [SerializeField, Min(0.05f)] float spawnInterval = 1.2f;
        [SerializeField, Min(0f)] float firstSpawnDelay = 1.5f;
        [SerializeField, Min(0f)] float spawnEdgeMargin = 1f;

        [Header("Pickups and flow")]
        [SerializeField] PickupView pickupPrefab;
        [SerializeField, Min(0f)] float pickupDriftSpeed = 2f;
        [SerializeField, Min(0f)] float restartDelay = 1f;

        readonly List<ShotSpec> _shots = new List<ShotSpec>();
        readonly List<SpawnRequest> _spawns = new List<SpawnRequest>();
        readonly Dictionary<EnemyView, ObjectPool<EnemyView>> _enemyPools = new Dictionary<EnemyView, ObjectPool<EnemyView>>();
        readonly Dictionary<EnemyView, Action<EnemyView>> _enemyReleasers = new Dictionary<EnemyView, Action<EnemyView>>();

        GameSession _session;
        DifficultySettings _settings;
        PlayerCommand[] _commands;
        PlayerCommand?[] _commandOverrides;
        Vector2[] _shipTargets;
        float[] _spawnMargins;
        SpawnDirector _spawner;
        IRandomSource _random;
        ObjectPool<Projectile> _playerProjectiles;
        ObjectPool<Projectile> _enemyProjectiles;
        ObjectPool<MeteorView> _meteors;
        ObjectPool<PickupView> _pickups;
        Action<MeteorView> _releaseMeteor;
        Action<PickupView> _releasePickup;
        float _gameOverTime = -1f;

        public GameSession Session => _session;
        public Playfield Playfield { get; private set; }
        public int PlayerCount => players.Length;
        public bool IsRunning => _session != null && !_session.IsGameOver;

        public PlayerShipView GetPlayerView(int playerIndex) => players[playerIndex];

        /// <summary>Test seam: while set, replaces the input device for that player.</summary>
        internal void SetCommandOverride(int playerIndex, PlayerCommand? command) => _commandOverrides[playerIndex] = command;

        /// <summary>Test seam: turns the random spawner off so a test controls every hazard.</summary>
        internal bool SpawningEnabled { get; set; } = true;

        internal int ActivePlayerProjectiles => _playerProjectiles.CountActive;

        void Awake()
        {
            if (!HasValidSetup())
            {
                enabled = false;
                return;
            }

            _settings = DifficultySettings.For(difficulty);
            _random = new SystemRandomSource(randomSeed != 0 ? randomSeed : Environment.TickCount);
            _session = new GameSession(_settings, _random, players.Length);
            _commands = new PlayerCommand[players.Length];
            _commandOverrides = new PlayerCommand?[players.Length];
            _shipTargets = new Vector2[players.Length];

            var weights = new int[spawnTable.Length];
            _spawnMargins = new float[spawnTable.Length];
            for (var i = 0; i < spawnTable.Length; i++)
            {
                weights[i] = spawnTable[i].weight;
                _spawnMargins[i] = SpawnMarginFor(spawnTable[i]);
            }

            _spawner = new SpawnDirector(weights, spawnInterval, _settings.EnemyCountMultiplier, _random, firstSpawnDelay);

            _playerProjectiles = CreatePool(playerProjectilePrefab, ProjectilePrewarm);
            _enemyProjectiles = CreatePool(enemyProjectilePrefab, ProjectilePrewarm);
            _meteors = CreatePool(meteorPrefab, HazardPrewarm);
            _pickups = CreatePool(pickupPrefab, HazardPrewarm);
            _releaseMeteor = m => _meteors.Release(m);
            _releasePickup = p => _pickups.Release(p);
            foreach (var entry in spawnTable)
                if (entry.enemyPrefab != null && !_enemyPools.ContainsKey(entry.enemyPrefab))
                {
                    var pool = CreatePool(entry.enemyPrefab, HazardPrewarm);
                    _enemyPools.Add(entry.enemyPrefab, pool);
                    _enemyReleasers.Add(entry.enemyPrefab, e => pool.Release(e));
                }

            UpdatePlayfield();
            for (var i = 0; i < players.Length; i++) players[i].Init(this, i);
        }

        void OnDestroy()
        {
            _playerProjectiles?.Dispose();
            _enemyProjectiles?.Dispose();
            _meteors?.Dispose();
            _pickups?.Dispose();
            foreach (var pool in _enemyPools.Values) pool.Dispose();
        }

        void FixedUpdate()
        {
            UpdatePlayfield();
            CheckGameOver();
            if (!IsRunning) return;

            var deltaTime = Time.fixedDeltaTime;
            var bounds = Playfield.Inset(playerDefinition.EdgeMargin);
            for (var i = 0; i < players.Length; i++)
            {
                var shipPosition = players[i].Position.ToNumerics();
                _commands[i] = _commandOverrides[i] ?? inputs[i].ReadCommand(shipPosition, worldCamera);

                if (_session.GetPlayer(i).IsGameOver) continue;
                _shipTargets[i] = ShipMotor.Step(shipPosition, _commands[i].Move, playerDefinition.Speed, deltaTime,
                    bounds).ToUnity();
                players[i].MoveTo(_shipTargets[i]);
            }

            _shots.Clear();
            _session.Tick(deltaTime, _commands, _shots);
            foreach (var shot in _shots) SpawnPlayerShot(shot);

            _spawns.Clear();
            if (SpawningEnabled && _spawner.Tick(deltaTime, _spawns))
                foreach (var request in _spawns) Spawn(request);

            CheckGameOver();
        }

        void Update()
        {
            if (_session == null) return;

            hud.Refresh(_session);
            for (var i = 0; i < players.Length; i++)
                if (!_session.GetPlayer(i).IsGameOver) players[i].Present(_session.GetPlayer(i));

            if (!IsRunning && Time.time - _gameOverTime >= restartDelay && AnyRestartPressed())
                SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        public bool IsInsidePlayfield(Vector2 position, float margin) =>
            Playfield.Inset(-margin).Contains(position.ToNumerics());

        // ---- Events reported by entities ------------------------------------------------------------------

        public void OnEnemyDestroyed(EnemyView enemy, int playerIndex)
        {
            _session.ReportKill(playerIndex, enemy.Points);
            TryDropPickup(DropSource.Enemy, playerIndex, enemy.Position);
            enemy.Despawn();
        }

        public void OnMeteorDestroyed(MeteorView meteor, int playerIndex)
        {
            var definition = meteor.Definition;
            _session.ReportKill(playerIndex, meteor.Points);
            TryDropPickup(MeteorRules.DropSourceFor(definition.Splitting), playerIndex, meteor.Position);

            if (definition.Splitting && definition.Fragment != null && MeteorRules.TrySplit(definition.Size, out _))
            {
                var parentVelocity = meteor.Velocity.sqrMagnitude > 1e-6f ? meteor.Velocity : Vector2.left;
                for (var i = 0; i < MeteorRules.FragmentsPerSplit; i++)
                {
                    var direction = NVector2.Normalize(MeteorRules.FragmentVelocity(parentVelocity.ToNumerics(), i));
                    SpawnMeteor(definition.Fragment, meteor.Position, direction.ToUnity() * RandomSpeed(definition.Fragment));
                }
            }

            meteor.Despawn();
        }

        /// <summary>Returns true when the projectile was used up (hit or absorbed); false lets it fly on.</summary>
        public bool OnPlayerHitByProjectile(PlayerShipView ship, Projectile projectile)
        {
            if (!IsRunning) return false;

            var outcome = _session.ReportPlayerHit(ship.PlayerIndex, projectile.Damage);
            return outcome != HitOutcome.Ignored;
        }

        public void OnPlayerRammedEnemy(PlayerShipView ship, EnemyView enemy)
        {
            if (!IsRunning || !enemy.IsAlive) return;

            var result = _session.ReportPlayerRam(ship.PlayerIndex, false, enemy.ContactDamage, enemy.Points);
            if (result.EnemyDestroyed)
            {
                TryDropPickup(DropSource.Enemy, ship.PlayerIndex, enemy.Position);
                enemy.Despawn();
            }
        }

        /// <summary>A rammed meteor is destroyed without splitting (provisional rule), so fragments cannot chain-hit the ship.</summary>
        public void OnPlayerRammedMeteor(PlayerShipView ship, MeteorView meteor)
        {
            if (!IsRunning || !meteor.IsAlive) return;

            var definition = meteor.Definition;
            var result = _session.ReportPlayerRam(ship.PlayerIndex, false, definition.ContactDamage, meteor.Points);
            if (result.EnemyDestroyed)
            {
                TryDropPickup(MeteorRules.DropSourceFor(definition.Splitting), ship.PlayerIndex, meteor.Position);
                meteor.Despawn();
            }
        }

        public void OnPickupCollected(PlayerShipView ship, PickupView pickup)
        {
            if (!IsRunning || !pickup.IsActive) return;

            _session.ReportPickupCollected(ship.PlayerIndex, pickup.Type);
            pickup.Despawn();
        }

        /// <summary>Fires an enemy bullet at the nearest player still in the game.</summary>
        public void FireEnemyProjectile(Vector2 origin, float speed, float damage)
        {
            if (!IsRunning) return;

            var direction = EnemyMotion.AimAt(origin.ToNumerics(), NearestPlayerPosition(origin).ToNumerics(),
                EnemyFallbackAim).ToUnity();
            _enemyProjectiles.Get().Launch(this, _enemyProjectiles, origin, direction, speed, damage, false, -1);
        }

        // ---- Internals ---------------------------------------------------------------------------------------

        void UpdatePlayfield()
        {
            var cameraPosition = worldCamera.transform.position;
            Playfield = Playfield.FromCamera(cameraPosition.ToNumerics(), worldCamera.orthographicSize, worldCamera.aspect);
        }

        /// <summary>
        /// Hides ships whose game is over and records when the whole run ended. Called only from FixedUpdate, never
        /// from trigger callbacks, so ships are not deactivated while physics is still reporting their contacts.
        /// </summary>
        void CheckGameOver()
        {
            for (var i = 0; i < players.Length; i++)
                if (_session.GetPlayer(i).IsGameOver && players[i].gameObject.activeSelf)
                    players[i].SetAlive(false);

            if (_gameOverTime < 0f && _session.IsGameOver) _gameOverTime = Time.time;
        }

        bool AnyRestartPressed()
        {
            foreach (var input in inputs)
                if (input.RestartPressed) return true;
            return false;
        }

        Vector2 NearestPlayerPosition(Vector2 from)
        {
            var best = from + Vector2.left;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < players.Length; i++)
            {
                if (_session.GetPlayer(i).IsGameOver) continue;
                var distance = (players[i].Position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = players[i].Position;
                }
            }

            return best;
        }

        void SpawnPlayerShot(ShotSpec shot)
        {
            var direction = shot.Direction.ToUnity();
            var position = players[shot.PlayerIndex].MuzzleAt(_shipTargets[shot.PlayerIndex], direction) +
                           shot.Offset.ToUnity();
            _playerProjectiles.Get().Launch(this, _playerProjectiles, position, direction,
                playerDefinition.ProjectileSpeed, playerDefinition.ProjectileDamage, shot.Piercing, shot.PlayerIndex);
        }

        void Spawn(SpawnRequest request)
        {
            var entry = spawnTable[request.EntryIndex];
            var position = new Vector2(Playfield.MaxX + spawnEdgeMargin,
                Playfield.Inset(_spawnMargins[request.EntryIndex]).YAt(request.NormalisedY));

            if (entry.enemyPrefab != null)
            {
                _enemyPools[entry.enemyPrefab].Get()
                    .Init(this, _settings, position, _enemyReleasers[entry.enemyPrefab]);
            }
            else
            {
                var definition = entry.meteor;
                var vertical = (_random.NextFloat() * 2f - 1f) * definition.MaxVerticalSpeed * _settings.EnemySpeedMultiplier;
                SpawnMeteor(definition, position, new Vector2(-RandomSpeed(definition), vertical));
            }
        }

        float RandomSpeed(MeteorDefinition definition) =>
            Mathf.Lerp(definition.MinSpeed, definition.MaxSpeed, _random.NextFloat()) * _settings.EnemySpeedMultiplier;

        void SpawnMeteor(MeteorDefinition definition, Vector2 position, Vector2 velocity)
        {
            var spin = (_random.NextFloat() * 2f - 1f) * definition.MaxSpinDegrees;
            _meteors.Get().Init(this, definition, position, velocity, spin, _releaseMeteor);
        }

        void TryDropPickup(DropSource source, int playerIndex, Vector2 position)
        {
            if (_session.TryRollDrop(source, playerIndex, out var type))
                _pickups.Get().Init(this, type, pickupDriftSpeed, position, _releasePickup);
        }

        /// <summary>Wave-flying enemies need a wider margin so their whole path stays on screen.</summary>
        float SpawnMarginFor(SpawnEntry entry)
        {
            if (entry.enemyPrefab == null || entry.enemyPrefab.Definition.Pattern != MotionPattern.SineWave)
                return spawnEdgeMargin;
            return Mathf.Max(spawnEdgeMargin, entry.enemyPrefab.Definition.WaveAmplitude + EnemyHalfExtent);
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
            Require(enemyProjectilePrefab, nameof(enemyProjectilePrefab));
            Require(meteorPrefab, nameof(meteorPrefab));
            Require(pickupPrefab, nameof(pickupPrefab));

            if (worldCamera != null && !worldCamera.orthographic) Fail("the world camera must be orthographic.");

            if (players.Length < 1 || players.Length > 4) Fail("there must be between 1 and 4 players.");
            if (inputs.Length != players.Length) Fail("there must be one input reader per player.");
            for (var i = 0; i < players.Length; i++) Require(players[i], $"{nameof(players)}[{i}]");
            // Readers validate their own actions asset in Awake, which runs after this one.
            for (var i = 0; i < inputs.Length; i++) Require(inputs[i], $"{nameof(inputs)}[{i}]");

            var totalWeight = 0;
            foreach (var entry in spawnTable)
            {
                if (entry.enemyPrefab == null && entry.meteor == null) Fail("a spawn entry has neither an enemy nor a meteor.");
                if (entry.enemyPrefab != null && entry.enemyPrefab.Definition == null)
                    Fail($"enemy prefab '{entry.enemyPrefab.name}' has no definition.");
                totalWeight += entry.weight;
            }

            if (totalWeight <= 0) Fail("the spawn table needs at least one positive weight.");
            return valid;
        }
    }
}
