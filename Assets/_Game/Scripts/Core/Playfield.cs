using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// The visible play area in world units. The GDD fixes the visible height; the width follows the aspect ratio.
    /// </summary>
    public readonly struct Playfield
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinY;
        public readonly float MaxY;

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;

        public Playfield(float minX, float maxX, float minY, float maxY)
        {
            if (maxX < minX) throw new ArgumentException("maxX must not be less than minX.");
            if (maxY < minY) throw new ArgumentException("maxY must not be less than minY.");

            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        /// <summary>The area seen by an orthographic camera centred on <paramref name="centre"/>.</summary>
        public static Playfield FromCamera(Vector2 centre, float orthographicSize, float aspect)
        {
            if (orthographicSize <= 0f) throw new ArgumentOutOfRangeException(nameof(orthographicSize));
            if (aspect <= 0f) throw new ArgumentOutOfRangeException(nameof(aspect));

            var halfHeight = orthographicSize;
            var halfWidth = orthographicSize * aspect;
            return new Playfield(centre.X - halfWidth, centre.X + halfWidth, centre.Y - halfHeight, centre.Y + halfHeight);
        }

        /// <summary>
        /// Shrinks each edge inwards by <paramref name="margin"/> (a negative margin grows it).
        /// An axis that would invert collapses to its centre line.
        /// </summary>
        public Playfield Inset(float margin)
        {
            var minX = MinX + margin;
            var maxX = MaxX - margin;
            var minY = MinY + margin;
            var maxY = MaxY - margin;

            if (minX > maxX) minX = maxX = (MinX + MaxX) * 0.5f;
            if (minY > maxY) minY = maxY = (MinY + MaxY) * 0.5f;
            return new Playfield(minX, maxX, minY, maxY);
        }

        public bool Contains(Vector2 point) =>
            point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

        public Vector2 Clamp(Vector2 point) =>
            new Vector2(Math.Clamp(point.X, MinX, MaxX), Math.Clamp(point.Y, MinY, MaxY));

        /// <summary>Maps 0 to the bottom edge and 1 to the top edge.</summary>
        public float YAt(float normalised) => MinY + Height * Math.Clamp(normalised, 0f, 1f);
    }
}
