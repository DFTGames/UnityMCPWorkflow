using UnityEngine;
using UnityEngine.Audio;
using YASS.Core;

namespace YASS.Feedback
{
    /// <summary>Which piece of music is playing (GDD "Audio Direction", Music).</summary>
    public enum Track
    {
        None = 0,
        Menu,
        Level,
        Boss
    }

    /// <summary>
    /// Plays the game's music, crossfading between tracks on scene and boss transitions (GDD "Audio Direction").
    /// Two sources take it in turns: the outgoing one fades down while the incoming one fades up, so a change
    /// never cuts.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioMixerGroup output;

        [SerializeField, Tooltip("Started when the scene loads; None leaves the music alone.")]
        Track playOnStart = Track.None;

        [SerializeField] AudioClip menuTheme;
        [SerializeField] AudioClip levelTheme;
        [SerializeField] AudioClip bossTheme;

        [SerializeField, Min(0f), Tooltip("Seconds to crossfade between tracks.")]
        float crossfadeSeconds = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Music level before the Music mixer group is applied.")]
        float trim = 0.8f;

        AudioSource[] _sources;
        int _current;
        float _fade = 1f;

        public static MusicPlayer Instance { get; private set; }

        /// <summary>What is playing, or fading in.</summary>
        public Track Playing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;

            _sources = new AudioSource[2];
            for (var i = 0; i < _sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;          // 30 second clips, looped (GDD "Audio Direction")
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = output;
                source.volume = 0f;
                _sources[i] = source;
            }
        }

        void Start()
        {
            if (playOnStart != Track.None) Play(playOnStart);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_fade >= 1f) return;

            // Unscaled: the music carries on through a pause, which is what stops a pause feeling like a crash.
            _fade = Mathf.Min(1f, _fade + (crossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / crossfadeSeconds));

            _sources[_current].volume = trim * _fade;
            var previous = _sources[1 - _current];
            previous.volume = trim * (1f - _fade);

            if (_fade >= 1f && previous.isPlaying) previous.Stop();
        }

        /// <summary>Crossfades to a track. Asking for the one already playing does nothing.</summary>
        public void Play(Track track)
        {
            if (track == Playing) return;

            var clip = ClipFor(track);
            if (clip == null)
            {
                if (track != Track.None) Debug.LogWarning($"{nameof(MusicPlayer)}: no clip for {track}.", this);
                return;
            }

            Playing = track;
            _current = 1 - _current;
            _fade = 0f;

            var next = _sources[_current];
            next.clip = clip;
            next.volume = 0f;
            next.Play();
        }

        public static void PlayIfPresent(Track track)
        {
            if (Instance != null) Instance.Play(track);
        }

        AudioClip ClipFor(Track track)
        {
            switch (track)
            {
                case Track.Menu: return menuTheme;
                case Track.Level: return levelTheme;
                case Track.Boss: return bossTheme;
                default: return null;
            }
        }
    }
}
