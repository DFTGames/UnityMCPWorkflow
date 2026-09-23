using System;
using System.Collections.Generic;

namespace YASS.Core
{
    /// <summary>
    /// Which assets should be in memory right now, and what has to change to get there. Keys are opaque to
    /// this class: the Unity layer uses Resources paths.
    /// </summary>
    /// <remarks>
    /// The game holds only what it is using and frees the rest as soon as it stops using it, so something has
    /// to work out the difference between what is held and what is wanted. That is a set comparison with no
    /// engine in it, which is why it lives here and is tested directly rather than through a scene.
    ///
    /// Loading is not instant, so the caller tells this class when a load or a free has actually happened
    /// (<see cref="NoteLoaded"/>, <see cref="NoteFreed"/>) rather than this class assuming its own plan worked.
    /// </remarks>
    public sealed class AssetResidency
    {
        readonly HashSet<string> _resident = new HashSet<string>(StringComparer.Ordinal);

        public int Count => _resident.Count;

        public bool Holds(string key) => key != null && _resident.Contains(key);

        /// <summary>
        /// Works out how to hold exactly <paramref name="required"/>: what is missing and must be loaded, and
        /// what is held but no longer wanted and can be freed. Neither list includes what is already right, so
        /// asking for the same set twice is free and an asset shared by two levels is never reloaded.
        /// </summary>
        public void Plan(IReadOnlyCollection<string> required, List<string> toLoad, List<string> toFree)
        {
            if (toLoad == null) throw new ArgumentNullException(nameof(toLoad));
            if (toFree == null) throw new ArgumentNullException(nameof(toFree));

            toLoad.Clear();
            toFree.Clear();

            if (required != null)
                foreach (var key in required)
                    if (!string.IsNullOrEmpty(key) && !_resident.Contains(key) && !toLoad.Contains(key))
                        toLoad.Add(key);

            foreach (var key in _resident)
                if (required == null || !Contains(required, key))
                    toFree.Add(key);
        }

        /// <summary>Records that a key is now in memory.</summary>
        public void NoteLoaded(string key)
        {
            if (!string.IsNullOrEmpty(key)) _resident.Add(key);
        }

        /// <summary>Records that a key has been freed.</summary>
        public void NoteFreed(string key)
        {
            if (!string.IsNullOrEmpty(key)) _resident.Remove(key);
        }

        /// <summary>Forgets everything, for a teardown that frees the lot in one go.</summary>
        public void Clear() => _resident.Clear();

        static bool Contains(IReadOnlyCollection<string> keys, string key)
        {
            // IReadOnlyCollection has no Contains, and the sets here are a handful of entries.
            foreach (var candidate in keys)
                if (string.Equals(candidate, key, StringComparison.Ordinal)) return true;

            return false;
        }
    }
}
