using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>Player ship movement: free 8-way movement kept inside the playfield.</summary>
    public static class ShipMotor
    {
        /// <summary>
        /// Moves by <paramref name="move"/> (length clamped to 1, so diagonals are not faster) at
        /// <paramref name="speed"/> units per second, then clamps to <paramref name="bounds"/>.
        /// </summary>
        public static Vector2 Step(Vector2 position, Vector2 move, float speed, float deltaTime, Playfield bounds)
        {
            if (speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed));
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            var lengthSquared = move.LengthSquared();
            if (lengthSquared > 1f) move /= MathF.Sqrt(lengthSquared);

            return bounds.Clamp(position + move * (speed * deltaTime));
        }
    }
}
