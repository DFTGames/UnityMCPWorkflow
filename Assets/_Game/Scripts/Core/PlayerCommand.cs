using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// One tick of player intent, independent of the device that produced it. Plain data so it can later be
    /// sent over the network (Photon Fusion input structs will mirror it at the adapter edge).
    /// </summary>
    public struct PlayerCommand
    {
        public Vector2 Move;
        public bool Fire;
        public Vector2 AimDirection;

        public PlayerCommand(Vector2 move, bool fire, Vector2 aimDirection)
        {
            Move = move;
            Fire = fire;
            AimDirection = aimDirection;
        }
    }
}
