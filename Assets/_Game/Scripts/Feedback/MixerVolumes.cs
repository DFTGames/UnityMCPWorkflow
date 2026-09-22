using UnityEngine;
using UnityEngine.Audio;
using YASS.Core;
using YASS.UI;

namespace YASS.Feedback
{
    /// <summary>
    /// Drives the mixer's Music and SFX groups from the player's settings (GDD "Audio Direction", Mixing).
    /// Sliders are linear but loudness is not, so the values go through <see cref="AudioRules.ToDecibels"/>.
    /// </summary>
    /// <remarks>
    /// The mixer is not simply written once. The first write of a session is made as the first scene wakes, and
    /// the audio system throws it away: the game would then start at full volume and stay there until the player
    /// opened the Settings screen and nudged a slider. So the two groups are read back every frame and written
    /// again whenever they do not hold what was asked for. Nothing else writes these parameters, so this
    /// component owns them outright.
    ///
    /// Latching once the groups agree would not be safe: the write is thrown away at a moment of the audio
    /// system's choosing, so a first read-back can agree and be overwritten afterwards. That leaves a window,
    /// a frame or two long, where the groups are at full volume; music is inaudible through it because a track
    /// starts at zero and fades in over a second and a half.
    ///
    /// The execution order is for <see cref="OnEnable"/>, so the volumes are set before anything plays; it buys
    /// the read-back nothing.
    /// </remarks>
    [DefaultExecutionOrder(-200)]
    public sealed class MixerVolumes : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;

        SettingsService _settings;
        float _musicDecibels;
        float _sfxDecibels;
        bool _hasTarget;
        bool _reportedMissing;

        void OnEnable()
        {
            if (mixer == null)
            {
                Debug.LogError($"{nameof(MixerVolumes)}: no mixer assigned; the volume sliders will do nothing.", this);
                return;
            }

            _settings = GameFlow.Settings;
            _settings.Changed += Apply;
            Apply(_settings.Settings);
        }

        void OnDisable()
        {
            if (_settings != null) _settings.Changed -= Apply;
        }

        void LateUpdate()
        {
            if (!_hasTarget || mixer == null) return;
            if (Holds(AudioRules.MusicVolumeParameter, _musicDecibels)
                && Holds(AudioRules.SfxVolumeParameter, _sfxDecibels)) return;

            Write();
        }

        void Apply(GameSettings settings)
        {
            if (mixer == null) return;

            _musicDecibels = AudioRules.ToDecibels(settings.MusicVolume);
            _sfxDecibels = AudioRules.ToDecibels(settings.SfxVolume);
            _hasTarget = true;
            Write();
        }

        void Write()
        {
            mixer.SetFloat(AudioRules.MusicVolumeParameter, _musicDecibels);
            mixer.SetFloat(AudioRules.SfxVolumeParameter, _sfxDecibels);
        }

        /// <summary>
        /// Whether a group is at the level it was asked for. A parameter the mixer does not have counts as
        /// held: there is nothing to enforce, and without this the component would write to a name that does
        /// not exist on every frame for the life of the process, saying nothing.
        /// </summary>
        bool Holds(string parameter, float decibels)
        {
            if (mixer.GetFloat(parameter, out var actual)) return AudioRules.IsSetTo(decibels, actual);

            if (!_reportedMissing)
            {
                _reportedMissing = true;
                Debug.LogError($"{nameof(MixerVolumes)}: the mixer has no exposed parameter '{parameter}', " +
                               "so that volume slider will do nothing.", this);
            }

            return true;
        }

        /// <summary>Test seam: the level a group is currently set to, in decibels.</summary>
        internal bool TryGetDecibels(string parameter, out float decibels)
        {
            decibels = 0f;
            return mixer != null && mixer.GetFloat(parameter, out decibels);
        }

        /// <summary>Test seam: writes a group's level directly, behind this component's back.</summary>
        internal void SetDecibels(string parameter, float decibels)
        {
            if (mixer != null) mixer.SetFloat(parameter, decibels);
        }
    }
}
