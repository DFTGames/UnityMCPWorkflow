using UnityEngine;
using YASS.Core;

namespace YASS.Feedback
{
    /// <summary>
    /// Flashes a sprite white when whatever it belongs to is damaged (GDD "Art Direction", Visual effects), so a
    /// hit on a tough enemy reads even when it survives.
    /// </summary>
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField, Tooltip("Sprites to flash; this object's renderers when left empty.")]
        SpriteRenderer[] renderers;

        [SerializeField, Min(1f), Tooltip("How much brighter the sprite goes at the moment of the hit.")]
        float flashBrightness = 2.4f;

        Color[] _restColours;
        float _elapsed = -1f;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            _restColours = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) _restColours[i] = renderers[i].color;
        }

        /// <summary>Pooled views come back with whatever colour the last flash left; reset it on reuse.</summary>
        void OnDisable() => Restore();

        public void Flash() => _elapsed = 0f;

        void LateUpdate()
        {
            if (_elapsed < 0f) return;

            _elapsed += Time.deltaTime;
            var amount = VisualCues.HitFlash(_elapsed);
            if (amount <= 0f)
            {
                Restore();
                return;
            }

            // Brightened rather than tinted: most of these sprites are already near white, so fading towards
            // white would be invisible. Alpha is left alone so nothing fades in or out.
            var brightness = Mathf.Lerp(1f, flashBrightness, amount);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                var rest = _restColours[i];
                renderers[i].color = new Color(rest.r * brightness, rest.g * brightness, rest.b * brightness, rest.a);
            }
        }

        void Restore()
        {
            _elapsed = -1f;
            if (_restColours == null) return;

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].color = _restColours[i];
            }
        }
    }
}
