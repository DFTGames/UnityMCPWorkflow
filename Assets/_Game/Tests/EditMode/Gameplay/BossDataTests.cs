using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Checks the eight bosses against the GDD page "Levels" and the per-level pages: the headline numbers and,
    /// more importantly, that each one still has the signature move it is named for. Update the GDD first, then
    /// the builder, then this test.
    /// </summary>
    public class BossDataTests
    {
        const string BossFolder = "Assets/_Game/ScriptableObjects/Bosses/";
        const string PrefabFolder = "Assets/_Game/Prefabs/";

        static BossDefinition Load(string name)
        {
            var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(BossFolder + name + ".asset");
            Assert.That(definition, Is.Not.Null, "Missing boss definition " + name);
            return definition;
        }

        static bool Has(BossDefinition definition, BossActionType type)
        {
            foreach (var row in definition.Attacks)
                if (row.type == type) return true;
            return false;
        }

        /// <summary>Name, level, health and the move the GDD names it for.</summary>
        [TestCase("HiveCarrier", 1, 200f, BossActionType.LaunchDarts)]
        [TestCase("RockCrusher", 2, 260f, BossActionType.HurlMeteor)]
        [TestCase("Sunforge", 3, 320f, BossActionType.BeamSweep)]
        [TestCase("FrostLancer", 4, 360f, BossActionType.Dash)]
        [TestCase("Dreadnought", 5, 460f, BossActionType.TurretVolley)]
        [TestCase("Tempest", 6, 500f, BossActionType.BeamSweep)]
        [TestCase("SingularityEngine", 7, 560f, BossActionType.GravityPull)]
        [TestCase("Overmind", 8, 700f, BossActionType.Dash)]
        public void EveryBoss_MatchesGdd(string name, int level, float health, BossActionType signature)
        {
            var definition = Load(name);

            Assert.That(definition.LevelNumber, Is.EqualTo(level));
            Assert.That(definition.MaxHealth, Is.EqualTo(health));
            Assert.That(definition.HullDamageMultiplier, Is.EqualTo(0.5f), "half damage to the hull (GDD Levels)");
            Assert.That(definition.CoreDamageMultiplier, Is.EqualTo(1f), "and full damage to the open core");
            Assert.That(Has(definition, signature), Is.True, $"{name} has lost its signature move");
            Assert.That(definition.Validate(), Is.Null, $"{name} is not usable: {definition.Validate()}");
        }

        /// <summary>The cycle each level's page states, which is what its fight's rhythm is built on.</summary>
        [TestCase("HiveCarrier", 8f, 4f)]
        [TestCase("RockCrusher", 8f, 3.5f)]
        [TestCase("Sunforge", 9f, 4f)]
        [TestCase("FrostLancer", 9f, 3f)]
        [TestCase("Dreadnought", 10f, 4f)]
        [TestCase("Tempest", 9f, 3.5f)]
        [TestCase("SingularityEngine", 10f, 4f)]
        [TestCase("Overmind", 11f, 4.5f)]
        public void EveryBoss_KeepsTheCycleItsPageStates(string name, float cycle, float open)
        {
            var spec = Load(name).ToSpec();

            Assert.That(spec.CycleSeconds, Is.EqualTo(cycle).Within(1e-3f));
            Assert.That(spec.OpenSeconds, Is.EqualTo(open).Within(1e-3f));
        }

        [TestCase("HiveCarrier")]
        [TestCase("RockCrusher")]
        [TestCase("Sunforge")]
        [TestCase("FrostLancer")]
        [TestCase("Dreadnought")]
        [TestCase("Tempest")]
        [TestCase("SingularityEngine")]
        [TestCase("Overmind")]
        public void EveryBoss_CanBeHurtAndCanHurtBack(string name)
        {
            var definition = Load(name);
            var spec = definition.ToSpec();

            Assert.That(spec.OpenSeconds, Is.LessThan(spec.CycleSeconds), "a boss that is always open has no fight");
            Assert.That(spec.ClosedSeconds, Is.GreaterThan(0.5f), "and one that never opens cannot be beaten");

            var armoured = false;
            var vulnerable = false;
            foreach (var attack in spec.Attacks)
            {
                if (attack.Matches(false)) armoured = true;
                if (attack.Matches(true)) vulnerable = true;
            }

            Assert.That(armoured, Is.True, "nothing happens while its core is shut");
            Assert.That(vulnerable, Is.True, "it is harmless in the window where it can be hurt");
        }

        /// <summary>
        /// Health bands are how a boss escalates. Two attacks of the same type in the same phase must not share a
        /// band, or the boss would do both at once, which is never what the table means.
        /// </summary>
        [TestCase("HiveCarrier")]
        [TestCase("RockCrusher")]
        [TestCase("Sunforge")]
        [TestCase("FrostLancer")]
        [TestCase("Dreadnought")]
        [TestCase("Tempest")]
        [TestCase("SingularityEngine")]
        [TestCase("Overmind")]
        public void EveryBoss_HasNoAccidentallyDoubledAttack(string name)
        {
            var definition = Load(name);
            var rows = definition.Attacks;

            for (var i = 0; i < rows.Length; i++)
            for (var j = i + 1; j < rows.Length; j++)
            {
                var a = rows[i];
                var b = rows[j];
                if (a.type != b.type || a.when != b.when) continue;
                if (a.intervalSeconds <= 0f && b.intervalSeconds <= 0f &&
                    !Mathf.Approximately(a.firstDelaySeconds, b.firstDelaySeconds)) continue;

                var overlaps = a.activeAbove < b.activeAtOrBelow && b.activeAbove < a.activeAtOrBelow;
                Assert.That(overlaps, Is.False,
                    $"{name}: two {a.type} attacks in the same phase overlap between " +
                    $"{Mathf.Max(a.activeAbove, b.activeAbove)} and {Mathf.Min(a.activeAtOrBelow, b.activeAtOrBelow)}");
            }
        }

        /// <summary>
        /// A dash or a sweep that is asked for while the last one is still running is dropped, and because those
        /// rows are one-shots it is dropped for good: the boss silently stops escalating. The durations here are
        /// the ones the level pages state.
        /// </summary>
        [TestCase("FrostLancer", BossActionType.Dash, 2.6f)]
        [TestCase("Overmind", BossActionType.Dash, 2.5f)]
        [TestCase("Sunforge", BossActionType.BeamSweep, 2f)]
        [TestCase("Tempest", BossActionType.BeamSweep, 1.6f)]
        public void EveryEscalatingAttack_HasRoomToFinish(string name, BossActionType type, float duration)
        {
            var definition = Load(name);
            var spec = definition.ToSpec();

            var delays = new List<float>();
            foreach (var row in definition.Attacks)
                if (row.type == type && row.when == BossPhase.Armoured) delays.Add(row.firstDelaySeconds);

            delays.Sort();
            Assert.That(delays, Is.Not.Empty);

            for (var i = 1; i < delays.Count; i++)
                Assert.That(delays[i], Is.GreaterThanOrEqualTo(delays[i - 1] + duration),
                    $"{name}: a {type} at {delays[i]} s is asked for while the one at {delays[i - 1]} s is still going");

            Assert.That(delays[delays.Count - 1] + duration, Is.LessThanOrEqualTo(spec.ClosedSeconds),
                $"{name}: its last {type} cannot finish inside a {spec.ClosedSeconds} s phase");
        }

        [TestCase("HiveCarrier")]
        [TestCase("RockCrusher")]
        [TestCase("Sunforge")]
        [TestCase("FrostLancer")]
        [TestCase("Dreadnought")]
        [TestCase("Tempest")]
        [TestCase("SingularityEngine")]
        [TestCase("Overmind")]
        public void EveryBossPrefab_IsWiredToItsOwnData(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, "Missing boss prefab " + name);

            var view = prefab.GetComponent<BossView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Definition, Is.Not.Null, name + " has no definition");
            Assert.That(view.Definition.name, Is.EqualTo(name));

            var renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.Not.Null, name + " has no sprite");
            Assert.That(renderer.sprite.name, Is.EqualTo(name), "each boss wears its own art");

            // A boss that sweeps a beam needs something to draw it with.
            var sweeps = Has(view.Definition, BossActionType.BeamSweep);
            Assert.That(prefab.transform.Find("Beam") != null, Is.EqualTo(sweeps),
                sweeps ? name + " sweeps a beam but has no beam object" : name + " has a beam object it never uses");
        }

        /// <summary>
        /// The one rule the whole boss fight rests on: the armour must never cover the core, or shots aimed at an
        /// open core would be absorbed by the hull instead.
        /// </summary>
        [TestCase("HiveCarrier")]
        [TestCase("RockCrusher")]
        [TestCase("Sunforge")]
        [TestCase("FrostLancer")]
        [TestCase("Dreadnought")]
        [TestCase("Tempest")]
        [TestCase("SingularityEngine")]
        [TestCase("Overmind")]
        public void EveryBoss_LeavesItsCoreExposed(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            var core = prefab.GetComponentInChildren<BossCoreView>(true);
            Assert.That(core, Is.Not.Null, name + " has no core");

            var circle = core.GetComponent<CircleCollider2D>();
            Assert.That(circle, Is.Not.Null);
            Assert.That(circle.isTrigger, Is.True);

            // The core's transform is scaled to fit its sprite, and that scales the collider with it: measuring
            // the raw radius would check a hitbox that does not exist.
            var scale = core.transform.localScale.x;
            var centre = (Vector2)core.transform.localPosition + circle.offset * scale;
            var radius = circle.radius * scale;

            var hull = prefab.GetComponents<BoxCollider2D>();
            Assert.That(hull.Length, Is.GreaterThan(0), name + " has no armour");
            foreach (var box in hull)
            {
                var min = box.offset - box.size * 0.5f;
                var max = box.offset + box.size * 0.5f;
                var closest = new Vector2(Mathf.Clamp(centre.x, min.x, max.x), Mathf.Clamp(centre.y, min.y, max.y));
                Assert.That(Vector2.Distance(closest, centre), Is.GreaterThanOrEqualTo(radius),
                    $"{name}: an armour collider overlaps the core");
            }
        }

        /// <summary>Bosses get tougher as the campaign goes on; that is the whole of the difficulty curve here.</summary>
        [Test]
        public void TheCampaign_GetsHarder()
        {
            var names = new[]
            {
                "HiveCarrier", "RockCrusher", "Sunforge", "FrostLancer",
                "Dreadnought", "Tempest", "SingularityEngine", "Overmind"
            };

            var health = new List<float>();
            foreach (var name in names) health.Add(Load(name).MaxHealth);

            for (var i = 1; i < health.Count; i++)
                Assert.That(health[i], Is.GreaterThan(health[i - 1]),
                    $"{names[i]} is no tougher than {names[i - 1]}");
        }
    }
}
