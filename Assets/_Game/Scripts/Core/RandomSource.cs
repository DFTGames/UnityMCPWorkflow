using System;

namespace YASS.Core
{
    /// <summary>Source of uniform random numbers, injectable for tests and deterministic networking.</summary>
    public interface IRandomSource
    {
        /// <summary>Returns a value in [0, 1).</summary>
        float NextFloat();
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        const int Resolution = 1 << 24; // float has a 24-bit significand, so every value below is exact.

        readonly Random _random;

        public SystemRandomSource(int seed) => _random = new Random(seed);
        public SystemRandomSource() => _random = new Random();

        public float NextFloat() => _random.Next(Resolution) * (1f / Resolution);
    }
}
