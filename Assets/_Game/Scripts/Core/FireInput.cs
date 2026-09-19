using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// Turns raw device input into a fire decision and direction (GDD page "Controls").
    /// Stick values must be raw (no Input System dead-zone processor), or the dead zone applies twice.
    /// </summary>
    public static class FireInput
    {
        public static readonly Vector2 Forward = Vector2.UnitX;

        /// <summary>
        /// Gamepad: the right stick fires in its direction beyond the dead zone; the right trigger fires forward.
        /// When both are active the stick direction wins.
        /// </summary>
        public static bool ResolveGamepad(Vector2 rightStick, bool triggerHeld, float deadZone, out Vector2 direction)
        {
            if (IsBeyond(rightStick, deadZone))
            {
                direction = Vector2.Normalize(rightStick);
                return true;
            }

            direction = triggerHeld ? Forward : Vector2.Zero;
            return triggerHeld;
        }

        /// <summary>Mouse: fires towards the pointer while the button is held.</summary>
        public static bool ResolvePointer(Vector2 shipPosition, Vector2 pointerWorldPosition, bool buttonHeld,
            out Vector2 direction)
        {
            if (!buttonHeld)
            {
                direction = Vector2.Zero;
                return false;
            }

            var delta = pointerWorldPosition - shipPosition;
            direction = delta.LengthSquared() > 1e-6f ? Vector2.Normalize(delta) : Forward;
            return true;
        }

        /// <summary>Touch: the right virtual stick fires while held, in its direction, or forward when centred.</summary>
        public static bool ResolveVirtualStick(Vector2 stick, bool held, float deadZone, out Vector2 direction)
        {
            if (!held)
            {
                direction = Vector2.Zero;
                return false;
            }

            direction = IsBeyond(stick, deadZone) ? Vector2.Normalize(stick) : Forward;
            return true;
        }

        static bool IsBeyond(Vector2 stick, float deadZone)
        {
            if (deadZone < 0f) throw new ArgumentOutOfRangeException(nameof(deadZone));
            return stick.LengthSquared() > deadZone * deadZone;
        }
    }
}
