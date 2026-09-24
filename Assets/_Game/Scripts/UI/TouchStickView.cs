using UnityEngine;
using UnityEngine.UI;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.UI
{
    /// <summary>
    /// Draws the two on-screen sticks where the player's thumbs are (GDD "Controls", Touch). A ring for the
    /// stick's centre and a knob showing how far it has been pushed, both only while a thumb is down.
    /// </summary>
    /// <remarks>
    /// Deliberately understated: the ring is drawn smaller than the thumb's actual reach and both parts are
    /// part-transparent, because the sticks sit on top of the thing the player is trying to watch. They are
    /// there to say where the stick centred and how hard it is being pushed, not to be furniture.
    ///
    /// Hidden whenever nothing is touching them, so a player on a desktop with a touchscreen never sees them.
    /// </remarks>
    public sealed class TouchStickView : MonoBehaviour
    {
        [SerializeField] TouchSticks sticks;

        [SerializeField, Tooltip("Ring and knob for the movement stick, in that order.")]
        RectTransform moveBase;

        [SerializeField] RectTransform moveKnob;

        [SerializeField, Tooltip("Ring and knob for the aiming stick.")]
        RectTransform aimBase;

        [SerializeField] RectTransform aimKnob;

        [SerializeField, Range(0f, 1f), Tooltip("How solid the sticks are drawn over the game.")]
        float opacity = 0.35f;

        Canvas _canvas;
        Drawn _moveDrawn;
        Drawn _aimDrawn;

        /// <summary>
        /// What a stick was last drawn with. Writing a <c>RectTransform</c>'s position or size dirties the
        /// canvas and re-meshes the graphic, so the HUD was being rebuilt every frame of every touch even
        /// while the thumb was perfectly still. Held so that a frame that changed nothing writes nothing.
        /// </summary>
        struct Drawn
        {
            public bool Valid;
            public Vector2 Origin;
            public Vector2 Deflection;
            public float Radius;
            public float Scale;
        }

        void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();

            if (sticks == null)
                Debug.LogError($"{nameof(TouchStickView)}: no {nameof(TouchSticks)} assigned; " +
                               "the sticks will never be drawn.", this);

            Fade(moveBase);
            Fade(moveKnob);
            Fade(aimBase);
            Fade(aimKnob);
        }

        void LateUpdate()
        {
            if (sticks == null) return;

            // After the input has been read, so the knob shows the deflection the ship is acting on rather
            // than the one from the frame before.
            Draw(sticks.Move, moveBase, moveKnob, ref _moveDrawn);
            Draw(sticks.Aim, aimBase, aimKnob, ref _aimDrawn);
        }

        /// <summary>Whatever is on screen goes with the view, rather than freezing there at its last push.</summary>
        void OnDisable()
        {
            Hide(moveBase);
            Hide(moveKnob);
            Hide(aimBase);
            Hide(aimKnob);

            _moveDrawn = default;
            _aimDrawn = default;
        }

        void Draw(VirtualStick stick, RectTransform ring, RectTransform knob, ref Drawn last)
        {
            if (ring == null || knob == null) return;

            if (!stick.IsHeld)
            {
                Show(ring, false);
                Show(knob, false);
                last = default;
                return;
            }

            Show(ring, true);
            Show(knob, true);

            var origin = stick.Origin.ToUnity();
            var deflection = stick.Deflection.ToUnity();
            var scale = Scale();

            if (!last.Valid || last.Origin != origin) Place(ring, origin);

            // The offset is already in screen pixels, like the radius it came from, and a RectTransform's
            // position on an overlay canvas is screen pixels too. Scaling it here was the one place the two
            // units got mixed: the knob moved by the canvas's scale factor less than the thumb had pushed.
            if (!last.Valid || last.Origin != origin || last.Deflection != deflection)
                knob.position = new Vector3(origin.x, origin.y, 0f) +
                                (Vector3)TouchControls.KnobOffset(stick.Deflection, stick.Radius).ToUnity();

            // Constant for the life of a held stick: only a rebuild, from a rotation or a resized window,
            // can change the reach or the scale it is converted through.
            if (!last.Valid || last.Radius != stick.Radius || last.Scale != scale)
            {
                Size(ring, stick.Radius * TouchControls.BaseRadiusFraction, scale);
                Size(knob, stick.Radius * TouchControls.KnobRadiusFraction, scale);
            }

            last = new Drawn
            {
                Valid = true,
                Origin = origin,
                Deflection = deflection,
                Radius = stick.Radius,
                Scale = scale,
            };
        }

        static void Hide(RectTransform part)
        {
            if (part != null) Show(part, false);
        }

        /// <summary>
        /// Puts a part at a point on the screen. The sticks are measured in screen pixels, which is what a
        /// thumb touches, so they are placed in screen space rather than in the canvas's own units.
        /// </summary>
        static void Place(RectTransform part, Vector2 screenPoint) =>
            part.position = new Vector3(screenPoint.x, screenPoint.y, 0f);

        /// <summary>A radius in screen pixels, as a width and height in the canvas's units.</summary>
        static void Size(RectTransform part, float radiusInPixels, float scale)
        {
            var diameter = radiusInPixels * 2f / scale;
            part.sizeDelta = new Vector2(diameter, diameter);
        }

        float Scale() => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

        static void Show(RectTransform part, bool shown)
        {
            if (part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        void Fade(RectTransform part)
        {
            if (part == null) return;

            var image = part.GetComponent<Image>();
            if (image == null) return;

            var colour = image.color;
            image.color = new Color(colour.r, colour.g, colour.b, opacity);
            image.raycastTarget = false; // the sticks are drawn, not pressed: they must not eat the touch
        }
    }
}
