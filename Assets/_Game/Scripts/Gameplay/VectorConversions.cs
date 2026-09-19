using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>Converts between Unity vectors and the System.Numerics vectors used by the engine-free rules layer.</summary>
    public static class VectorConversions
    {
        public static NVector2 ToNumerics(this Vector2 v) => new NVector2(v.x, v.y);
        public static NVector2 ToNumerics(this Vector3 v) => new NVector2(v.x, v.y);
        public static Vector2 ToUnity(this NVector2 v) => new Vector2(v.X, v.Y);
    }
}
