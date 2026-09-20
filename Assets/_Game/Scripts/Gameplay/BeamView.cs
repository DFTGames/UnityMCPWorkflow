using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>
    /// The Sniper's telegraph and shot: a thin line that brightens while it warns, then a wide bright beam for the
    /// moment it fires (GDD "Enemies and Hazards": the warning is the whole fight). Purely presentation; whether
    /// anything is hit is decided by <see cref="YASS.Core.SniperShot"/> in the rules layer.
    /// </summary>
    public sealed class BeamView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer lineRenderer;
        [SerializeField, Tooltip("How wide the warning line is, as a fraction of the beam's width.")]
        [Range(0.05f, 1f)] float warningWidthFraction = 0.25f;
        [SerializeField] Color warningColour = new Color(1f, 0.35f, 0.3f, 0.35f);
        [SerializeField] Color beamColour = new Color(1f, 0.85f, 0.6f, 1f);

        Vector3 _parentScale = Vector3.one;

        void Reset() => lineRenderer = GetComponentInChildren<SpriteRenderer>();

        void Awake()
        {
            // The ship's scale is fixed for its lifetime; decomposing its matrix every step would be waste.
            if (transform.parent != null) _parentScale = transform.parent.lossyScale;
            Hide();
        }

        /// <summary>
        /// Draws the line from <paramref name="origin"/> along <paramref name="aim"/>. While warning it is thin and
        /// fades up with <paramref name="warningProgress"/>, so the closer the shot the harder it is to miss.
        /// </summary>
        public void Show(Vector2 origin, Vector2 aim, float length, float halfWidth, bool firing, float warningProgress)
        {
            if (lineRenderer == null || aim.sqrMagnitude < 1e-6f) return;

            var width = firing ? halfWidth * 2f : halfWidth * 2f * warningWidthFraction;
            var angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            // The sprite's pivot is its centre, so the line sits half its length along the aim.
            transform.SetPositionAndRotation(origin + aim.normalized * (length * 0.5f), Quaternion.Euler(0f, 0f, angle));
            transform.localScale = LocalScaleFor(length, width);

            var colour = firing ? beamColour : warningColour;
            if (!firing) colour.a *= Mathf.Clamp01(warningProgress);
            lineRenderer.color = colour;
            lineRenderer.enabled = true;
        }

        public void Hide()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
        }

        /// <summary>
        /// Turns a world-space length and width into a local scale. The sprite has its own size in units and the
        /// ship it hangs off is scaled to the size the GDD asks for, so neither may be allowed to stretch the beam.
        /// </summary>
        Vector3 LocalScaleFor(float length, float width)
        {
            var sprite = lineRenderer.sprite;
            var spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;

            return new Vector3(
                Divide(length, spriteSize.x * _parentScale.x),
                Divide(width, spriteSize.y * _parentScale.y),
                1f);
        }

        /// <summary>A degenerate scale collapses the beam rather than drawing one of the wrong length.</summary>
        static float Divide(float value, float divisor) => Mathf.Abs(divisor) > 1e-6f ? value / divisor : 0f;
    }
}
