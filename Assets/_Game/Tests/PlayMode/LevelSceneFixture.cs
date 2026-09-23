using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Gameplay;
using NVector2 = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Shared setup for the tests that play the real level scene: a fresh run with in-memory settings, plus the
    /// handful of helpers they all need. Prefabs are loaded through the AssetDatabase, so these run in the
    /// Editor only.
    ///
    /// One scene plays every level and Endless too, so a test that wants something other than the scene's own
    /// fallback level says so in <see cref="ConfigureRun"/>, before the scene loads and its runner reads the
    /// context.
    /// </summary>
    public abstract class LevelSceneFixture
    {
        protected const string PrefabDir = "Assets/_Game/Prefabs/";

        /// <summary>The one scene the game is played in.</summary>
        protected const string SceneName = "Level";

        /// <summary>
        /// Sets up the run before the scene loads. The default is no run at all, which is a level opened
        /// directly: the runner falls back to the level assigned in the scene.
        /// </summary>
        protected virtual void ConfigureRun() { }

        protected static readonly PlayerCommand Idle = new PlayerCommand(NVector2.Zero, false, NVector2.UnitX);
        protected static readonly PlayerCommand FireForward = new PlayerCommand(NVector2.Zero, true, NVector2.UnitX);

        bool _previousRunInBackground;

        protected GameRunner Runner { get; private set; }
        protected GameSession Session => Runner.Session;
        protected PlayerShip Ship => Session.GetPlayer(0);
        protected PlayerShipView ShipView => Runner.GetPlayerView(0);
        protected Vector2 ShipPosition => ShipView.Position;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Loads prefabs through the AssetDatabase, so runs in the Editor only.");
#endif
            _previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            // The level reads the run context and the scene's SettingsApplier reads the saved settings: start
            // from a clean, in-memory state so these tests neither depend on nor touch either.
            RunContext.Clear();
            ConfigureRun();
            YASS.UI.GameFlow.UseSettings(new SettingsService(new MemorySettingsStore()));
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            Runner = Object.FindAnyObjectByType<GameRunner>();
            Assert.That(Runner, Is.Not.Null, "GameRunner missing from the scene");
            Assert.That(Runner.enabled, Is.True, "GameRunner disabled itself: check its setup errors");
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void RestoreSettings()
        {
            Application.runInBackground = _previousRunInBackground;
            YASS.UI.GameFlow.UseSettings(null);
            RunContext.Clear();
        }

        /// <summary>Settings that live only for the test, so a run never writes to the developer's own prefs.</summary>
        sealed class MemorySettingsStore : ISettingsStore
        {
            public float GetFloat(string key, float fallback) => fallback;
            public void SetFloat(string key, float value) { }
            public bool GetBool(string key, bool fallback) => fallback;
            public void SetBool(string key, bool value) { }
            public string GetString(string key, string fallback) => fallback;
            public void SetString(string key, string value) { }
            public void Save() { }
        }

        // ---- Helpers ----

        /// <summary>Flies the ship right until it has clear space behind it, then stops.</summary>
        protected IEnumerator MoveShipAwayFromTheLeftEdge()
        {
            var target = Runner.Playfield.MinX + 4f;
            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.UnitX, false, NVector2.UnitX));
            yield return WaitUntil(() => ShipPosition.x >= target, 4f, "the ship to fly clear of the left edge");
            Runner.SetCommandOverride(0, Idle);
        }

        protected void AimAt(Vector3 target)
        {
            var aim = ((Vector2)target - ShipPosition).ToNumerics();
            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.Zero, true, aim));
        }

        protected EnemyView SpawnEnemy(string prefabName, Vector2 position) =>
            SpawnEnemy(prefabName, position, null);

        /// <summary>
        /// Spawns an enemy outside the runner's pools. A null <paramref name="release"/> destroys it on despawn;
        /// pass a no-op to keep the instance alive, which is how a pooled enemy is reused.
        /// </summary>
        protected EnemyView SpawnEnemy(string prefabName, Vector2 position, Action<EnemyView> release)
        {
            var enemy = Object.Instantiate(LoadPrefab<EnemyView>(prefabName));
            enemy.Init(Runner, Session.Settings, position, release);
            return enemy;
        }

        protected static int CountLiveEnemies()
        {
            var count = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyView>()) if (enemy.IsAlive) count++;
            return count;
        }

        /// <summary>Waits for <paramref name="condition"/>, failing the test with a clear message on timeout.</summary>
        protected static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string waitingFor)
        {
            var deadline = Time.time + timeoutSeconds;
            while (!condition())
            {
                if (Time.time > deadline) Assert.Fail($"Timed out after {timeoutSeconds} s waiting for {waitingFor}.");
                yield return null;
            }
        }

        protected static T LoadPrefab<T>(string name) where T : Component
        {
            var prefab = LoadAsset<GameObject>(PrefabDir + name + ".prefab");
            var component = prefab.GetComponent<T>();
            Assert.That(component, Is.Not.Null, $"{name} prefab has no {typeof(T).Name}");
            return component;
        }

        protected static T LoadAsset<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Missing asset " + path);
            return asset;
#else
            throw new InvalidOperationException("Editor only");
#endif
        }
    }
}
