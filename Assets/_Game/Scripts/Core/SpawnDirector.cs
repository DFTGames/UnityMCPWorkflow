using System;
using System.Collections.Generic;

namespace YASS.Core
{
    public readonly struct SpawnRequest
    {
        /// <summary>Index into the spawn table the director was built with.</summary>
        public readonly int EntryIndex;

        /// <summary>Vertical spawn position: 0 is the bottom of the spawn band, 1 the top.</summary>
        public readonly float NormalisedY;

        public SpawnRequest(int entryIndex, float normalisedY)
        {
            EntryIndex = entryIndex;
            NormalisedY = normalisedY;
        }
    }

    /// <summary>
    /// Prototype spawner: picks a weighted random entry at a steady interval. Temporary until the scripted wave
    /// system described in the GDD exists. The difficulty's enemy count multiplier shortens the interval.
    /// </summary>
    public sealed class SpawnDirector
    {
        readonly int[] _weights;
        readonly int _totalWeight;
        readonly IRandomSource _random;
        float _timer;

        public float Interval { get; }

        public SpawnDirector(IReadOnlyList<int> weights, float baseInterval, float countMultiplier,
            IRandomSource random, float initialDelay)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (baseInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(baseInterval));
            if (countMultiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(countMultiplier));
            if (initialDelay < 0f) throw new ArgumentOutOfRangeException(nameof(initialDelay));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _weights = new int[weights.Count];
            for (var i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0) throw new ArgumentOutOfRangeException(nameof(weights), "Weights must not be negative.");
                _weights[i] = weights[i];
                _totalWeight += weights[i];
            }

            if (_totalWeight <= 0) throw new ArgumentException("At least one weight must be positive.", nameof(weights));

            Interval = baseInterval / countMultiplier;
            _timer = initialDelay;
        }

        /// <summary>Advances the timer; appends at most one spawn per tick. Returns true when it spawned.</summary>
        public bool Tick(float deltaTime, List<SpawnRequest> requests)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (requests == null) throw new ArgumentNullException(nameof(requests));

            _timer -= deltaTime;
            if (_timer > 0f) return false;

            _timer += Interval;
            if (_timer <= 0f) _timer = Interval;

            requests.Add(new SpawnRequest(PickEntry(_random.NextFloat()), _random.NextFloat()));
            return true;
        }

        int PickEntry(float roll)
        {
            var value = roll * _totalWeight;
            for (var i = 0; i < _weights.Length; i++)
            {
                if (value < _weights[i]) return i;
                value -= _weights[i];
            }

            // roll is below 1, so this is only reached through float rounding: use the last positive weight.
            for (var i = _weights.Length - 1; i >= 0; i--)
                if (_weights[i] > 0) return i;
            return 0;
        }
    }
}
