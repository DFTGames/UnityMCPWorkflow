namespace YASS.Core
{
    public enum HitOutcome
    {
        Ignored = 0,
        Absorbed = 1,
        Damaged = 2,
        LifeLost = 3,
        GameOver = 4
    }

    public static class HitOutcomeExtensions
    {
        /// <summary>True when health was actually lost. Ignored and shield-absorbed hits are not damage.</summary>
        public static bool TookDamage(this HitOutcome outcome) =>
            outcome == HitOutcome.Damaged || outcome == HitOutcome.LifeLost || outcome == HitOutcome.GameOver;
    }
}
