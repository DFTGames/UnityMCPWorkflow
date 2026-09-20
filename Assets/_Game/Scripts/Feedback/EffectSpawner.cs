using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace YASS.Feedback
{
    /// <summary>
    /// Spawns the game's particle effects from pools (GDD "Art Direction": all effects are pooled particle
    /// systems), so explosions during a busy wave do not allocate.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class EffectSpawner : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public Effect Effect;
            public PooledEffect Prefab;
        }

        const int PoolCapacity = 8;
        const int PoolMaxSize = 64;

        /// <summary>Instances built up front, so the first explosion does not instantiate mid-fight.</summary>
        const int Prewarm = 4;

        [SerializeField] Entry[] effects = Array.Empty<Entry>();
        [SerializeField, Tooltip("Parent for spawned effects; this object when left empty.")] Transform root;

        readonly Dictionary<Effect, ObjectPool<PooledEffect>> _pools = new Dictionary<Effect, ObjectPool<PooledEffect>>();
        readonly Dictionary<Effect, Action<PooledEffect>> _release = new Dictionary<Effect, Action<PooledEffect>>();

        public static EffectSpawner Instance { get; private set; }

        // Domain reloading is off in this project, so a stale instance would otherwise survive into the
        // next play session (see GameFlow, which does the same).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;

            foreach (var entry in effects)
            {
                if (entry.Prefab == null || _pools.ContainsKey(entry.Effect)) continue;

                var prefab = entry.Prefab;
                ObjectPool<PooledEffect> pool = null;
                pool = new ObjectPool<PooledEffect>(
                    () => Instantiate(prefab, root != null ? root : transform),
                    e => e.gameObject.SetActive(true),
                    e => e.gameObject.SetActive(false),
                    e => Destroy(e.gameObject),
                    collectionCheck: false, PoolCapacity, PoolMaxSize);

                _pools[entry.Effect] = pool;
                _release[entry.Effect] = e => pool.Release(e); // built once, not per spawn

                Prime(pool);
            }
        }

        /// <summary>Fills a pool by cycling instances through it, the same way the runner prewarms its pools.</summary>
        static void Prime(ObjectPool<PooledEffect> pool)
        {
            var warmed = new PooledEffect[Prewarm];
            for (var i = 0; i < Prewarm; i++) warmed[i] = pool.Get();
            foreach (var effect in warmed) pool.Release(effect);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            foreach (var pool in _pools.Values) pool.Dispose();
            _pools.Clear();
        }

        public void Spawn(Effect effect, Vector2 position)
        {
            if (!_pools.TryGetValue(effect, out var pool)) return;

            var instance = pool.Get();
            instance.Play(position, _release[effect]);
        }

        public static void SpawnIfPresent(Effect effect, Vector2 position)
        {
            if (Instance != null) Instance.Spawn(effect, position);
        }
    }
}
