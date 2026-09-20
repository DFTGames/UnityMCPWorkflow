using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Gameplay;
using NVector2 = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Integration tests for the Level01 scene: real physics, prefabs and the GameRunner wiring.
    /// The rules themselves are covered by the EditMode tests; these check that the Unity layer reports
    /// the right events. Prefabs are loaded through the AssetDatabase, so these tests run in the Editor only.
    /// </summary>
    public class Level01SceneTests : LevelSceneFixture
    {
        const string MeteorDir = "Assets/_Game/ScriptableObjects/Meteors/";

        [Test]
        public void Scene_StartsWithFreshSession()
        {
            Assert.That(Session, Is.Not.Null);
            Assert.That(Session.Settings.Difficulty, Is.EqualTo(Difficulty.Pilot));
            Assert.That(Runner.PlayerCount, Is.EqualTo(1));
            Assert.That(Ship.Vitals.Lives, Is.EqualTo(3));
            Assert.That(Session.Score.Score, Is.EqualTo(0));
            Assert.That(Runner.IsRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator FirstWave_BringsFiveDartsAfterTheOpeningDelay()
        {
            Assert.That(Runner.Director.CurrentWave, Is.EqualTo(-1));

            yield return WaitUntil(() => Runner.Director.CurrentWave == 0, 4f, "the first wave to start");
            yield return WaitUntil(() => CountLiveHazards() == 5, 4f, "the five Darts of wave 1");
            Assert.That(Runner.Director.Outstanding(0), Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator PlayerShots_DestroyADartAndScore()
        {
            Runner.SpawningEnabled = false;
            var dart = SpawnEnemy("Dart", ShipPosition + new Vector2(5f, 0f));
            Runner.SetCommandOverride(0, FireForward);

            yield return WaitUntil(() => Session.Score.Kills == 1, 3f, "the Dart to be shot down");

            Assert.That(Session.Score.Score, Is.EqualTo(150)); // 100 x Pilot 1.5
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f), "the Dart should die before reaching the ship");
            Assert.That(dart.IsAlive, Is.False);
        }

        [UnityTest]
        public IEnumerator DartRammingTheShip_DamagesItAndDies()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var dart = SpawnEnemy("Dart", ShipPosition + new Vector2(3f, 0f));

            yield return WaitUntil(() => Session.Score.Kills == 1, 3f, "the Dart to ram the ship");

            Assert.That(Ship.Vitals.Health, Is.EqualTo(80f).Within(1e-3f)); // 20 contact damage x Pilot 1.0
            Assert.That(Ship.Vitals.Lives, Is.EqualTo(3));
            Assert.That(dart.IsAlive, Is.False);
        }

        [UnityTest]
        public IEnumerator WeaverShot_HitsTheShip()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            SpawnEnemy("Weaver", ShipPosition + new Vector2(7f, 0f));

            yield return WaitUntil(() => Ship.Vitals.Health < 100f, 4f, "the Weaver's shot to land");

            Assert.That(Ship.Vitals.Health, Is.EqualTo(90f).Within(1e-3f)); // 10 bullet damage x Pilot 1.0
        }

        [UnityTest]
        public IEnumerator Shield_AbsorbsAnEnemyShot()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            Session.ReportPickupCollected(0, PickupType.Shield);
            Runner.FireEnemyProjectile(ShipPosition + new Vector2(3f, 0f), 6f, 10f);

            yield return WaitUntil(() => Ship.Shield.HitsRemaining < GameTuning.ShieldHits, 2f, "the shield to absorb the shot");

            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f));
            Assert.That(Ship.Shield.HitsRemaining, Is.EqualTo(GameTuning.ShieldHits - 1));
        }

        [UnityTest]
        public IEnumerator FlyingThroughAPickup_CollectsIt()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var pickup = Object.Instantiate(LoadPrefab<PickupView>("Pickup"));
            pickup.Init(Runner, PickupType.WeaponUpgrade, 2f, ShipPosition + new Vector2(1.5f, 0f), null);

            yield return WaitUntil(() => Ship.Weapon.Level == 2, 3f, "the pickup to be collected");

            Assert.That(pickup == null || !pickup.IsActive, Is.True, "the pickup should be removed once collected");
        }

        [UnityTest]
        public IEnumerator LargeSplittingMeteor_BreaksIntoTwoMediumOnes()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var large = LoadAsset<MeteorDefinition>(MeteorDir + "MeteorLarge.asset");
            var meteor = SpawnMeteor(large, ShipPosition + new Vector2(6f, 0f), new Vector2(-0.5f, 0f));

            meteor.TakeHit(large.MaxHealth, 0, meteor.Position);
            yield return new WaitForFixedUpdate();

            Assert.That(Session.Score.Kills, Is.EqualTo(1));
            Assert.That(Session.Score.Score, Is.EqualTo(75)); // 50 x Pilot 1.5
            Assert.That(CountLiveMeteors(MeteorSize.Medium), Is.EqualTo(MeteorRules.FragmentsPerSplit));
            foreach (var fragment in Object.FindObjectsByType<MeteorView>())
            {
                if (!fragment.IsAlive || fragment.Definition.Size != MeteorSize.Medium) continue;
                var speed = fragment.Velocity.magnitude;
                Assert.That(speed, Is.InRange(fragment.Definition.MinSpeed, fragment.Definition.MaxSpeed),
                    "fragments use their own definition's speed range");
            }
        }

        [UnityTest]
        public IEnumerator RammingAMeteor_DestroysItWithoutSplitting()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var large = LoadAsset<MeteorDefinition>(MeteorDir + "MeteorLarge.asset");
            SpawnMeteor(large, ShipPosition + new Vector2(2.5f, 0f), new Vector2(-3f, 0f));

            yield return WaitUntil(() => Session.Score.Kills == 1, 3f, "the meteor to hit the ship");

            Assert.That(Ship.Vitals.Health, Is.EqualTo(75f).Within(1e-3f)); // 25 contact damage x Pilot 1.0
            Assert.That(CountLiveMeteors(MeteorSize.Medium), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator GameOver_HidesTheShipAndLaterKillsDoNotScore()
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
            Assert.That(ShipView.gameObject.activeSelf, Is.False);

            var dart = SpawnEnemy("Dart", new Vector2(0f, 0f));
            dart.TakeHit(100f, 0, dart.Position);
            Assert.That(Session.Score.Kills, Is.EqualTo(0));
            Assert.That(Session.Score.Score, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PlayerShotsLeavingTheScreen_ReturnToThePool()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, FireForward);
            yield return WaitUntil(() => Runner.ActivePlayerProjectiles > 0, 1f, "shots to be fired");

            Runner.SetCommandOverride(0, Idle);
            yield return WaitUntil(() => Runner.ActivePlayerProjectiles == 0, 3f, "shots to leave the screen");
        }

        [UnityTest]
        public IEnumerator Ship_StopsAtTheInsetPlayfieldEdge()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, new PlayerCommand(new NVector2(-1f, 1f), false, NVector2.UnitX));

            yield return new WaitForSeconds(2f);

            var field = Runner.Playfield;
            const float margin = 0.5f; // PlayerDefinition edge margin, GDD "Mechanics"
            Assert.That(ShipPosition.x, Is.EqualTo(field.MinX + margin).Within(0.01f));
            Assert.That(ShipPosition.y, Is.EqualTo(field.MaxY - margin).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator PlayerEngine_FollowsActualMovement()
        {
            Runner.SpawningEnabled = false;
            var engine = ShipView.Engine;
            Assert.That(engine, Is.Not.Null, "player ship has no engine exhaust");
            var particles = engine.GetComponent<ParticleSystem>();
            Assert.That(engine.Throttle, Is.EqualTo(engine.IdleThrottle).Within(0.05f), "starts at idle");
            var idleMaxSize = particles.main.startSize.constantMax;
            var idleRate = particles.emission.rateOverTime.constant;

            // Moving down from the centre: full throttle, bigger and denser exhaust.
            Runner.SetCommandOverride(0, new PlayerCommand(-NVector2.UnitY, false, NVector2.UnitX));
            yield return WaitUntil(() => engine.Throttle > 0.95f, 2f, "the engine to reach full throttle");
            Assert.That(particles.main.startSize.constantMax, Is.GreaterThan(idleMaxSize));
            Assert.That(particles.emission.rateOverTime.constant, Is.GreaterThan(idleRate));
            Assert.That(particles.velocityOverLifetime.x.constant, Is.LessThan(0f), "exhaust flows out of the rear");

            // Still pushing down but pinned at the bottom edge: the ship is not moving, so the engine idles.
            yield return WaitUntil(() => engine.Throttle < engine.IdleThrottle + 0.05f, 4f,
                "the engine to settle while the ship is pinned at the edge");
            Assert.That(ShipPosition.y, Is.EqualTo(Runner.Playfield.MinY + 0.5f).Within(0.01f));
        }

        /// <summary>
        /// How long a boss takes to reach its station and open its core for the first time: it flies in from
        /// beyond the right edge before it does anything, then holds its armoured phase.
        /// </summary>
        const float BossEntryAndFirstCycle = 10f;

        [UnityTest]
        public IEnumerator Boss_ClosedCoreArmourAbsorbsShots()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            Runner.StartBossFightNow();
            var boss = Runner.Boss;
            var core = boss.GetComponentInChildren<BossCoreView>();

            // Aim above the core, at the upper armour, while the boss flies in: during the entry it only moves
            // horizontally, so aimed shots land (once it drifts vertically they would trail behind it).
            while (!boss.Brain.IsCoreOpen && boss.ArmourHits == 0)
            {
                AimAt(core.transform.position + Vector3.up * 1.0f);
                yield return null;
            }

            Assert.That(boss.ArmourHits, Is.GreaterThan(0), "shots reached the armour");
            Assert.That(boss.Brain.Health.Current, Is.EqualTo(boss.Brain.Health.Max));
        }

        [UnityTest]
        public IEnumerator Boss_OpenCoreTakesDamageFromShots()
        {
            Runner.SpawningEnabled = false;
            Runner.StartBossFightNow();
            var boss = Runner.Boss;
            var core = boss.GetComponentInChildren<BossCoreView>();
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, BossEntryAndFirstCycle, "the core to open");

            for (var t = 0f; t < 2f && boss.Brain.Health.Current >= boss.Brain.Health.Max; t += Time.deltaTime)
            {
                AimAt(core.transform.position);
                yield return null;
            }

            Assert.That(boss.Brain.Health.Current, Is.LessThan(boss.Brain.Health.Max));
        }

        [UnityTest]
        public IEnumerator Boss_LaunchesDartsThatBelongToNoWave()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            Runner.StartBossFightNow();

            yield return WaitUntil(() => CountLiveEnemies() >= 3, 6f, "the boss to launch three Darts");
            foreach (var enemy in Object.FindObjectsByType<EnemyView>())
                if (enemy.IsAlive) Assert.That(enemy.WaveIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator Boss_Defeat_AwardsExactScoreAndClearsTheSector()
        {
            Runner.SpawningEnabled = false;
            // Park at the top edge, out of the path of the launched Darts.
            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.UnitY, false, NVector2.UnitX));
            Runner.StartBossFightNow();
            var boss = Runner.Boss;
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, BossEntryAndFirstCycle, "the core to open");
            Runner.SetCommandOverride(0, Idle);
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f), "precondition: no damage taken during the fight");
            var before = Session.Score.Score;

            boss.TakeCoreHit(boss.Brain.Health.Max, 0);
            yield return new WaitForFixedUpdate();

            Assert.That(boss.IsAlive, Is.False);
            Assert.That(Runner.IsBossDefeated, Is.True);
            Assert.That(Session.Score.Score - before, Is.EqualTo(15000 + 7500)); // (10,000 + 5,000 no-damage) x Pilot 1.5
            var drops = Object.FindObjectsByType<PickupView>();
            Assert.That(drops, Has.Some.Matches<PickupView>(p => p.IsActive && p.Type == PickupType.WeaponUpgrade));
            Assert.That(drops, Has.Some.Matches<PickupView>(p => p.IsActive && p.Type == PickupType.Health));

            yield return WaitUntil(() => Runner.IsSectorClear, 4f, "the sector to be cleared");
            Assert.That(Runner.IsRunning, Is.False);
            Assert.That(Session.Score.Score - before, Is.EqualTo(15000 + 7500 + 3000)); // + level clear 2,000 x 1.5
        }

        [UnityTest]
        public IEnumerator AfterBossDefeat_ThePlayerCannotBeHurt()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            Runner.StartBossFightNow();
            var boss = Runner.Boss;
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, BossEntryAndFirstCycle, "the core to open");
            boss.TakeCoreHit(boss.Brain.Health.Max, 0);
            var health = Ship.Vitals.Health;

            Runner.FireEnemyProjectile(ShipPosition + new Vector2(2f, 0f), 8f, 10f);
            SpawnEnemy("Dart", ShipPosition + new Vector2(2f, 0f));
            yield return new WaitForSeconds(1f);

            Assert.That(Ship.Vitals.Health, Is.EqualTo(health));
            yield return WaitUntil(() => Runner.IsSectorClear, 4f, "the sector to be cleared");
        }

        [UnityTest]
        public IEnumerator Ship_TiltsTowardsTheAimWhileFiringAndLevelsOff()
        {
            Runner.SpawningEnabled = false;
            var visual = ShipView.transform.Find("Visual");
            Assert.That(ShipView.TiltDegrees, Is.EqualTo(0f));

            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.Zero, true, NVector2.UnitY));
            yield return WaitUntil(() => ShipView.TiltDegrees > 30f, 2f, "the ship to tilt up");
            Assert.That(Mathf.DeltaAngle(0f, visual.localEulerAngles.z), Is.EqualTo(ShipView.TiltDegrees).Within(0.5f));

            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.Zero, true, -NVector2.UnitY));
            yield return WaitUntil(() => ShipView.TiltDegrees < -30f, 2f, "the ship to tilt down");
            Assert.That(Mathf.DeltaAngle(0f, visual.localEulerAngles.z), Is.EqualTo(ShipView.TiltDegrees).Within(0.5f));

            Runner.SetCommandOverride(0, Idle);
            yield return WaitUntil(() => Mathf.Abs(ShipView.TiltDegrees) < 0.5f, 2f, "the ship to level off");
        }

        [UnityTest]
        public IEnumerator AimingBackwards_StillFiresForward()
        {
            Runner.SpawningEnabled = false;
            // The ship starts against the left edge, where anything "behind" it would be off the playfield and a
            // backwards shot would despawn before reaching it: move out first so the rear target is truly in range.
            yield return MoveShipAwayFromTheLeftEdge();

            var behindPosition = ShipPosition + new Vector2(-3f, 0f);
            Assert.That(Runner.Playfield.Contains(behindPosition.ToNumerics()), Is.True,
                "the rear target must sit inside the playfield for this test to mean anything");
            var behind = SpawnEnemy("Dart", behindPosition);
            var ahead = SpawnEnemy("Dart", ShipPosition + new Vector2(5f, 0f));
            behind.enabled = false; // hold it still behind the ship
            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.Zero, true, -NVector2.UnitX));

            yield return WaitUntil(() => !ahead.IsAlive, 3f, "the Dart ahead to be shot down");

            Assert.That(behind.IsAlive, Is.True, "a side-scroller never fires behind the ship");
            Assert.That(ShipView.TiltDegrees, Is.EqualTo(0f).Within(0.5f), "aiming straight back keeps the ship level");
        }

        [UnityTest]
        public IEnumerator SectorClear_WhileFiring_LetsTheShipLevelOff()
        {
            Runner.SpawningEnabled = false;
            Runner.StartBossFightNow();
            var boss = Runner.Boss;
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, BossEntryAndFirstCycle, "the core to open");

            Runner.SetCommandOverride(0, new PlayerCommand(NVector2.Zero, true, NVector2.UnitY));
            yield return WaitUntil(() => ShipView.TiltDegrees > 30f, 2f, "the ship to tilt up");
            boss.TakeCoreHit(boss.Brain.Health.Max, 0);

            yield return WaitUntil(() => Runner.IsSectorClear, 4f, "the sector to be cleared");
            yield return WaitUntil(() => Mathf.Abs(ShipView.TiltDegrees) < 0.5f, 2f, "the ship to level off after the clear");
        }

        // ---- Level 1 helpers ----

        MeteorView SpawnMeteor(MeteorDefinition definition, Vector2 position, Vector2 velocity)
        {
            var meteor = Object.Instantiate(LoadPrefab<MeteorView>("Meteor"));
            meteor.Init(Runner, definition, position, velocity, 0f, null);
            return meteor;
        }

        static int CountLiveHazards()
        {
            var count = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyView>()) if (enemy.IsAlive) count++;
            foreach (var meteor in Object.FindObjectsByType<MeteorView>()) if (meteor.IsAlive) count++;
            return count;
        }

        static int CountLiveMeteors(MeteorSize size)
        {
            var count = 0;
            foreach (var meteor in Object.FindObjectsByType<MeteorView>())
                if (meteor.IsAlive && meteor.Definition.Size == size) count++;
            return count;
        }
    }
}
