using UnityEngine;
using YASS.Core;
using YASS.UI;

namespace YASS.Feedback
{
    /// <summary>
    /// Shakes the camera when the player is hit and when a boss dies (GDD "Art Direction", Visual effects).
    /// Honours the Screen shake setting, which some players need turned off.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CameraShaker : MonoBehaviour
    {
        [SerializeField, Tooltip("The camera to shake; this object's camera when left empty.")]
        Transform target;

        Vector3 _restPosition;
        float _trauma;
        bool _enabled = true;
        SettingsService _settings;

        public static CameraShaker Instance { get; private set; }

        /// <summary>Current shake strength, 0 to 1. Exposed for tests.</summary>
        public float Trauma => _trauma;

        /// <summary>
        /// Where the camera sits when it is not being shaken. Gameplay works from this: the playfield is derived
        /// from the camera, and a shaking playfield would drag ships along its edges and flare their engines.
        /// </summary>
        public Vector3 RestPosition => _restPosition;

        /// <summary>The steady position of a camera, whether or not anything is shaking it.</summary>
        public static Vector3 SteadyPosition(Camera camera) =>
            Instance != null && Instance.Target == camera.transform
                ? Instance.transform.parent == null ? Instance._restPosition : camera.transform.parent.TransformPoint(Instance._restPosition)
                : camera.transform.position;

        internal Transform Target => target != null ? target : transform;

        // Domain reloading is off in this project, so a stale instance would otherwise survive into the
        // next play session (see GameFlow, which does the same).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            _restPosition = Target.localPosition;
        }

        void OnEnable()
        {
            _settings = GameFlow.Settings;
            _settings.Changed += ApplySettings;
            ApplySettings(_settings.Settings);
        }

        void OnDisable()
        {
            if (_settings != null) _settings.Changed -= ApplySettings;

            // Never leave the camera parked mid-shake.
            _trauma = 0f;
            Target.localPosition = _restPosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Leave the camera where it belongs, not wherever the last shake happened to be.
            Target.localPosition = _restPosition;
        }

        void LateUpdate()
        {
            if (_trauma <= 0f) return;

            // Unscaled: a boss dying can end the run, and the shake should finish even as the game stops.
            _trauma = ScreenShake.Decay(_trauma, Time.unscaledDeltaTime);

            var offset = ScreenShake.Offset(_trauma, Time.unscaledTime);
            Target.localPosition = _restPosition + new Vector3(offset.X, offset.Y, 0f);

            if (_trauma <= 0f) Target.localPosition = _restPosition;
        }

        public void Shake(float trauma)
        {
            if (!_enabled) return;

            _trauma = ScreenShake.Add(_trauma, trauma);
        }

        public static void ShakeIfPresent(float trauma)
        {
            if (Instance != null) Instance.Shake(trauma);
        }

        void ApplySettings(GameSettings settings)
        {
            _enabled = settings.ScreenShake;
            if (_enabled) return;

            _trauma = 0f;
            Target.localPosition = _restPosition;
        }
    }
}
