using UnityEngine;
using UnityEngine.Audio;
using YASS.Core;

namespace YASS.Feedback
{
    /// <summary>
    /// Plays the game's sound effects through a small pool of voices (GDD "Audio Direction", Mixing). Repeated
    /// sounds are rate-limited and slightly varied in pitch by <see cref="AudioRules"/> so a stream of shots
    /// does not stack into noise, and the effects volume from Settings is applied here.
    /// </summary>
    /// <remarks>
    /// Voices feed the mixer's SFX group, which carries the player's effects volume (see
    /// <see cref="MixerVolumes"/>); each voice applies only its clip's own trim from the bank.
    /// </remarks>
    [DefaultExecutionOrder(-200)] // ready before anything that might play a sound on its first frame
    public sealed class AudioDirector : MonoBehaviour
    {
        static readonly int SfxCount = System.Enum.GetValues(typeof(Sfx)).Length;

        [SerializeField] SoundBank bank;
        [SerializeField, Tooltip("The mixer's SFX group; the effects volume is applied there.")]
        AudioMixerGroup output;

        [SerializeField, Min(1), Tooltip("How many sounds can overlap before the longest-running one is replaced.")]
        int voices = 12;

        AudioSource[] _sources;
        float[] _clipVolume;
        float[] _lastPlayed;
        int[] _playCount;

        /// <summary>The director in the current scene, if any. Sounds are optional: callers check for null.</summary>
        public static AudioDirector Instance { get; private set; }

        /// <summary>Test seam: how many times each sound has actually been played.</summary>
        internal int PlayCount(Sfx sfx) => _playCount[(int)sfx];

        // Domain reloading is off in this project, so a stale instance would otherwise survive into the
        // next play session (see GameFlow, which does the same).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;

            _sources = new AudioSource[voices];
            for (var i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // 2D: the playfield is one screen wide
                source.outputAudioMixerGroup = output;
                _sources[i] = source;
            }

            _clipVolume = new float[voices];
            _lastPlayed = new float[SfxCount];
            _playCount = new int[SfxCount];
            for (var i = 0; i < SfxCount; i++) _lastPlayed[i] = -1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays a sound, unless it played too recently to be worth hearing again.</summary>
        public void Play(Sfx sfx)
        {
            if (sfx == Sfx.None || bank == null) return;

            // Unscaled: menu and results sounds must still play while the game is stopped.
            var now = Time.unscaledTime;
            if (!AudioRules.CanPlay(sfx, _lastPlayed[(int)sfx], now)) return;
            if (!bank.TryGet(sfx, out var clip, out var clipVolume)) return;

            _lastPlayed[(int)sfx] = now;
            var index = _playCount[(int)sfx]++;

            var voice = FreeVoice();
            var source = _sources[voice];

            _clipVolume[voice] = Mathf.Clamp01(clipVolume);
            source.clip = clip;
            source.volume = _clipVolume[voice];
            source.pitch = AudioRules.Pitch(sfx, index);
            source.Play();
        }

        /// <summary>
        /// A free voice, or failing that the one closest to finishing. Round robin (or stealing the oldest)
        /// would cut off exactly the sounds worth keeping: a four-second boss explosion is the oldest voice
        /// playing, while the player's shots are nearly spent.
        /// </summary>
        int FreeVoice()
        {
            var shortestRemaining = float.MaxValue;
            var candidate = 0;

            for (var i = 0; i < _sources.Length; i++)
            {
                var source = _sources[i];
                if (!source.isPlaying) return i;

                var remaining = source.clip == null ? 0f : source.clip.length - source.time;
                if (remaining >= shortestRemaining) continue;

                shortestRemaining = remaining;
                candidate = i;
            }

            return candidate;
        }

        /// <summary>Convenience for code that may run in a scene without a director (tests, tools).</summary>
        public static void PlayIfPresent(Sfx sfx)
        {
            if (Instance != null) Instance.Play(sfx);
        }

    }
}
