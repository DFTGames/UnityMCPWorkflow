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
    /// Endless mode (GDD "Core Loop", Endless mode): cycles of ten waves and a boss,
    /// each one faster, fuller and worth more, with no Sector Clear between them and no way to win.
    /// </summary>
    public class EndlessSceneTests : LevelSceneFixture
    {
        protected override void ConfigureRun() => RunContext.Configure(Difficulty.Pilot, GameMode.Endless);

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
            Assert.That(Runner.IsEndless, Is.True, "the run was not started as an Endless one");
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

        static SkyView Sky => Object.FindAnyObjectByType<SkyView>();

        [UnityTest]
        public IEnumerator TheSkyDoesNotChangeWhenACycleTurns()
        {
            // The sky used to move on to the next level's backdrop the instant a boss died, which put a hard
            // cut at the busiest moment of the run and made a long run look like a playlist of levels. An
            // Endless run is one journey: nothing the player does may swap the sky between two frames.
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            var sky = Sky;
            var showing = sky.GetComponent<SpriteRenderer>();
            var before = showing.sprite;
            var beforeIndex = sky.Cycle.Showing;
            Assert.That(before, Is.Not.Null);

            yield return ClearTheCycle();

            Assert.That(showing.sprite, Is.EqualTo(before), "the sky cut to another one when the cycle turned");
            Assert.That(sky.Cycle.Showing, Is.EqualTo(beforeIndex), "the cycle moved the sky along");
        }

        [UnityTest]
        public IEnumerator ARunHoldsOnlyTheSkyItIsShowing()
        {
            // Holding the whole ring because a definition referenced it is what this replaced. A run keeps the
            // sky on screen, takes the next one shortly before its dissolve, and gives the old one back when
            // that dissolve ends. Counts here are the cache's, including anything still on its way in.
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            yield return null;

            var sky = Sky;
            var ring = sky.Cycle.SkyCount;
            Assert.That(ring, Is.GreaterThan(2), "this test means nothing on a ring of two");

            // Settled, well before the next dissolve: one sky and nothing else.
            Assert.That(sky.CacheCount, Is.EqualTo(1), "a settled run should hold exactly the sky on screen");
            Assert.That(sky.ArrivingAlpha, Is.EqualTo(0f).Within(0.01f), "the run did not open settled");

            // Halfway through a dissolve both are unavoidably needed, and no more.
            sky.Tick(sky.HoldSeconds + sky.FadeSeconds / 2f);
            yield return null;
            Assert.That(sky.CacheCount, Is.EqualTo(2), "a dissolve needs both of its skies");

            // Once it finishes, the sky that was left goes back and the run settles on one again.
            sky.Tick(sky.FadeSeconds);
            yield return null;
            Assert.That(sky.CacheCount, Is.EqualTo(1),
                "the sky the ring has finished leaving was never given back");

            // A whole lap does not accumulate.
            for (var i = 0; i < ring + 1; i++)
            {
                sky.Tick(sky.HoldSeconds + sky.FadeSeconds);
                yield return null;
            }

            Assert.That(sky.CacheCount, Is.EqualTo(1), "the ring leaks a sky every lap");
        }

        [UnityTest]
        public IEnumerator TheSkyDriftsThroughItsRing()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            yield return null;

            var sky = Sky;
            Assert.That(sky, Is.Not.Null, "the level scene has no sky view");
            Assert.That(sky.Cycle, Is.Not.Null, "the run never started the sky drifting");
            Assert.That(sky.Cycle.SkyCount, Is.GreaterThanOrEqualTo(2), "a ring needs more than one sky");

            var showing = sky.GetComponent<SpriteRenderer>();
            var first = showing.sprite;
            var arriving = sky.Cycle.Arriving;

            // Driven by hand rather than waited out: a sky is held for over a minute on purpose.
            sky.Tick(sky.HoldSeconds + sky.FadeSeconds / 2f);

            Assert.That(sky.ArrivingAlpha, Is.GreaterThan(0.2f).And.LessThan(0.8f),
                "halfway through a dissolve both skies should be on screen");
            Assert.That(showing.sprite, Is.EqualTo(first), "the sky being left stays until the dissolve ends");

            sky.Tick(sky.FadeSeconds);

            Assert.That(sky.Cycle.Showing, Is.EqualTo(arriving), "the dissolve never finished");
            Assert.That(showing.sprite, Is.Not.EqualTo(first), "the next sky is on screen now");
            Assert.That(sky.ArrivingAlpha, Is.EqualTo(0f).Within(0.01f), "and it is whole, not still arriving");
        }

        [UnityTest]
        public IEnumerator TheHudCountsTheCycle()
        {
            // The counter is a serialised reference, so the code being right is not enough: the scene has to
            // carry the label, and only an Endless run has any use for one.
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
