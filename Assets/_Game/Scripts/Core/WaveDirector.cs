using System;
using System.Collections.Generic;

namespace YASS.Core
{
    public enum Formation
    {
        Line = 0,
        Column = 1,
        V = 2,
        Staggered = 3,

        /// <summary>One every spacing seconds, spread across the whole spawn band (meteor fields). Ignores entry height.</summary>
        Scatter = 4
    }

    public enum LevelPhase
    {
        Waves = 0,
        BossWarning = 1,
        Boss = 2,
        Cleared = 3
    }

    public enum LevelEvent
    {
        None = 0,
        BossWarning = 1,
        BossArrives = 2,
        SectorClear = 3
    }

    /// <summary>One group of identical hazards within a wave (GDD "Wave System").</summary>
    public readonly struct SpawnGroupSpec
    {
        public readonly int Count;
        public readonly Formation Formation;
        public readonly float EntryHeight;
        public readonly float Spacing;
        public readonly float StartDelay;

        public SpawnGroupSpec(int count, Formation formation, float entryHeight, float spacing, float startDelay)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (spacing < 0f) throw new ArgumentOutOfRangeException(nameof(spacing));
            if (startDelay < 0f) throw new ArgumentOutOfRangeException(nameof(startDelay));

            Count = count;
            Formation = formation;
            EntryHeight = Math.Clamp(entryHeight, 0f, 1f);
            Spacing = spacing;
            StartDelay = startDelay;
        }
    }

    /// <summary>A hazard to spawn now: which wave, group and member it is, and where in its formation.</summary>
    public readonly struct WaveSpawnRequest
    {
        public readonly int WaveIndex;
        public readonly int GroupIndex;
        public readonly int MemberIndex;

        /// <summary>0 is the bottom of the spawn band, 1 the top.</summary>
        public readonly float NormalisedY;

        /// <summary>Extra distance behind the spawn line, in world units (V formations).</summary>
        public readonly float XOffset;

        public WaveSpawnRequest(int waveIndex, int groupIndex, int memberIndex, float normalisedY, float xOffset)
        {
            WaveIndex = waveIndex;
            GroupIndex = groupIndex;
            MemberIndex = memberIndex;
            NormalisedY = normalisedY;
            XOffset = xOffset;
        }
    }

    /// <summary>Where and when each member of a formation spawns.</summary>
    public static class FormationLayout
    {
        public const float ColumnStep = 0.12f;
        public const float VStepY = 0.08f;
        public const float VStepX = 0.7f;
        public const float StaggerOffset = 0.2f;

        /// <summary>Scatter heights stay this far inside the spawn band.</summary>
        public const float ScatterMargin = 0.1f;

        /// <summary>Golden-ratio step: successive members land far apart and never repeat a height.</summary>
        const float ScatterStep = 0.618034f;

        /// <summary>Seconds after the group starts at which member <paramref name="index"/> spawns.</summary>
        public static float Delay(Formation formation, int index, float spacing)
        {
            switch (formation)
            {
                case Formation.Line:
                case Formation.Staggered:
                case Formation.Scatter:
                    return index * spacing;
                case Formation.Column:
                case Formation.V:
                    return 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(formation), formation, null);
            }
        }

        public static void Slot(Formation formation, int index, int count, float entryHeight,
            out float normalisedY, out float xOffset)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));

            var fromCentre = index - (count - 1) * 0.5f;
            switch (formation)
            {
                case Formation.Line:
                    normalisedY = entryHeight;
                    xOffset = 0f;
                    break;
                case Formation.Column:
                    normalisedY = entryHeight + fromCentre * ColumnStep;
                    xOffset = 0f;
                    break;
                case Formation.V:
                    normalisedY = entryHeight + fromCentre * VStepY;
                    xOffset = Math.Abs(fromCentre) * VStepX;
                    break;
                case Formation.Staggered:
                    normalisedY = entryHeight + (index % 2 == 0 ? StaggerOffset : -StaggerOffset);
                    xOffset = 0f;
                    break;
                case Formation.Scatter:
                    var phase = 0.5f + index * ScatterStep;
                    normalisedY = ScatterMargin + (1f - 2f * ScatterMargin) * (phase - MathF.Floor(phase));
                    xOffset = 0f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(formation), formation, null);
            }

            normalisedY = Math.Clamp(normalisedY, 0f, 1f);
        }

        /// <summary>True when members spawn one after another (so a spacing of zero would stack them).</summary>
        public static bool IsSequential(Formation formation) =>
            formation == Formation.Line || formation == Formation.Staggered || formation == Formation.Scatter;

        /// <summary>Count scaled by the difficulty's enemy count multiplier, rounded, never below 1.</summary>
        public static int ScaledCount(int count, float countMultiplier)
        {
            if (countMultiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(countMultiplier));

            // Via decimal: 1.3f is really 1.2999999..., so 5 x 1.3f would round to 6 instead of 7. The float-to-decimal
            // conversion keeps 7 significant digits, which recovers the tuned value exactly.
            var scaled = Math.Round(count * (decimal)countMultiplier, MidpointRounding.AwayFromZero);
            return Math.Max(1, (int)scaled);
        }
    }

    /// <summary>
    /// Runs a level's waves and then its boss (GDD "Wave System"). The Unity layer spawns what
    /// <see cref="Tick"/> asks for and reports every hazard that leaves play, so the director knows when a wave
    /// is cleared.
    /// </summary>
    public sealed class WaveDirector
    {
        public const float GapAfterClear = 3f;
        public const float MaxWaveDuration = 20f;
        public const float BossWarningDuration = 3f;

        /// <summary>Seconds between the boss's defeat and Sector Clear (time to collect its drops).</summary>
        public const float SectorClearDelay = 2.5f;

        readonly SpawnGroupSpec[][] _waves;
        readonly int[][] _counts;
        readonly int[][] _spawned;
        readonly int[] _outstanding;
        readonly float[] _startTime;
        readonly float[] _clearedTime;
        readonly float _initialDelay;

        float _time;
        float _phaseTime;
        int _nextWave;

        public LevelPhase Phase { get; private set; }

        /// <summary>True once the Sector Clear delay after the boss's defeat has passed.</summary>
        public bool IsSectorClear { get; private set; }
        public int WaveCount => _waves.Length;

        /// <summary>Index of the most recently started wave, or -1 before the first.</summary>
        public int CurrentWave => _nextWave - 1;

        public WaveDirector(IReadOnlyList<IReadOnlyList<SpawnGroupSpec>> waves, float countMultiplier,
            float initialDelay = 2f)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (initialDelay < 0f) throw new ArgumentOutOfRangeException(nameof(initialDelay));

            _waves = new SpawnGroupSpec[waves.Count][];
            _counts = new int[waves.Count][];
            _spawned = new int[waves.Count][];
            for (var w = 0; w < waves.Count; w++)
            {
                var groups = waves[w] ?? throw new ArgumentException($"Wave {w} is null.", nameof(waves));
                if (groups.Count == 0) throw new ArgumentException($"Wave {w} has no groups.", nameof(waves));

                _waves[w] = new SpawnGroupSpec[groups.Count];
                _counts[w] = new int[groups.Count];
                _spawned[w] = new int[groups.Count];
                for (var g = 0; g < groups.Count; g++)
                {
                    _waves[w][g] = groups[g];
                    _counts[w][g] = FormationLayout.ScaledCount(groups[g].Count, countMultiplier);
                }
            }

            _outstanding = new int[waves.Count];
            _startTime = new float[waves.Count];
            _clearedTime = new float[waves.Count];
            for (var w = 0; w < waves.Count; w++) _clearedTime[w] = -1f;
            _initialDelay = initialDelay;
        }

        /// <summary>Number of hazards group <paramref name="groupIndex"/> of wave <paramref name="waveIndex"/> spawns after scaling.</summary>
        public int ScaledCount(int waveIndex, int groupIndex) => _counts[waveIndex][groupIndex];

        public int Outstanding(int waveIndex) => _outstanding[waveIndex];

        public LevelEvent Tick(float deltaTime, List<WaveSpawnRequest> spawns)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (spawns == null) throw new ArgumentNullException(nameof(spawns));

            _time += deltaTime;
            switch (Phase)
            {
                case LevelPhase.Waves:
                    return TickWaves(spawns);
                case LevelPhase.BossWarning:
                    _phaseTime += deltaTime;
                    if (_phaseTime < BossWarningDuration) return LevelEvent.None;
                    Phase = LevelPhase.Boss;
                    return LevelEvent.BossArrives;
                case LevelPhase.Cleared:
                    if (IsSectorClear) return LevelEvent.None;
                    _phaseTime += deltaTime;
                    if (_phaseTime < SectorClearDelay) return LevelEvent.None;
                    IsSectorClear = true;
                    return LevelEvent.SectorClear;
                default:
                    return LevelEvent.None;
            }
        }

        /// <summary>A hazard from wave <paramref name="waveIndex"/> was destroyed or left the screen. Negative indices are ignored.</summary>
        public void NotifyHazardGone(int waveIndex)
        {
            if (waveIndex < 0 || waveIndex >= _outstanding.Length) return;
            if (_outstanding[waveIndex] > 0) _outstanding[waveIndex]--;
        }

        /// <summary>A hazard joined wave <paramref name="waveIndex"/> after spawning (a meteor fragment).</summary>
        public void NotifyHazardAdded(int waveIndex)
        {
            if (waveIndex < 0 || waveIndex >= _outstanding.Length) return;
            _outstanding[waveIndex]++;
        }

        /// <summary>Jumps straight to the boss, skipping any remaining waves (tests and debug tools).</summary>
        public void SkipToBoss()
        {
            if (Phase == LevelPhase.Waves || Phase == LevelPhase.BossWarning) Phase = LevelPhase.Boss;
        }

        public void NotifyBossDefeated()
        {
            if (Phase != LevelPhase.Boss) return;
            Phase = LevelPhase.Cleared;
            _phaseTime = 0f;
        }

        LevelEvent TickWaves(List<WaveSpawnRequest> spawns)
        {
            if (_nextWave > 0)
            {
                var last = _nextWave - 1;
                if (_clearedTime[last] < 0f && IsWaveDone(last)) _clearedTime[last] = _time;
            }

            if (_nextWave < _waves.Length && ShouldStartNextWave())
            {
                _startTime[_nextWave] = _time;
                _nextWave++;
            }

            for (var w = 0; w < _nextWave; w++) SpawnDue(w, spawns);

            // The boss comes once every wave is done, or at most MaxWaveDuration after the last wave started, so a
            // hazard that never leaves play cannot hold the level forever.
            if (_nextWave == _waves.Length && (AllWavesDone() || LastWaveTimedOut()))
            {
                Phase = LevelPhase.BossWarning;
                _phaseTime = 0f;
                return LevelEvent.BossWarning;
            }

            return LevelEvent.None;
        }

        bool LastWaveTimedOut() =>
            _waves.Length > 0 && _time - _startTime[_waves.Length - 1] >= MaxWaveDuration;

        bool ShouldStartNextWave()
        {
            if (_nextWave == 0) return _time >= _initialDelay;

            var last = _nextWave - 1;
            if (_clearedTime[last] >= 0f && _time >= _clearedTime[last] + GapAfterClear) return true;
            return _time - _startTime[last] >= MaxWaveDuration;
        }

        void SpawnDue(int wave, List<WaveSpawnRequest> spawns)
        {
            var groups = _waves[wave];
            for (var g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                var count = _counts[wave][g];
                while (_spawned[wave][g] < count)
                {
                    var member = _spawned[wave][g];
                    var due = _startTime[wave] + group.StartDelay + FormationLayout.Delay(group.Formation, member, group.Spacing);
                    if (_time < due) break;

                    FormationLayout.Slot(group.Formation, member, count, group.EntryHeight, out var y, out var x);
                    spawns.Add(new WaveSpawnRequest(wave, g, member, y, x));
                    _spawned[wave][g]++;
                    _outstanding[wave]++;
                }
            }
        }

        bool IsWaveDone(int wave)
        {
            if (_outstanding[wave] > 0) return false;
            for (var g = 0; g < _counts[wave].Length; g++)
                if (_spawned[wave][g] < _counts[wave][g]) return false;
            return true;
        }

        bool AllWavesDone()
        {
            for (var w = 0; w < _waves.Length; w++)
                if (!IsWaveDone(w)) return false;
            return true;
        }
    }
}
