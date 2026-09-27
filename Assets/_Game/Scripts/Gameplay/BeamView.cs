using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// A sweeping beam and the line that warns of it: the Sniper's shot and the bosses' (GDD "Enemies and
    /// Hazards": the warning is the whole fight). Purely presentation; whether anything is hit is decided by
    /// <see cref="YASS.Core.SniperShot"/> and the boss's sweep in the rules layer.
    /// </summary>
    /// <remarks>
    /// **It is three sprites, not one.** A single stretched quad is what this was, and it read as exactly
    /// that: a flat rectangle of colour that began in mid-air wherever the muzzle happened to be. What sells
    /// a beam is the layering, and each part earns its place:
    ///
    /// - the **glow**, wide and dim, which is the light spilling around the beam and is what stops it having
    ///   a hard edge against the sky;
    /// - the **shaft**, narrow and white hot, which is the beam itself;
    /// - the **flare** at the muzzle, which covers the join. Without it the shaft has a visible start, and a
    ///   line that starts in the middle of nowhere never looks like it came out of anything.
    ///
    /// All three are additive, which matters more than it sounds: addition does not care what order it
    /// happens in, so the three may share a sorting order and still composite correctly, and the part of the
    /// shaft that lies over the boss reads as the emitter glowing rather than as a rectangle drawn on top of
    /// it. It is also why the beam looks like light instead of paint.
    ///
    /// The shaft is sunk back into the emitter by <see cref="rootOverlap"/>, so it begins inside the body
    /// rather than at its edge. That is the other half of the fix: the eye needs the beam to be continuous
    /// with the thing firing it.
    /// </remarks>
    public sealed class BeamView : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField, Tooltip("The white hot centre.")] SpriteRenderer shaft;
        [SerializeField, Tooltip("Wider and dimmer, the light spilling around the shaft.")] SpriteRenderer glow;
        [SerializeField, Tooltip("Sits on the muzzle and hides where the shaft begins.")] SpriteRenderer flare;

        [Header("Shape")]
        [SerializeField, Min(0f)]
        [Tooltip("How far back into the emitter the shaft starts, in world units. Without this the beam " +
                 "begins at the muzzle and looks detached from the thing firing it.")]
        float rootOverlap = 1.4f;

        [SerializeField, Range(1.5f, 8f), Tooltip("How much wider the glow is than the shaft.")]
        float glowWidth = 3.2f;

        [SerializeField, Range(1f, 12f), Tooltip("Flare size, as a multiple of the beam's width.")]
        float flareSize = 5f;

        [Header("Colour")]
        [SerializeField, Tooltip("The shaft while firing. Additive, so this is light being added.")]
        Color beamColour = new Color(1f, 0.83f, 0.45f, 1f);

        [SerializeField, Tooltip("The glow around it. Dimmer and further towards the beam's own hue.")]
        Color glowColour = new Color(1f, 0.55f, 0.18f, 0.5f);

        [SerializeField, Tooltip("The warning line, before the shot.")]
        Color warningColour = new Color(1f, 0.35f, 0.3f, 0.9f);

        Vector3 _parentScale = Vector3.one;

        /// <summary>
        /// When this shot started, so the shimmer is measured from it rather than from an absolute clock.
        /// </summary>
        /// <remarks>
        /// Three things follow from this being per shot. Every beam starts its shimmer at the same phase, so
        /// no shot happens to begin at the bottom of a flicker. It cannot drift: an absolute clock loses
        /// float resolution over a long session until a wave multiplied by forty-one is visibly quantised.
        /// And it is scaled time, so a beam frozen behind a pause menu stays frozen, which the boss's (driven
        /// from Update) and the Sniper's (driven from FixedUpdate) previously disagreed about.
        /// </remarks>
        float _firingSince = float.NegativeInfinity;

        bool _firing;

        /// <summary>
        /// A fixed offset into the shimmer, different for every emitter. Without it every beam on screen
        /// pulses on the same beat, which does not read as several machines working: it reads as the screen
        /// itself flickering, which is the opposite of what the shimmer is for.
        /// </summary>
        float _phase;

        /// <summary>
        /// Each part's sprite size in units, taken once. <c>sprite.bounds</c> is a call into native code
        /// whose answer never changes, and it was being asked three times a frame for the whole length of
        /// every beam.
        /// </summary>
        Vector2 _shaftSize, _glowSize, _flareSize;

        bool _originKnown;
        Vector2 _localOrigin;

        /// <summary>
        /// Where the beam leaves the ship, in the emitter's own space: **the position this object is placed
        /// at in the prefab.** Drag the Beam object onto the gun and the beam comes out of the gun.
        /// </summary>
        /// <remarks>
        /// It has to be captured before anything draws, because drawing destroys it: <see cref="Lay"/> sets
        /// this transform's world position every frame, so by the second frame the authored value is gone.
        /// That is also why it is read here rather than by the callers reading <c>transform.localPosition</c>
        /// themselves, which would give them whatever the last frame happened to leave behind.
        ///
        /// The glow and the flare follow this one point rather than their own positions. They are parts of a
        /// single beam, and letting them sit apart from the shaft would only ever be a mistake.
        /// </remarks>
        public Vector2 LocalOrigin
        {
            get
            {
                if (_originKnown) return _localOrigin;

                _originKnown = true;
                _localOrigin = transform.localPosition;

                return _localOrigin;
            }
        }

        void Reset()
        {
            // All three, by name. Wiring only the shaft leaves a beam with no glow and no flare, which is
            // indistinguishable from the flat rectangle this replaced.
            shaft = Named("Beam");
            glow = Named("BeamGlow");
            flare = Named("BeamFlare");
        }

        SpriteRenderer Named(string name)
        {
            var found = transform.parent != null ? transform.parent.Find(name) : null;
            if (found == null && transform.name == name) return GetComponent<SpriteRenderer>();

            return found == null ? null : found.GetComponent<SpriteRenderer>();
        }

        void Awake()
        {
            // The emitter's scale is fixed for its lifetime; decomposing its matrix every step would be waste.
            // Load bearing: all three parts are siblings under this same parent, so one cached scale serves
            // them all. Parent a part anywhere else and its size will be divided by the wrong number.
            if (transform.parent != null) _parentScale = transform.parent.lossyScale;

            // Before anything can draw over it. See LocalOrigin.
            _ = LocalOrigin;

            // Spread out by object, not at random, so a given emitter looks the same every time it fires.
            // Hashed rather than cast: an EntityId is no longer representable as an int, and its exact
            // value means nothing here anyway. All that is wanted is a stable number per object.
            _phase = Mathf.Abs(GetEntityId().GetHashCode() % 1000) * 0.017f;

            if (shaft == null || glow == null || flare == null)
                Debug.LogWarning($"{name}: a beam is missing one of its three parts (shaft={shaft != null}, " +
                                 $"glow={glow != null}, flare={flare != null}), so it will draw flat.");

            Hide();
        }

        /// <summary>
        /// The part's sprite size in units, worked out once and kept. <c>sprite.bounds</c> is a call into
        /// native code whose answer never changes, and it was being asked three times a frame for as long as
        /// a beam was up.
        /// </summary>
        /// <remarks>
        /// Filled on first use rather than in <c>Awake</c>. Awake does not run for a prefab instantiated in
        /// the editor, and a cache left at zero makes <c>Divide</c> collapse the scale, so the beam draws
        /// nothing at all: an editor preview of this went blank for exactly that reason, and the failure is
        /// silent because a zero scale looks the same as a beam nobody asked for.
        /// </remarks>
        static Vector2 SizeOf(SpriteRenderer part, ref Vector2 cached)
        {
            if (cached != Vector2.zero) return cached;

            cached = part != null && part.sprite != null ? (Vector2)part.sprite.bounds.size : Vector2.one;
            return cached;
        }

        /// <summary>
        /// Draws the beam from <paramref name="origin"/> along <paramref name="aim"/>. While warning it is a
        /// thin line that builds with <paramref name="warningProgress"/>, so the closer the shot the harder
        /// it is to miss; while firing it is the full three-layer beam.
        /// </summary>
        public void Show(Vector2 origin, Vector2 aim, float length, float halfWidth, bool firing,
            float warningProgress)
        {
            if (aim.sqrMagnitude < 1e-6f) return;

            // Taken before the first Lay overwrites this transform, which covers anything that draws without
            // Awake having run, such as the editor's parade window.
            _ = LocalOrigin;

            var direction = aim.normalized;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var turn = Quaternion.Euler(0f, 0f, angle);

            if (firing && !_firing) _firingSince = Time.time;
            _firing = firing;

            // Measured from the start of this shot, not from an absolute clock: see _firingSince.
            var now = Time.time - _firingSince + _phase;

            if (firing)
            {
                var width = halfWidth * 2f * BeamLook.WidthAt(now);
                var brightness = BeamLook.BrightnessAt(now);

                // Sunk into the emitter, so the shaft is continuous with the body it comes out of.
                Lay(shaft, SizeOf(shaft, ref _shaftSize), origin, direction, turn, length, width,
                    rootOverlap, Dim(beamColour, brightness));
                Lay(glow, SizeOf(glow, ref _glowSize), origin, direction, turn, length, width * glowWidth,
                    rootOverlap, Dim(glowColour, brightness));

                ShowFlare(origin, turn, width * flareSize, brightness, now);
            }
            else
            {
                var width = halfWidth * 2f * BeamLook.WarningWidthAt(warningProgress);
                var colour = Dim(warningColour, BeamLook.WarningAt(warningProgress));

                // No root overlap and no flare while warning: nothing is coming out of the emitter yet, and
                // a flare sitting there would promise a shot that has already been fired.
                Lay(shaft, SizeOf(shaft, ref _shaftSize), origin, direction, turn, length, width, 0f, colour);
                Hide(glow);
                Hide(flare);
            }
        }

        public void Hide()
        {
            _firing = false;
            Hide(shaft);
            Hide(glow);
            Hide(flare);
        }

        /// <summary>
        /// Puts one layer along the beam. <paramref name="overlap"/> extends it backwards past the origin,
        /// into whatever is firing it.
        /// </summary>
        void Lay(SpriteRenderer part, Vector2 spriteSize, Vector2 origin, Vector2 direction, Quaternion turn,
            float length, float width, float overlap, Color colour)
        {
            if (part == null) return;

            // The sprite's pivot is its centre, so it sits half its length along the aim. The overlap
            // lengthens it behind the origin, which moves that centre back by half as much.
            var whole = length + overlap;
            part.transform.SetPositionAndRotation(origin + direction * (length - overlap) * 0.5f, turn);
            part.transform.localScale = LocalScaleFor(spriteSize, whole, width);

            part.color = colour;
            part.enabled = true;
        }

        void ShowFlare(Vector2 origin, Quaternion turn, float size, float brightness, float age)
        {
            if (flare == null) return;

            flare.transform.SetPositionAndRotation(origin, turn);
            flare.transform.localScale = LocalScaleFor(SizeOf(flare, ref _flareSize), size, size);

            // Its own quicker beat, so the muzzle looks like it is working rather than painted on.
            flare.color = Dim(beamColour, brightness * (0.85f + 0.15f * Mathf.Sin(age * 13f)));
            flare.enabled = true;
        }

        static void Hide(SpriteRenderer part)
        {
            if (part != null) part.enabled = false;
        }

        /// <summary>Scales the colour's alpha, which under additive blending is how bright it is.</summary>
        static Color Dim(Color colour, float by)
        {
            colour.a *= by;
            return colour;
        }

        /// <summary>
        /// Turns a world-space length and width into a local scale. The sprite has its own size in units and
        /// the thing it hangs off may be scaled, so neither may be allowed to stretch the beam.
        /// </summary>
        Vector3 LocalScaleFor(Vector2 spriteSize, float length, float width)
        {
            return new Vector3(
                Divide(length, spriteSize.x * _parentScale.x),
                Divide(width, spriteSize.y * _parentScale.y),
                1f);
        }

        /// <summary>A degenerate scale collapses the beam rather than drawing one of the wrong length.</summary>
        static float Divide(float value, float divisor) => Mathf.Abs(divisor) > 1e-6f ? value / divisor : 0f;
    }
}
