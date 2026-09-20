using System;
using UnityEngine;
using YASS.Core;

namespace YASS.Feedback
{
    /// <summary>
    /// Which clip each sound effect plays, and how loud (GDD "Audio Direction"). Built by
    /// <c>Tools/YASS/Build Sound Bank</c>, which matches the clips in <c>Assets/_Game/Audio/SFX/</c> to the
    /// <see cref="Sfx"/> entries by name, so a new sound only has to be generated with the right file name.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "YASS/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Sfx Sfx;
            public AudioClip Clip;

            [Range(0f, 1f), Tooltip("Per-sound trim, so one clip does not drown the others.")]
            public float Volume;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;

        /// <summary>The clip for a sound, or null when it has none yet.</summary>
        public bool TryGet(Sfx sfx, out AudioClip clip, out float volume)
        {
            foreach (var entry in entries)
            {
                if (entry.Sfx != sfx || entry.Clip == null) continue;

                clip = entry.Clip;
                volume = entry.Volume;
                return true;
            }

            clip = null;
            volume = 0f;
            return false;
        }
    }
}
