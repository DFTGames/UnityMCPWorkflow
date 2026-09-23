using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Gameplay;
using NVector2 = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The boss moves that only exist once a boss is in a scene: a dash, a sweeping beam, hurled meteors, a
    /// turret volley and a gravity pull (GDD "Levels"). The schedules behind them are covered by the EditMode
    /// tests; these check that the actions reach the playfield at all. Each boss is spawned into the Level 1
    /// scene rather than loading its own level, so the fixture stays one scene.
    /// </summary>
    public class BossActionSceneTests : LevelSceneFixture
    {
        const string BossPrefabDir = "Assets/_Game/Prefabs/";

        /// <summary>Puts a boss on the field in place of the level's own, and waits for it to reach its station.</summary>
        IEnumerator SpawnBoss(string prefabName, System.Action<BossView> spawned)
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            var prefab = LoadAsset<GameObject>(BossPrefabDir + prefabName + ".prefab").GetComponent<BossView>();

            // Standing in for the runner: a boss takes its art from the cache, and the cache holds only what
            // it has been asked for, so a test putting its own boss on the field has to ask for its art.
            Runner.BossContent.Require<Sprite>(new[] { prefab.Definition.SpritePath });

            var boss = Object.Instantiate(prefab);
            boss.Init(Runner, Session.Settings, new Vector2(Runner.Playfield.MaxX + 3f, ShipPosition.y));

            Assert.That(boss.BodySprite, Is.Not.Null, prefabName + " arrived with no art");

            // It does nothing until it has flown in; everything these tests look for happens after that.
            yield return WaitUntil(() => HasArrived(boss), 8f, prefabName + " to reach its station");
            spawned(boss);
        }

        /// <summary>Where a boss stops: its hold inset in from the right edge.</summary>
        bool HasArrived(BossView boss) =>
            boss.Position.x <= Runner.Playfield.MaxX - boss.Definition.HoldInset + 0.05f;

        static int CountEnemyShots()
        {
            var count = 0;
            foreach (var shot in Object.FindObjectsByType<Projectile>())
                if (shot.IsActive && shot.OwnerIndex < 0) count++;
            return count;
        }

        static int CountLiveMeteors()
        {
            var count = 0;
            foreach (var meteor in Object.FindObjectsByType<MeteorView>()) if (meteor.IsAlive) count++;
            return count;
        }

        [UnityTest]
        public IEnumerator TheRockCrusher_ThrowsMeteorsThatAreRealHazards()
        {
            BossView boss = null;
            yield return SpawnBoss("RockCrusher", b => boss = b);

            yield return WaitUntil(() => CountLiveMeteors() > 0, 6f, "the Rock Crusher to throw a rock");

            var thrown = Object.FindAnyObjectByType<MeteorView>();
            Assert.That(thrown.WaveIndex, Is.EqualTo(-1), "a thrown rock belongs to no wave, or it would hold one open");
            Assert.That(thrown.Velocity.x, Is.LessThan(0f), "and it is thrown towards the player, not away");
            Assert.That(boss.IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator TheFrostLancer_WindsUpThenCrossesTheScreen()
        {
            BossView boss = null;
            yield return SpawnBoss("FrostLancer", b => boss = b);

            yield return WaitUntil(() => boss.DashPhase == BossDash.DashPhase.WindUp, 6f, "the Frost Lancer to wind up");
            var held = boss.Position;
            yield return new WaitForSeconds(0.3f);
            Assert.That(boss.Position.x, Is.EqualTo(held.x).Within(0.01f), "the wind-up is the tell: it holds still");

            yield return WaitUntil(() => boss.DashPhase == BossDash.DashPhase.Dashing, 2f, "the dash");
            yield return WaitUntil(() => boss.Position.x < Runner.Playfield.MinX + 2f, 3f, "it to cross the screen");

            yield return WaitUntil(() => boss.DashPhase == null, 6f, "it to return to its station");
            Assert.That(boss.Position.x, Is.GreaterThan(Runner.Playfield.MinX + 4f), "and it comes back");
        }

        [UnityTest]
        public IEnumerator TheSunforge_SweepsABeamThatBurnsTheShip()
        {
            BossView boss = null;
            yield return SpawnBoss("Sunforge", b => boss = b);

            yield return WaitUntil(() => boss.IsSweeping, 8f, "the Sunforge to light its beam");

            // The ship is parked in the beam's arc, so the sweep has to find it.
            yield return WaitUntil(() => Ship.Vitals.Health < 100f, 4f, "the beam to burn the ship");
            Assert.That(Ship.Vitals.Health, Is.LessThanOrEqualTo(100f - boss.Definition.BeamDamage + 0.01f));
        }

        [UnityTest]
        public IEnumerator TheDreadnought_FiresFromEveryTurretAtOnce()
        {
            BossView boss = null;
            yield return SpawnBoss("Dreadnought", b => boss = b);

            yield return WaitUntil(() => CountEnemyShots() >= 3, 8f, "a turret volley");
            Assert.That(boss.Brain.IsCoreOpen, Is.False, "the volley belongs to its armoured half");
        }

        [UnityTest]
        public IEnumerator TheSingularityEngine_DragsTheShipTowardsItButCannotHoldIt()
        {
            BossView boss = null;
            yield return SpawnBoss("SingularityEngine", b => boss = b);

            // Standing still, the pull is the only thing moving the ship.
            var before = ShipPosition.x;
            yield return WaitUntil(() => ShipPosition.x > before + 0.2f, 8f, "the well to drag the ship in");
            Assert.That(Runner.Playfield.Contains(ShipPosition.ToNumerics()), Is.True,
                "it never drags the ship off the playfield");

            // Flying away must still work: the pull is half the ship's top speed (GDD Level 07).
            var pulled = ShipPosition.x;
            Runner.SetCommandOverride(0, new PlayerCommand(-NVector2.UnitX, false, NVector2.UnitX));
            yield return new WaitForSeconds(0.6f);
            Assert.That(ShipPosition.x, Is.LessThan(pulled), "the player can still fly out of it");
            Assert.That(boss.IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator TheOvermind_ChangesWhatItDoesAsItIsWornDown()
        {
            BossView boss = null;
            yield return SpawnBoss("Overmind", b => boss = b);

            // Its first third launches Divers; the last one dashes. Wearing it down should change the fight.
            yield return WaitUntil(() => boss.Brain.IsCoreOpen, 12f, "its core to open");
            boss.TakeCoreHit(boss.Brain.Health.Max * 0.75f, 0);
            Assert.That(boss.Brain.HealthFraction, Is.LessThan(0.33f));

            yield return WaitUntil(() => boss.DashPhase != null, 14f, "the Overmind to start dashing");
            Assert.That(boss.IsAlive, Is.True, "it is not finished yet");
        }
    }
}
