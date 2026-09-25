using UnityEngine;
using UnityEngine.UI;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// Draws the menu's drifting starfield as part of the canvas (GDD "Art Direction": star fields).
    /// <see cref="StarfieldDrift"/> in Core decides where the stars are; this only turns them into quads.
    /// </summary>
    /// <remarks>
    /// **A canvas starfield, not a particle system.** The menu canvas is Screen Space Overlay, which draws
    /// on top of everything the camera renders, so a world-space particle system behind it is invisible no
    /// matter how its particles are sized: the scene used to hold one copied from the level, at gameplay
    /// scale and parked off to the side, and nothing of it ever reached the screen. Anything that must
    /// appear between the backdrop and the menu has to be drawn by the canvas itself.
    ///
    /// Unity has no built-in UI particle renderer, so this is one <see cref="MaskableGraphic"/> building a
    /// quad per star. That is cheaper than a particle system would have been anyway: one mesh, one draw
    /// call, no per-particle objects.
    ///
    /// **It wants its own nested Canvas.** The mesh is rebuilt every frame, and a rebuild dirties the
    /// canvas the graphic belongs to: on the menu's canvas that would re-batch every button and label in
    /// the scene on every frame. A Canvas component on this object confines the rebuild to these quads.
    /// </remarks>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StarfieldGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 1200), Tooltip("How many stars. The field is generated once, not emitted.")]
        int starCount = 220;

        [SerializeField, Tooltip("The same seed is always the same sky, so the menu does not reshuffle itself.")]
        int seed = 20261;

        [SerializeField, Tooltip("Size of the brightest star, in canvas units. Others scale down from it.")]
        float starSize = 7.5f;

        [SerializeField, Tooltip("Soft round dot. Without one the stars are square, which reads as dirt.")]
        Texture2D dot;

        StarfieldDrift _field;

        /// <summary>The graphic samples this, which is how the quads come out round rather than square.</summary>
        public override Texture mainTexture => dot != null ? dot : s_WhiteTexture;

        /// <summary>Test seam: the field being drawn.</summary>
        internal StarfieldDrift Field => _field;

        protected override void Awake()
        {
            base.Awake();
            _field = new StarfieldDrift(starCount, seed);
        }

        void Update()
        {
            if (_field == null) return;

            // Unscaled: a menu over a paused level has timeScale at zero, and a sky that freezes with the
            // game looks broken rather than paused.
            _field.Advance(Time.unscaledDeltaTime);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_field == null) return;

            var area = GetPixelAdjustedRect();
            var stars = _field.Stars;

            for (var i = 0; i < stars.Length; i++)
            {
                var star = stars[i];

                var centre = new Vector2(area.xMin + star.X * area.width, area.yMin + star.Y * area.height);
                var half = starSize * star.Size * 0.5f;

                var tint = color;
                tint.a *= star.Brightness;

                Quad(helper, centre, half, tint);
            }
        }

        static void Quad(VertexHelper helper, Vector2 centre, float half, Color32 tint)
        {
            var index = helper.currentVertCount;

            helper.AddVert(new Vector3(centre.x - half, centre.y - half), tint, new Vector2(0f, 0f));
            helper.AddVert(new Vector3(centre.x - half, centre.y + half), tint, new Vector2(0f, 1f));
            helper.AddVert(new Vector3(centre.x + half, centre.y + half), tint, new Vector2(1f, 1f));
            helper.AddVert(new Vector3(centre.x + half, centre.y - half), tint, new Vector2(1f, 0f));

            helper.AddTriangle(index + 0, index + 1, index + 2);
            helper.AddTriangle(index + 2, index + 3, index + 0);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // So the count, the seed and the size can be judged in the Inspector rather than only in play.
            if (Application.isPlaying && _field != null && _field.Count != starCount)
                _field = new StarfieldDrift(starCount, seed);

            SetVerticesDirty();
        }
#endif
    }
}
