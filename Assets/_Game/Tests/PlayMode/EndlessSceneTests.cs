using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Gameplay;
using Object = UnityEngine.Object;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Endless mode played in its own scene (GDD "Core Loop", Endless mode): cycles of ten waves and a boss,
    /// each one faster, fuller and worth more, with no Sector Clear between them and no way to win.
    /// </summary>
    public class EndlessSceneTests : LevelSceneFixture
    {
        protected override string SceneName => "Endless";

        /// <summary>Beats the cycle's boss, and returns once the next cycle has started.</summary>
        IEnumerator ClearTheCycle()
        {
            var cycle = Runner.EndlessRun.Cycle;
            Runner.StartBossFightNow();

            var boss = Runner.Boss;
            Assert.That(boss, Is.Not.Null, "no boss for this cycle");
            yield return WaitUntil(() => boss.Brain != null && boss.Brain.IsCoreOpen, 14f, "the boss core to open");

            boss.TakeCoreHit(boss.Brain.Health.Max, 0);
            yield return WaitUntil(() => Runner.EndlessRun.Cycle > cycle, 3f, "the next cycle to start");
        }

        [Test]
        public void ItStartsAsAnEndlessRunOnCycleOne()
        {
            Assert.That(Runner.IsEndless, Is.True, "the Endless scene is not set up as one");
            Assert.That(Runner.EndlessRun, Is.Not.Null);
            Assert.That(Runner.EndlessRun.Cycle, Is.EqualTo(1));
            Assert.That(Runner.IsRunning, Is.True);
            Assert.That(Session.Score.EndlessMultiplier, Is.EqualTo(1f), "cycle 1 scores at face value");
        }

        [UnityTest]
        public IEnumerator ACycleSpawnsItsWaves()
        {
            yield return WaitUntil(() => Runner.Director.CurrentWave >= 0, 4f, "the first wave");
            yield return WaitUntil(() => CountLiveEnemies() + CountLiveMeteors() > 0, 6f, "hazards to arrive");
        }

        [UnityTest]
        public IEnumerator BeatingABossStartsTheNextCycleInsteadOfClearingTheSector()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            yield return ClearTheCycle();

            Assert.That(Runner.EndlessRun.Cycle, Is.EqualTo(2));
            Assert.That(Runner.IsSectorClear, Is.False, "Endless has no Sector Clear");
            Assert.That(Runner.IsRunning, Is.True, "and no way to win: the run carries on");
            Assert.That(Runner.Boss, Is.Not.Null, "the next cycle brings its own boss");
            Assert.That(Runner.Boss.IsAlive, Is.False, "which waits off screen until its waves are done");
        }

        [UnityTest]
        public IEnumerator EachCycleIsFasterFullerAndWorthMore()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            // What a Dart covers in half a second at cycle 1, measured rather than assumed: the run's own
            // numbers can be right while nothing on screen has been told about them. It has to be spawned by
            // the runner, because that is where the cycle's settings are applied.
            var first = SpawnThroughRunner("Dart", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y + 3f));
            var from = first.Position.x;
            yield return new WaitForSeconds(0.5f);
            var cycleOneTravel = from - first.Position.x;
            first.Despawn();

            yield return ClearTheCycle();

            var run = Runner.EndlessRun;
            Assert.That(run.EnemySpeedMultiplier, Is.EqualTo(1.10f).Within(1e-3f));
            Assert.That(run.EnemyCountMultiplier, Is.EqualTo(1.15f).Within(1e-3f));
            Assert.That(run.ScoreMultiplier, Is.EqualTo(1.25f).Within(1e-3f));
            Assert.That(Session.Score.EndlessMultiplier, Is.EqualTo(1.25f).Within(1e-3f),
                "and the session is told, or the score would not follow");

            var second = SpawnThroughRunner("Dart", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y + 3f));
            var secondFrom = second.Position.x;
            yield return new WaitForSeconds(0.5f);
            var cycleTwoTravel = secondFrom - second.Position.x;

            Assert.That(cycleTwoTravel, Is.GreaterThan(cycleOneTravel * 1.05f),
                "an enemy spawned in cycle 2 is faster, or the escalation never reached it");
        }

        [UnityTest]
        public IEnumerator ACycleWearsItsOwnSky()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            var backdrop = GameObject.Find("Background").GetComponent<SpriteRenderer>();
            var first = backdrop.sprite;
            Assert.That(first, Is.Not.Null);

            yield return ClearTheCycle();

            Assert.That(backdrop.sprite, Is.Not.Null);
            Assert.That(backdrop.sprite, Is.Not.EqualTo(first), "cycle 2 moves on to the next level's setting");
        }

        [UnityTest]
        public IEnumerator TheHudCountsTheCycle()
        {
            // The counter is a serialised reference, so the code being right is not enough: the scene has to
            // carry the label, and only the Endless scene has any use for one.
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            var label = GameObject.Find("Cycle");
            Assert.That(label, Is.Not.Null, "the Endless HUD has no cycle label");
            var text = label.GetComponent<TMPro.TMP_Text>();
            Assert.That(text.gameObject.activeInHierarchy, Is.True);
            Assert.That(text.text, Does.Contain("1"), "it starts on cycle 1");

            yield return ClearTheCycle();
            yield return null; // the HUD is written in Update

            Assert.That(text.text, Does.Contain("2"), "and follows the run into the next cycle");
        }

        [UnityTest]
        public IEnumerator TheRunEndsOnlyWhenThePlayerDoes()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            while (!Ship.IsGameOver)
            {
                Session.ReportPlayerHit(0, 1000f);
                yield return new WaitForSeconds(GameTuning.RespawnInvulnerabilitySeconds + 0.1f);
            }

            yield return new WaitForFixedUpdate();

            Assert.That(Runner.IsRunning, Is.False);
            Assert.That(Runner.IsSectorClear, Is.False, "an Endless run is never cleared, only ended");
            Assert.That(Runner.SecondsSinceRunEnded, Is.GreaterThanOrEqualTo(0f), "and the results are on their way");
        }

        [UnityTest]
        public IEnumerator TheResultsReportTheCycleReached()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            yield return ClearTheCycle(); // so the run has somewhere to have got to

            while (!Ship.IsGameOver)
            {
                Session.ReportPlayerHit(0, 1000f);
                yield return new WaitForSeconds(GameTuning.RespawnInvulnerabilitySeconds + 0.1f);
            }

            yield return WaitUntil(() => FindNote() != null, 6f, "the results panel");

            var note = FindNote();
            Assert.That(note.gameObject.activeInHierarchy, Is.True, "an Endless run has something to report");
            Assert.That(note.text, Does.Contain("2"), "the cycle it reached");
        }

        /// <summary>
        /// Spawns an enemy the way the game does, through the runner, so it is built with the cycle's own
        /// settings rather than the difficulty's. A test that news one up itself measures nothing about Endless.
        /// </summary>
        EnemyView SpawnThroughRunner(string prefabName, Vector2 position)
        {
            var prefab = LoadPrefab<EnemyView>(prefabName);
            Runner.LaunchBossMinion(prefab, position);

            EnemyView spawned = null;
            var nearest = float.MaxValue;
            foreach (var enemy in Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                if (!enemy.IsAlive || enemy.Definition != prefab.Definition) continue;

                var distance = Vector2.Distance(enemy.Position, position);
                if (distance >= nearest) continue;

                nearest = distance;
                spawned = enemy;
            }

            Assert.That(spawned, Is.Not.Null, "the runner did not spawn a " + prefabName);
            return spawned;
        }

        /// <summary>The note line on whichever results panel is open.</summary>
        static TMPro.TMP_Text FindNote()
        {
            foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
                if (text.name == "Note" && text.gameObject.activeInHierarchy) return text;

            return null;
        }

        static int CountLiveMeteors()
        {
            var count = 0;
            foreach (var meteor in Object.FindObjectsByType<MeteorView>()) if (meteor.IsAlive) count++;
            return count;
        }
    }
}
