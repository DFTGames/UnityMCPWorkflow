using UnityEngine;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Settings screen (GDD "UI Flow and Screens", Settings): music and effects volume, screen shake, and
    /// fullscreen on desktop. Every change is saved at once, so leaving by any route keeps it.
    /// </summary>
    public sealed class SettingsMenu : MonoBehaviour
    {
        [SerializeField] Slider musicVolume;
        [SerializeField] Slider sfxVolume;
        [SerializeField] Toggle screenShake;
        [SerializeField] Toggle fullscreen;

        [SerializeField, Tooltip("The fullscreen row, hidden where the platform owns the window.")]
        GameObject fullscreenRow;

        // While the widgets are being filled in from the saved settings, their callbacks must not write back.
        bool _loading;

        /// <summary>Read at the point of use: tests swap the service, so a cached reference would go stale.</summary>
        static SettingsService Settings => GameFlow.Settings;

        void Awake()
        {
            if (fullscreenRow != null) fullscreenRow.SetActive(GameFlow.SupportsFullscreen);

            if (musicVolume != null) musicVolume.onValueChanged.AddListener(OnMusicVolume);
            if (sfxVolume != null) sfxVolume.onValueChanged.AddListener(OnSfxVolume);
            if (screenShake != null) screenShake.onValueChanged.AddListener(OnScreenShake);
            if (fullscreen != null) fullscreen.onValueChanged.AddListener(OnFullscreen);
        }

        void OnEnable() => Load(Settings.Settings);

        /// <summary>Dragging a slider writes the value at once but not to disk; leaving the screen does that.</summary>
        void OnDisable() => Settings.Flush();

        void OnDestroy()
        {
            if (musicVolume != null) musicVolume.onValueChanged.RemoveListener(OnMusicVolume);
            if (sfxVolume != null) sfxVolume.onValueChanged.RemoveListener(OnSfxVolume);
            if (screenShake != null) screenShake.onValueChanged.RemoveListener(OnScreenShake);
            if (fullscreen != null) fullscreen.onValueChanged.RemoveListener(OnFullscreen);
        }

        void Load(GameSettings settings)
        {
            _loading = true;
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(settings.MusicVolume);
            if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(settings.SfxVolume);
            if (screenShake != null) screenShake.SetIsOnWithoutNotify(settings.ScreenShake);
            if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(settings.Fullscreen);
            _loading = false;
        }

        void OnMusicVolume(float value)
        {
            if (!_loading) Settings.SetMusicVolume(value);
        }

        void OnSfxVolume(float value)
        {
            if (!_loading) Settings.SetSfxVolume(value);
        }

        void OnScreenShake(bool value)
        {
            if (!_loading) Settings.SetScreenShake(value);
        }

        void OnFullscreen(bool value)
        {
            if (!_loading) Settings.SetFullscreen(value);
        }
    }
}
