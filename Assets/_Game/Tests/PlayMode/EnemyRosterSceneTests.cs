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
    /// The six enemies added after Level 1's Dart and Weaver, played in the real scene (GDD "Enemies and
    /// Hazards"). The behaviours themselves are covered by the EditMode tests; these check that the prefabs and
    /// the runner wire them up: that a Gunship really stops and fires bursts, a mine really hurts the ship.
    /// </summary>
    public class EnemyRosterSceneTests : LevelSceneFixture
    {
        [UnityTest]
        public IEnumerator Gunship_StopsInTheRightThirdAndFiresBurstsOfThree()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var field = Runner.Playfield;
            var gunship = SpawnEnemy("Gunship", new Vector2(field.MaxX - 0.5f, ShipPosition.y));
            var station = HoldingPattern.StationFor(field, gunship.Definition.StationFromRight);

            yield return WaitUntil(() => gunship.Position.x <= station + 0.05f, 6f, "the Gunship to reach its station");
            yield return new WaitForSeconds(0.3f); // the last fraction of a unit onto the station itself
            var stopped = gunship.Position;
            yield return new WaitForSeconds(0.5f);

            Assert.That(gunship.Position.x, Is.EqualTo(stopped.x).Within(0.001f), "it holds its station to shoot");
            Assert.That(gunship.Position.x, Is.EqualTo(station).Within(0.01f), "the right third of the screen");
            Assert.That(station, Is.LessThan(field.MaxX), "the station is on screen");

            // Three shots close together, then a long wait: the burst should be complete well before the gap ends.
            yield return WaitUntil(() => CountEnemyShots() >= 1, 3f, "the first shot of a burst");
            yield return new WaitForSeconds(gunship.Definition.BurstShotInterval * 3f);
            Assert.That(CountEnemyShots(), Is.EqualTo(3), "a burst is three shots");
        }

        [UnityTest]
        public IEnumerator Diver_CommitsToTheLineItLockedAndCannotFollowTheShip()
        {
            Runner.SpawningEnabled = false;
            yield return MoveShipAwayFromTheLeftEdge();
            var diver = SpawnEnemy("Diver", ShipPosition + new Vector2(7f, 3f));

            yield return WaitUntil(() => diver.DiverPhase == DivePhase.Locking, 3f, "the Diver to stop and lock on");
            var whileLocking = diver.Position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(diver.Position, Is.EqualTo(whileLocking), "holding still is the tell");

            yield return WaitUntil(() => diver.DiverPhase == DivePhase.Charging, 2f, "the Diver to charge");
            var aim = diver.Position;

            // Fly away from the line it committed to: it must not follow.
            Runner.SetCommandOverride(0, new PlayerCommand(-NVector2.UnitY, false, NVector2.UnitX));
            yield return new WaitForSeconds(0.3f);
            var heading = ((Vector2)(diver.Position - aim)).normalized;

            yield return new WaitForSeconds(0.2f);
            Assert.That(diver.IsAlive, Is.True, "the ship dodged, so the Diver should have missed it");
            var later = ((Vector2)(diver.Position - aim)).normalized;
            Assert.That(Vector2.Dot(heading, later), Is.GreaterThan(0.999f), "the charge is a straight line");
        }

        [UnityTest]
        public IEnumerator MineLayer_DropsMinesAsItFlies()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            SpawnEnemy("MineLayer", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y + 3f));

            yield return WaitUntil(() => CountLiveMines() >= 1, 3f, "the Mine Layer to drop a mine");
        }

        [UnityTest]
        public IEnumerator Mine_ArmsBeforeItCanHurtAnyone()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            // Dropped right on top of the ship: it must still be harmless until it arms.
            Runner.DropMine(ShipPosition + new Vector2(0.8f, 0f));
            yield return new WaitForSeconds(MineSpec.ArmSeconds * 0.5f);
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f), "an unarmed mine cannot kill whatever it was dropped on");

            yield return WaitUntil(() => Ship.Vitals.Health < 100f, 1.5f, "the armed mine to go off");
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f - MineSpec.Damage).Within(1e-3f));
            Assert.That(CountLiveMines(), Is.EqualTo(0), "a mine that has gone off is removed");
        }

        [UnityTest]
        public IEnumerator Mine_LeftAloneDoesNotGoOff()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            // Far enough that its leftward drift cannot carry it into range during the test.
            Runner.DropMine(ShipPosition + new Vector2(MineSpec.TriggerRadius + 5f, 0f));

            yield return new WaitForSeconds(1f);
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f), "it only goes off for a ship that comes close");
            Assert.That(CountLiveMines(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Frigate_TurnsAwayShotsIntoItsNoseButNotFromBehind()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var frigate = SpawnEnemy("Frigate", ShipPosition + new Vector2(6f, 0f));
            var lethal = frigate.Definition.MaxHealth * 2f;

            frigate.TakeHit(lethal, 0, frigate.Position + new Vector2(-1f, 0f));
            yield return new WaitForFixedUpdate();
            Assert.That(frigate.IsAlive, Is.True, "the shield covers its nose");
            Assert.That(Session.Score.Kills, Is.EqualTo(0));

            frigate.TakeHit(lethal, 0, frigate.Position + new Vector2(1f, 0f));
            yield return new WaitForFixedUpdate();
            Assert.That(frigate.IsAlive, Is.False, "from behind it is as soft as anything else");
            Assert.That(Session.Score.Kills, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Frigate_SurvivesAShipFiringStraightAtIt()
        {
            Runner.SpawningEnabled = false;
            var frigate = SpawnEnemy("Frigate", ShipPosition + new Vector2(5f, 0f));
            frigate.enabled = false; // hold it still: this test is about the shield, not the approach
            Runner.SetCommandOverride(0, FireForward);

            // Positive evidence that the shots arrived: without this the test would pass just as well with a
            // ship that never fired, or shots that sailed past.
            yield return WaitUntil(() => frigate.ShieldBlocks > 3, 4f, "the shield to turn shots away");

            Assert.That(frigate.IsAlive, Is.True, "shots into the front are turned away, however many there are");
            Assert.That(Session.Score.Kills, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Sniper_WarnsFirstAndOnlyThenTheBeamHurts()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var sniper = SpawnEnemy("Sniper", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y));

            // The warning is the whole fight, so it must cost nothing on its own.
            yield return WaitUntil(() => sniper.BeamIsWarning == true, 6f, "the Sniper to start warning");
            yield return new WaitForSeconds(sniper.Definition.BeamWarningSeconds * 0.5f);
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f), "a warning line cannot hurt anyone");

            yield return WaitUntil(() => sniper.BeamIsFiring == true, 4f, "the Sniper to fire its beam");
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f - sniper.Definition.BeamDamage).Within(1e-3f),
                "the beam hits the moment it fires");

            // It stays lit for a quarter of a second: every step of that must not be a fresh hit.
            yield return WaitUntil(() => sniper.BeamIsFiring == false, 2f, "the beam to go out");
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f - sniper.Definition.BeamDamage).Within(1e-3f),
                "one shot is one hit, not one per frame it is lit");
        }

        [UnityTest]
        public IEnumerator Sniper_MissesAShipThatMovesOffTheWarningLine()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var sniper = SpawnEnemy("Sniper", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y));

            // The first shot lands on a ship that stood still: it is the second one this test is about, taken
            // once the Sniper is on station and its cycle is running.
            yield return WaitUntil(() => sniper.BeamIsFiring == true, 8f, "the Sniper's first beam");
            var afterFirst = Ship.Vitals.Health;
            Assert.That(afterFirst, Is.LessThan(100f), "precondition: a stationary ship is hit");

            yield return WaitUntil(() => sniper.BeamIsWarning == true, 4f, "the Sniper to warn again");
            // Fly out of the line it has just committed to.
            Runner.SetCommandOverride(0, new PlayerCommand(-NVector2.UnitY, false, NVector2.UnitX));

            yield return WaitUntil(() => sniper.BeamIsFiring == true, 4f, "the Sniper to fire again");
            Assert.That(Ship.Vitals.Health, Is.EqualTo(afterFirst), "moving is the answer to a Sniper");
        }

        [UnityTest]
        public IEnumerator SwarmDrone_FliesStraightAndFast()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            var drone = SpawnEnemy("SwarmDrone", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y + 3f));
            var start = drone.Position;

            yield return new WaitForSeconds(0.5f);

            Assert.That(drone.Position.y, Is.EqualTo(start.y).Within(1e-3f), "it holds its lane");
            Assert.That(start.x - drone.Position.x, Is.GreaterThan(drone.Definition.Speed * 0.4f), "and it is quick");
        }

        [UnityTest]
        public IEnumerator Mine_ShotFromADistance_ScoresAndHurtsNobody()
        {
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);
            Runner.DropMine(ShipPosition + new Vector2(MineSpec.TriggerRadius + 5f, 0f));
            yield return WaitUntil(() => CountLiveMines() == 1, 1f, "the mine to appear");
            var mine = Object.FindAnyObjectByType<MineView>();

            mine.TakeHit(1f, 0, mine.Position);
            yield return new WaitForFixedUpdate();

            Assert.That(CountLiveMines(), Is.EqualTo(0), "a mine that is shot goes off");
            Assert.That(Session.Score.Kills, Is.EqualTo(1));
            Assert.That(Session.Score.Score, Is.EqualTo(75)); // 50 x Pilot 1.5
            Assert.That(Ship.Vitals.Health, Is.EqualTo(100f), "the blast does not reach a ship that kept its distance");
        }

        [UnityTest]
        public IEnumerator AReusedEnemy_StartsAsCleanAsANewOne()
        {
            // Enemies are pooled, so an instance is only ever Awoken once: everything a Diver's charge or a
            // Sniper's beam leaves behind has to be undone by Init.
            Runner.SpawningEnabled = false;
            Runner.SetCommandOverride(0, Idle);

            // A no-op release stands in for the pool: the instance survives its despawn, as a pooled one does.
            var diver = SpawnEnemy("Diver", ShipPosition + new Vector2(7f, 3f), _ => { });
            yield return WaitUntil(() => diver.DiverPhase == DivePhase.Charging, 4f, "the Diver to charge");
            Assert.That(diver.transform.rotation, Is.Not.EqualTo(Quaternion.identity), "a charging Diver turns");

            diver.Despawn();
            diver.Init(Runner, Session.Settings, ShipPosition + new Vector2(6f, -3f), null);

            Assert.That(diver.IsAlive, Is.True);
            Assert.That(diver.DiverPhase, Is.EqualTo(DivePhase.Entering), "it starts its approach again");
            Assert.That(diver.transform.rotation, Is.EqualTo(Quaternion.identity), "and it is facing forward again");

            var sniper = SpawnEnemy("Sniper", new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y), _ => { });
            yield return WaitUntil(() => sniper.BeamIsFiring == true, 8f, "the Sniper to fire its beam");
            var beam = sniper.GetComponentInChildren<SpriteRenderer>(true);

            sniper.Despawn();
            sniper.Init(Runner, Session.Settings, new Vector2(Runner.Playfield.MaxX - 1f, ShipPosition.y), null);

            Assert.That(sniper.BeamIsFiring, Is.False, "a reused Sniper is not still mid-shot");
            foreach (var renderer in sniper.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.GetComponent<BeamView>() != null || renderer.transform.name == "Beam")
                    Assert.That(renderer.enabled, Is.False, "and its beam is out");
            Assert.That(beam, Is.Not.Null);
        }

        // ---- Helpers ----

        static int CountLiveMines()
        {
            var count = 0;
            foreach (var mine in Object.FindObjectsByType<MineView>()) if (mine.IsAlive) count++;
            return count;
        }

        /// <summary>Enemy bullets in flight: the pool keeps spent ones around, so only live ones count.</summary>
        static int CountEnemyShots()
        {
            var count = 0;
            foreach (var shot in Object.FindObjectsByType<Projectile>())
                if (shot.IsActive && shot.OwnerIndex < 0) count++;
            return count;
        }
    }
}
