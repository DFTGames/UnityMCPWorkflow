using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>Meteor rules (GDD "Enemies and Hazards" and "Scoring").</summary>
    public static class MeteorRules
    {
        public const int FragmentsPerSplit = 2;

        /// <summary>Fragments fly off at this angle either side of the parent's direction of travel.</summary>
        public const float FragmentSpreadDegrees = 25f;

        /// <summary>Large splits into medium, medium into small; small does not split.</summary>
        public static bool TrySplit(MeteorSize size, out MeteorSize fragment)
        {
            switch (size)
            {
                case MeteorSize.Large:
                    fragment = MeteorSize.Medium;
                    return true;
                case MeteorSize.Medium:
                    fragment = MeteorSize.Small;
                    return true;
                case MeteorSize.Small:
                    fragment = MeteorSize.Small;
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }
        }

        public static int Points(bool splitting, MeteorSize size) =>
            splitting ? PointValues.SplittingMeteor(size) : PointValues.SolidMeteor;

        public static DropSource DropSourceFor(bool splitting) =>
            splitting ? DropSource.SplittingMeteor : DropSource.SolidMeteor;

        /// <summary>Velocity of fragment <paramref name="index"/> (0 or 1): the parent's velocity rotated by +/- the spread.</summary>
        public static Vector2 FragmentVelocity(Vector2 parentVelocity, int index)
        {
            if (index < 0 || index >= FragmentsPerSplit) throw new ArgumentOutOfRangeException(nameof(index));

            var degrees = index == 0 ? FragmentSpreadDegrees : -FragmentSpreadDegrees;
            var radians = degrees * (MathF.PI / 180f);
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            return new Vector2(parentVelocity.X * cos - parentVelocity.Y * sin,
                parentVelocity.X * sin + parentVelocity.Y * cos);
        }
    }
}
