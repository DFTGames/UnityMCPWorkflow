using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Checks the Level 1 data and the Hive Carrier prefab against the GDD pages "Level 01 - Magenta Nebula" and
    /// "Hive Carrier". Update the GDD first, then the asset, then this test.
    /// </summary>
    public class LevelDataTests
    {
        static LevelDefinition LoadLevel()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/_Game/ScriptableObjects/Levels/Level01.asset");
            Assert.That(level, Is.Not.Null);
            return level;
        }

        static EnemyView Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyView>("Assets/_Game/Prefabs/" + name + ".prefab");

        static void Totals(LevelDefinition.Wave wave, out int darts, out int weavers, out int meteors)
        {
            darts = weavers = meteors = 0;
            var dart = Prefab("Dart");
            var weaver = Prefab("Weaver");
            foreach (var group in wave.groups)
            {
                if (group.meteor != null) meteors += group.count;
                else if (group.enemy == dart) darts += group.count;
                else if (group.enemy == weaver) weavers += group.count;
                else Assert.Fail("unexpected enemy " + group.enemy);
            }
        }

        [Test]
        public void Level01_IsValidAndHasTheBoss()
        {
            var level = LoadLevel();

            Assert.That(level.Validate(), Is.Null);
            Assert.That(level.LevelNumber, Is.EqualTo(1));
            Assert.That(level.BossPrefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<BossView>("Assets/_Game/Prefabs/HiveCarrier.prefab")));
            Assert.That(level.Waves.Count, Is.EqualTo(11));
        }

        // Wave number, Darts, Weavers, meteors (GDD wave table, Pilot counts).
        [TestCase(1, 5, 0, 0)]
        [TestCase(2, 0, 3, 0)]
        [TestCase(3, 0, 0, 4)]
        [TestCase(4, 7, 0, 0)]
        [TestCase(5, 4, 4, 0)]
        [TestCase(6, 0, 0, 5)]
        [TestCase(7, 8, 0, 0)]
        [TestCase(8, 0, 6, 0)]
        [TestCase(9, 6, 3, 2)]
        [TestCase(10, 0, 0, 8)]
        [TestCase(11, 10, 4, 0)]
        public void Level01_WaveMatchesGdd(int waveNumber, int darts, int weavers, int meteors)
        {
            Totals(LoadLevel().Waves[waveNumber - 1], out var d, out var w, out var m);

            Assert.That(d, Is.EqualTo(darts), "Darts");
            Assert.That(w, Is.EqualTo(weavers), "Weavers");
            Assert.That(m, Is.EqualTo(meteors), "meteors");
        }

        [Test]
        public void Level01_StartsPromptly()
        {
            // GDD "Wave System", Pacing: the level opens with action, not with the player sitting still.
            Assert.That(LoadLevel().FirstWaveDelay, Is.LessThanOrEqualTo(1.5f));
        }

        [Test]
        public void Level01_SpecsConvertForTheDirector()
        {
            var specs = LoadLevel().ToSpecs();

            Assert.That(specs.Length, Is.EqualTo(11));
            Assert.DoesNotThrow(() => new WaveDirector(specs, 1.3f));
        }

        [TestCase(3, 5)]  // 4 meteors x Ace 1.3 = 5.2
        [TestCase(10, 11)] // 4 x 1.3 = 5.2 -> 5, 2 x 1.3 = 2.6 -> 3, 2 x 1.3 = 2.6 -> 3
        public void Level01_MeteorFieldsScaleWithDifficulty(int waveNumber, int expectedOnAce)
        {
            var specs = LoadLevel().ToSpecs();
            var director = new WaveDirector(specs, DifficultySettings.Ace.EnemyCountMultiplier);

            var total = 0;
            for (var g = 0; g < specs[waveNumber - 1].Length; g++) total += director.ScaledCount(waveNumber - 1, g);
            Assert.That(total, Is.EqualTo(expectedOnAce));
        }

        [Test]
        public void HiveCarrier_TuningMatchesGdd()
        {
            var view = AssetDatabase.LoadAssetAtPath<BossView>("Assets/_Game/Prefabs/HiveCarrier.prefab");
            var so = new SerializedObject(view);

            Assert.That(so.FindProperty("contactDamage").floatValue, Is.EqualTo(40f));
            Assert.That(so.FindProperty("spreadBulletSpeed").floatValue, Is.EqualTo(6f));
            Assert.That(so.FindProperty("spreadBulletDamage").floatValue, Is.EqualTo(10f));
            Assert.That(so.FindProperty("spreadAngle").floatValue, Is.EqualTo(60f));
            Assert.That(so.FindProperty("entrySpeed").floatValue, Is.EqualTo(2f));
            Assert.That(so.FindProperty("holdInset").floatValue, Is.EqualTo(3f));
            Assert.That(so.FindProperty("driftAmplitude").floatValue, Is.EqualTo(2.2f));
            Assert.That(so.FindProperty("driftPeriod").floatValue, Is.EqualTo(6f));
        }

        [Test]
        public void Validate_RejectsBadGroups()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var so = new SerializedObject(level);
                so.FindProperty("bossPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<BossView>("Assets/_Game/Prefabs/HiveCarrier.prefab");
                var waves = so.FindProperty("waves");
                waves.arraySize = 1;
                var groups = waves.GetArrayElementAtIndex(0).FindPropertyRelative("groups");
                groups.arraySize = 1;
                var group = groups.GetArrayElementAtIndex(0);
                group.FindPropertyRelative("count").intValue = 3;
                group.FindPropertyRelative("formation").enumValueIndex = (int)Formation.Line;
                group.FindPropertyRelative("spacing").floatValue = 0f;
                group.FindPropertyRelative("enemy").objectReferenceValue = Prefab("Dart");
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(level.Validate(), Does.Contain("zero spacing"));

                group.FindPropertyRelative("spacing").floatValue = 0.5f;
                group.FindPropertyRelative("meteor").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<MeteorDefinition>("Assets/_Game/ScriptableObjects/Meteors/MeteorLarge.asset");
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(level.Validate(), Does.Contain("both an enemy and a meteor"));

                group.FindPropertyRelative("meteor").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(level.Validate(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void HiveCarrier_CoreIsOnTheHazardLayerAndNotCoveredByArmour()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/HiveCarrier.prefab");
            var core = prefab.GetComponentInChildren<BossCoreView>();
            Assert.That(core, Is.Not.Null);
            Assert.That(core.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Hazard")));
            Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Hazard")));

            var coreCircle = core.GetComponent<CircleCollider2D>();
            Assert.That(coreCircle.isTrigger, Is.True);
            var coreCentre = (Vector2)core.transform.localPosition + coreCircle.offset;

            var hull = prefab.GetComponents<BoxCollider2D>();
            Assert.That(hull.Length, Is.GreaterThan(0));
            foreach (var box in hull)
            {
                Assert.That(box.isTrigger, Is.True);
                var min = box.offset - box.size * 0.5f;
                var max = box.offset + box.size * 0.5f;
                var closest = new Vector2(Mathf.Clamp(coreCentre.x, min.x, max.x), Mathf.Clamp(coreCentre.y, min.y, max.y));
                Assert.That(Vector2.Distance(closest, coreCentre), Is.GreaterThanOrEqualTo(coreCircle.radius),
                    "an armour collider overlaps the core, so shots at the open core could be absorbed");
            }
        }

        [Test]
        public void HiveCarrier_HasEnginesAndLaunchBays()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/HiveCarrier.prefab");
            var view = prefab.GetComponent<BossView>();

            Assert.That(prefab.GetComponentsInChildren<EngineExhaust>().Length, Is.EqualTo(2));
            Assert.That(new SerializedObject(view).FindProperty("launchBays").arraySize, Is.EqualTo(HiveCarrierSpec.Default.DartsPerLaunch));
        }
    }
}
