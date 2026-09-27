using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// Who takes a shot when the armour and the core are both in the way (GDD "Enemies and Hazards", Bosses).
    /// </summary>
    /// <remarks>
    /// **An open core wins, whatever the armour is doing.** A shot that would have reached the core if the
    /// hull were not there counts as a core hit: the armour does not get to absorb it, and the player gets
    /// the core's explosion rather than the dull thud of a hull hit.
    ///
    /// This exists because the alternative was making the art obey the physics. The armour is three boxes
    /// laid over the ship and the core is a lit circle somewhere on it, and demanding they never overlap
    /// pushed the core away from where the art wanted it on six of the eight bosses, one of them badly
    /// enough to have been unkillable. The core is the thing the player aims at and the thing the whole
    /// fight is about, so where it is drawn is what should decide where it can be hit. Overlap is now
    /// allowed and meaningless.
    ///
    /// It also removes a failure nobody could have reasoned about from the outside: both colliders are
    /// triggers on the same layer, so a shot landing in the overlap hit whichever the physics engine
    /// happened to report first. That was never decided by anything in this project.
    /// </remarks>
    public static class BossHits
    {
        /// <summary>
        /// Whether the core takes this shot instead of the armour: only while it is open, and only for a
        /// shot that actually landed on it.
        /// </summary>
        /// <param name="hitPoint">Where the shot landed, in world space.</param>
        /// <param name="coreCentre">The core's centre, in world space.</param>
        /// <param name="coreRadius">The core's radius, after its own scale.</param>
        /// <param name="coreOpen">Whether the core is open. A closed core is shutters, and armour again.</param>
        public static bool CoreTakesIt(Vector2 hitPoint, Vector2 coreCentre, float coreRadius, bool coreOpen)
        {
            // A closed core is not a weak point at all, so the armour keeps the shot and it is worth what
            // hitting the boss anywhere else is worth. Showing the core's explosion here would teach the
            // player the opposite of the one mechanic the fight has.
            if (!coreOpen) return false;

            // A core with no radius cannot be hit. Guarded because a missing or zero collider would
            // otherwise make every hull hit within nothing at all count as a core hit.
            if (coreRadius <= 0f) return false;

            return Vector2.DistanceSquared(hitPoint, coreCentre) <= coreRadius * coreRadius;
        }
    }
}
