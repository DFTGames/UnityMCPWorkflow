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
    [DefaultExecutionOrder(-200)]
    public sealed class MixerVolumes : MonoBehaviour
    {
        [SerializeField] AudioMixer mixer;

        SettingsService _settings;

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

        void Apply(GameSettings settings)
        {
            if (mixer == null) return;

            mixer.SetFloat(AudioRules.MusicVolumeParameter, AudioRules.ToDecibels(settings.MusicVolume));
            mixer.SetFloat(AudioRules.SfxVolumeParameter, AudioRules.ToDecibels(settings.SfxVolume));
        }

        /// <summary>The level a group is currently set to, in decibels. For tests.</summary>
        internal bool TryGetDecibels(string parameter, out float decibels)
        {
            decibels = 0f;
            return mixer != null && mixer.GetFloat(parameter, out decibels);
        }
    }
}
