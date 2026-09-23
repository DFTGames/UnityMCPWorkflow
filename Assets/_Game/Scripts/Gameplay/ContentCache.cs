using System.Collections.Generic;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// Loads the assets the game is using from <c>Resources</c> and frees them the moment it stops using them.
    /// <see cref="AssetResidency"/> decides what that means; this does it.
    /// </summary>
    /// <remarks>
    /// The alternative is a direct reference from a scene or a ScriptableObject, which hands the decision to
    /// Unity: the whole dependency graph of whatever the scene can reach is packed with it, and when it is
    /// loaded depends on the platform. One scene now plays all eight levels and Endless, so that graph is the
    /// entire game. Paths instead of references means a level holds its own sky and nothing else, and gives it
    /// back when the level ends.
    ///
    /// Loading is started in the background and finished on demand: <see cref="Require{T}"/> only asks, and
    /// <see cref="TryGet{T}"/> returns what has arrived, while <see cref="Get{T}"/> waits for one that has not.
    /// A caller that asks early enough (the sky, which knows a dissolve is coming long before it starts) never
    /// blocks; one that needs an asset the same frame it asks for it pays for the load, which is right when a
    /// level is starting and there is nothing to draw yet anyway.
    ///
    /// **One owner.** <see cref="Require{T}"/> means "hold exactly this and nothing else", so a second owner
    /// calling it would free the first one's assets. The runner's cache belongs to the sky.
    ///
    /// Keys are Resources paths: no extension, relative to a <c>Resources</c> folder ("Backgrounds/Nebula").
    /// </remarks>
    public sealed class ContentCache
    {
        readonly Dictionary<string, Object> _loaded = new Dictionary<string, Object>();
        readonly Dictionary<string, ResourceRequest> _loading = new Dictionary<string, ResourceRequest>();
        readonly HashSet<string> _failed = new HashSet<string>();
        readonly AssetResidency _residency = new AssetResidency();
        readonly List<string> _toLoad = new List<string>();
        readonly List<string> _toFree = new List<string>();

        /// <summary>How many assets are held, counting those still on their way in.</summary>
        public int Count => _residency.Count + _loading.Count;

        /// <summary>How many are loaded and usable now.</summary>
        public int LoadedCount => _residency.Count;

        /// <summary>
        /// Asks for exactly <paramref name="paths"/> and nothing else: frees what is no longer wanted now, and
        /// starts loading what is missing. Nothing blocks here.
        /// </summary>
        public void Require<T>(IReadOnlyCollection<string> paths) where T : Object
        {
            _residency.Plan(paths, _toLoad, _toFree);

            foreach (var path in _toFree) Free(path);
            foreach (var path in _toLoad) BeginLoad<T>(path);

            // A load still in flight for something no longer wanted: let it land, then drop it.
            if (_loading.Count > 0) DiscardUnwantedLoads(paths);
        }

        /// <summary>What has arrived, or null if it is still on its way. Never blocks.</summary>
        public T TryGet<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!_loaded.TryGetValue(path, out var asset)) return null;

            // Unity's fake null: an unloaded asset is not a null reference, but it compares equal to one.
            return asset == null ? null : asset as T;
        }

        /// <summary>
        /// What was asked for, waiting for it if the background load has not finished. For the caller that
        /// needs it this frame, such as a level painting its backdrop as it starts.
        /// </summary>
        public T Get<T>(string path) where T : Object
        {
            var arrived = TryGet<T>(path);
            if (arrived != null) return arrived;
            if (string.IsNullOrEmpty(path) || _failed.Contains(path)) return null;

            // Blocking, and deliberately: the caller has said it cannot carry on without this.
            _loading.Remove(path);
            Store(path, Resources.Load<T>(path));

            return TryGet<T>(path);
        }

        /// <summary>Frees everything, for leaving the level behind entirely.</summary>
        public void Clear()
        {
            _loading.Clear();
            _failed.Clear();
            Require<Object>(null);
        }

        void BeginLoad<T>(string path) where T : Object
        {
            if (_loading.ContainsKey(path) || _failed.Contains(path)) return;

            // Typed: a texture imported as a sprite has both in the file, and loading it as Object returns the
            // texture, so asking for Object and casting to Sprite silently yields null.
            var request = Resources.LoadAsync<T>(path);
            _loading[path] = request;
            request.completed += _ =>
            {
                if (!_loading.Remove(path)) return; // no longer wanted, or already forced through Get

                Store(path, request.asset);
            };
        }

        void Store(string path, Object asset)
        {
            if (asset == null)
            {
                // Remembered, or a missing path would be retried and logged every time the set changes.
                _failed.Add(path);
                Debug.LogError($"{nameof(ContentCache)}: nothing loadable at Resources/{path}.");
                return;
            }

            _loaded[path] = asset;
            _residency.NoteLoaded(path);
        }

        void DiscardUnwantedLoads(IReadOnlyCollection<string> wanted)
        {
            _toLoad.Clear();
            foreach (var path in _loading.Keys)
                if (wanted == null || !Contains(wanted, path)) _toLoad.Add(path);

            foreach (var path in _toLoad) _loading.Remove(path);
        }

        void Free(string path)
        {
            _residency.NoteFreed(path);

            if (!_loaded.TryGetValue(path, out var asset)) return;

            _loaded.Remove(path);
            if (asset == null) return;

            // A sprite is a thin wrapper: unloading it leaves its texture resident, and the texture is where
            // the megabytes are. Only for a standalone sprite: a packed one's texture is the atlas page, which
            // everything else in the game is drawing from.
            if (asset is Sprite sprite)
            {
                if (sprite.packed)
                {
                    Debug.LogError($"{nameof(ContentCache)}: {path} is packed into an atlas, so its texture is " +
                                   "shared and cannot be freed on its own. Keep loaded sprites out of the atlas.");
                    return;
                }

                if (sprite.texture != null && !IsTextureStillWanted(sprite.texture))
                    Resources.UnloadAsset(sprite.texture);
            }

            // Resources.UnloadAsset frees exactly this one asset, which is the point of holding them by name.
            // It refuses whole GameObjects, which are left to the usual unused-asset sweep.
            if (!(asset is GameObject) && !(asset is Component)) Resources.UnloadAsset(asset);
        }

        /// <summary>
        /// Whether another held asset is drawn from this texture, which two paths for the same file would be.
        /// Freeing it then would pull it out from under whatever is still using it.
        /// </summary>
        bool IsTextureStillWanted(Texture texture)
        {
            foreach (var held in _loaded.Values)
                if (held is Sprite other && other != null && other.texture == texture) return true;

            return false;
        }

        static bool Contains(IReadOnlyCollection<string> keys, string key)
        {
            foreach (var candidate in keys)
                if (string.Equals(candidate, key, System.StringComparison.Ordinal)) return true;

            return false;
        }
    }
}
