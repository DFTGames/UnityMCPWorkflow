using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Makes the saved settings take effect. Present in every scene, so settings apply whether the player changed
    /// them here or two scenes ago.
    /// </summary>
    /// <remarks>
    /// Volumes drive <see cref="AudioListener.volume"/> for now; M3 replaces this with separate music and effects
    /// mixer groups (GDD "Audio Direction"). Screen shake is read by the effects that will use it in M3.
    /// </remarks>
    public sealed class SettingsApplier : MonoBehaviour
    {
        static bool _fullscreenApplied;

        SettingsService _settings;

        void OnEnable()
        {
            _settings = GameFlow.Settings;

            // The saved preference wins once per session; after that the window is the player's, who may have
            // used Alt+Enter or the window chrome, so adopt what it actually is instead of snapping it back.
            if (GameFlow.SupportsFullscreen)
            {
                if (_fullscreenApplied) _settings.SetFullscreen(Screen.fullScreen);
                else _fullscreenApplied = true;
            }

            _settings.Changed += Apply;
            _settings.Apply();
        }

        void OnDisable()
        {
            if (_settings == null) return;

            _settings.Changed -= Apply;
            _settings.Flush();
        }

        // Quitting or being backgrounded (mobile, where the process may not come back) must not lose a change.
        void OnApplicationQuit() => _settings?.Flush();

        void OnApplicationPause(bool paused)
        {
            if (paused) _settings?.Flush();
        }

        static void Apply(GameSettings settings)
        {
            // The effects volume belongs to the audio director, which applies it per voice; scaling the listener
            // as well would apply it twice, and would let the (still silent) music slider change effect loudness.
            AudioListener.volume = 1f;

            if (GameFlow.SupportsFullscreen && Screen.fullScreen != settings.Fullscreen)
                Screen.fullScreen = settings.Fullscreen;
        }
    }
}
