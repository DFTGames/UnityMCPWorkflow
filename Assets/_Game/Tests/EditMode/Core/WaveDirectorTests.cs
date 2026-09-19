using System;
using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class FormationLayoutTests
    {
        [Test]
        public void Line_SpawnsOneAfterAnotherAtEntryHeight()
        {
            Assert.That(FormationLayout.Delay(Formation.Line, 3, 0.5f), Is.EqualTo(1.5f));
            FormationLayout.Slot(Formation.Line, 3, 5, 0.4f, out var y, out var x);
            Assert.That(y, Is.EqualTo(0.4f));
            Assert.That(x, Is.EqualTo(0f));
        }

        [Test]
        public void Column_SpawnsTogetherStackedAroundEntryHeight()
        {
            Assert.That(FormationLayout.Delay(Formation.Column, 3, 0.5f), Is.EqualTo(0f));
            FormationLayout.Slot(Formation.Column, 0, 4, 0.5f, out var bottom, out _);
            FormationLayout.Slot(Formation.Column, 3, 4, 0.5f, out var top, out _);

            Assert.That(bottom, Is.EqualTo(0.5f - 1.5f * FormationLayout.ColumnStep).Within(1e-6f));
            Assert.That(top, Is.EqualTo(0.5f + 1.5f * FormationLayout.ColumnStep).Within(1e-6f));
        }

        [Test]
        public void V_LeaderInFrontWingsBehind()
        {
            FormationLayout.Slot(Formation.V, 3, 7, 0.5f, out var leaderY, out var leaderX);
            FormationLayout.Slot(Formation.V, 0, 7, 0.5f, out var wingY, out var wingX);

            Assert.That(leaderX, Is.EqualTo(0f));
            Assert.That(leaderY, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(wingX, Is.EqualTo(3f * FormationLayout.VStepX).Within(1e-6f));
            Assert.That(wingY, Is.LessThan(leaderY));
        }

        [Test]
        public void Staggered_AlternatesAboveAndBelow()
        {
            FormationLayout.Slot(Formation.Staggered, 0, 3, 0.5f, out var first, out _);
            FormationLayout.Slot(Formation.Staggered, 1, 3, 0.5f, out var second, out _);

            Assert.That(first, Is.EqualTo(0.5f + FormationLayout.StaggerOffset).Within(1e-6f));
            Assert.That(second, Is.EqualTo(0.5f - FormationLayout.StaggerOffset).Within(1e-6f));
        }

        [Test]
        public void Scatter_SpreadsMembersAcrossTheBandOneAtATime()
        {
            Assert.That(FormationLayout.Delay(Formation.Scatter, 2, 0.6f), Is.EqualTo(1.2f).Within(1e-6f));

            var heights = new List<float>();
            for (var i = 0; i < 4; i++)
            {
                FormationLayout.Slot(Formation.Scatter, i, 4, 0.5f, out var y, out var x);
                Assert.That(x, Is.EqualTo(0f));
                Assert.That(y, Is.InRange(FormationLayout.ScatterMargin, 1f - FormationLayout.ScatterMargin));
                heights.Add(y);
            }

            heights.Sort();
            for (var i = 1; i < heights.Count; i++)
                Assert.That(heights[i] - heights[i - 1], Is.GreaterThan(0.1f), "members spread out, not stacked");
        }

        [TestCase(Formation.Line, true)]
        [TestCase(Formation.Staggered, true)]
        [TestCase(Formation.Scatter, true)]
        [TestCase(Formation.Column, false)]
        [TestCase(Formation.V, false)]
        public void IsSequential(Formation formation, bool expected)
        {
            Assert.That(FormationLayout.IsSequential(formation), Is.EqualTo(expected));
        }

        [Test]
        public void Slot_ClampsToSpawnBand()
        {
            FormationLayout.Slot(Formation.Column, 0, 10, 0.05f, out var y, out _);

            Assert.That(y, Is.EqualTo(0f));
        }

        [TestCase(5, 1.0f, 5)]
        [TestCase(5, 0.75f, 4)] // 3.75
        [TestCase(5, 1.3f, 7)]  // 6.5 rounds away from zero
        [TestCase(1, 0.75f, 1)]
        [TestCase(1, 0.1f, 1)]
        public void ScaledCount_RoundsAndNeverDropsBelowOne(int count, float multiplier, int expected)
        {
            Assert.That(FormationLayout.ScaledCount(count, multiplier), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FormationLayout.Slot(Formation.Line, 2, 2, 0.5f, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => FormationLayout.Slot(Formation.Line, 0, 0, 0.5f, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => FormationLayout.Delay((Formation)99, 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FormationLayout.ScaledCount(3, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnGroupSpec(0, Formation.Line, 0.5f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnGroupSpec(1, Formation.Line, 0.5f, -1f, 0f));
        }
    }

    public class WaveDirectorTests
    {
        const float Step = 0.02f;

        static SpawnGroupSpec Group(int count, Formation formation = Formation.Line, float spacing = 0.5f,
            float delay = 0f) => new SpawnGroupSpec(count, formation, 0.5f, spacing, delay);

        static WaveDirector Director(float multiplier, params SpawnGroupSpec[][] waves) =>
            new WaveDirector(waves, multiplier, initialDelay: 1f);

        /// <summary>Ticks in fixed steps for <paramref name="seconds"/>, collecting spawns and returning the last non-None event.</summary>
        static LevelEvent Run(WaveDirector director, float seconds, List<WaveSpawnRequest> spawns)
        {
            var last = LevelEvent.None;
            var steps = (int)Math.Round(seconds / Step);
            for (var i = 0; i < steps; i++)
            {
                var e = director.Tick(Step, spawns);
                if (e != LevelEvent.None) last = e;
            }

            return last;
        }

        static void ClearAll(WaveDirector director, List<WaveSpawnRequest> spawned)
        {
            foreach (var s in spawned) director.NotifyHazardGone(s.WaveIndex);
            spawned.Clear();
        }

        [Test]
        public void FirstWave_StartsAfterInitialDelay()
        {
            var director = Director(1f, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();

            Run(director, 0.9f, spawns);
            Assert.That(spawns, Is.Empty);

            Run(director, 0.2f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(1));
            Assert.That(director.CurrentWave, Is.EqualTo(0));
        }

        [Test]
        public void LineGroup_SpawnsMembersAtSpacing()
        {
            var director = Director(1f, new[] { Group(3, Formation.Line, 0.5f) });
            var spawns = new List<WaveSpawnRequest>();

            Run(director, 1.1f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(1));
            Run(director, 0.5f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(2));
            Run(director, 0.5f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(3));
            Assert.That(spawns.ConvertAll(s => s.MemberIndex), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void GroupStartDelay_IsHonoured()
        {
            var director = Director(1f, new[] { Group(1), Group(1, delay: 2f) });
            var spawns = new List<WaveSpawnRequest>();

            Run(director, 1.1f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(1));
            Run(director, 2f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(2));
            Assert.That(spawns[1].GroupIndex, Is.EqualTo(1));
        }

        [Test]
        public void NextWave_StartsThreeSecondsAfterClear()
        {
            var director = Director(1f, new[] { Group(2, Formation.Column) }, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            Run(director, 1.1f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(2));

            ClearAll(director, spawns);
            Run(director, WaveDirector.GapAfterClear - 0.2f, spawns);
            Assert.That(spawns, Is.Empty, "gap after clear not respected");

            Run(director, 0.4f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(1));
            Assert.That(spawns[0].WaveIndex, Is.EqualTo(1));
        }

        [Test]
        public void NextWave_StartsAtTheCapEvenIfNotCleared()
        {
            var director = Director(1f, new[] { Group(1) }, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            Run(director, 1.1f, spawns);
            spawns.Clear();

            Run(director, WaveDirector.MaxWaveDuration - 0.2f, spawns);
            Assert.That(spawns, Is.Empty);

            Run(director, 0.4f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(1));
            Assert.That(spawns[0].WaveIndex, Is.EqualTo(1));
        }

        [Test]
        public void Fragments_KeepTheirWaveOpen()
        {
            var director = Director(1f, new[] { Group(1) }, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            Run(director, 1.1f, spawns);

            director.NotifyHazardAdded(0);
            director.NotifyHazardAdded(0);
            director.NotifyHazardGone(0);
            Assert.That(director.Outstanding(0), Is.EqualTo(2));

            spawns.Clear();
            Run(director, 5f, spawns);
            Assert.That(spawns, Is.Empty, "wave 0 still has fragments in play");
        }

        [Test]
        public void CountMultiplier_ScalesGroups()
        {
            var director = Director(1.3f, new[] { Group(5, Formation.Column) });
            var spawns = new List<WaveSpawnRequest>();

            Run(director, 1.1f, spawns);

            Assert.That(director.ScaledCount(0, 0), Is.EqualTo(7));
            Assert.That(spawns.Count, Is.EqualTo(7));
        }

        [Test]
        public void AfterLastWaveCleared_WarnsThenBossArrives()
        {
            var director = Director(1f, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            Run(director, 1.1f, spawns);
            ClearAll(director, spawns);

            Assert.That(Run(director, Step * 2, spawns), Is.EqualTo(LevelEvent.BossWarning));
            Assert.That(director.Phase, Is.EqualTo(LevelPhase.BossWarning));

            Assert.That(Run(director, WaveDirector.BossWarningDuration + 0.1f, spawns), Is.EqualTo(LevelEvent.BossArrives));
            Assert.That(director.Phase, Is.EqualTo(LevelPhase.Boss));

            director.NotifyBossDefeated();
            Assert.That(director.Phase, Is.EqualTo(LevelPhase.Cleared));
        }

        [Test]
        public void Boss_ArrivesAtTheCapEvenIfTheLastWaveNeverClears()
        {
            var director = Director(1f, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            Run(director, 1.1f, spawns);

            Assert.That(Run(director, WaveDirector.MaxWaveDuration - 0.5f, spawns), Is.EqualTo(LevelEvent.None));
            Assert.That(Run(director, 1f, spawns), Is.EqualTo(LevelEvent.BossWarning));
        }

        [Test]
        public void SectorClear_FollowsTheBossDefeatAfterTheDelay()
        {
            var director = Director(1f, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            director.SkipToBoss();
            director.NotifyBossDefeated();
            Assert.That(director.IsSectorClear, Is.False);

            Assert.That(Run(director, WaveDirector.SectorClearDelay - 0.1f, spawns), Is.EqualTo(LevelEvent.None));
            Assert.That(Run(director, 0.2f, spawns), Is.EqualTo(LevelEvent.SectorClear));
            Assert.That(director.IsSectorClear, Is.True);
            Assert.That(Run(director, 5f, spawns), Is.EqualTo(LevelEvent.None), "Sector Clear fires once");
        }

        [Test]
        public void Boss_WaitsForEveryWaveIncludingOverlappingOnes()
        {
            var director = Director(1f, new[] { Group(1) }, new[] { Group(1) });
            var spawns = new List<WaveSpawnRequest>();
            Run(director, WaveDirector.MaxWaveDuration + 1.5f, spawns);
            Assert.That(spawns.Count, Is.EqualTo(2));

            director.NotifyHazardGone(1);
            Assert.That(Run(director, 1f, spawns), Is.EqualTo(LevelEvent.None), "wave 0 is still alive");

            director.NotifyHazardGone(0);
            Assert.That(Run(director, Step, spawns), Is.EqualTo(LevelEvent.BossWarning));
        }

        [Test]
        public void NoWaves_GoesStraightToBossWarning()
        {
            var director = new WaveDirector(new SpawnGroupSpec[0][], 1f, 0f);

            Assert.That(director.Tick(Step, new List<WaveSpawnRequest>()), Is.EqualTo(LevelEvent.BossWarning));
        }

        [Test]
        public void NotifyHazardGone_IgnoresUnknownWavesAndNeverGoesNegative()
        {
            var director = Director(1f, new[] { Group(1) });

            director.NotifyHazardGone(-1);
            director.NotifyHazardGone(5);
            director.NotifyHazardGone(0);

            Assert.That(director.Outstanding(0), Is.EqualTo(0));
        }

        [Test]
        public void SkipToBoss_EntersBossPhaseAndSpawnsNothingMore()
        {
            var director = Director(1f, new[] { Group(3) });
            var spawns = new List<WaveSpawnRequest>();

            director.SkipToBoss();
            Run(director, 5f, spawns);

            Assert.That(director.Phase, Is.EqualTo(LevelPhase.Boss));
            Assert.That(spawns, Is.Empty);
            director.NotifyBossDefeated();
            Assert.That(director.Phase, Is.EqualTo(LevelPhase.Cleared));
        }

        [Test]
        public void SkipToBoss_AfterClear_DoesNotReopenTheLevel()
        {
            var director = Director(1f, new[] { Group(1) });
            director.SkipToBoss();
            director.NotifyBossDefeated();

            director.SkipToBoss();

            Assert.That(director.Phase, Is.EqualTo(LevelPhase.Cleared));
        }

        [Test]
        public void NotifyBossDefeated_BeforeBoss_IsIgnored()
        {
            var director = Director(1f, new[] { Group(1) });

            director.NotifyBossDefeated();

            Assert.That(director.Phase, Is.EqualTo(LevelPhase.Waves));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new WaveDirector(null, 1f));
            Assert.Throws<ArgumentException>(() => new WaveDirector(new[] { new SpawnGroupSpec[0] }, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveDirector(new[] { new[] { Group(1) } }, 1f, -1f));
            var director = Director(1f, new[] { Group(1) });
            Assert.Throws<ArgumentOutOfRangeException>(() => director.Tick(-1f, new List<WaveSpawnRequest>()));
            Assert.Throws<ArgumentNullException>(() => director.Tick(Step, null));
        }
    }
}
