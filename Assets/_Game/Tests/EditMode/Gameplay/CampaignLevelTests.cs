using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Checks the campaign's eight levels against the GDD pages "Levels" and each level's own page: that every
    /// level is playable, introduces its enemies in the right order, and is listed in the campaign and the build.
    /// </summary>
    public class CampaignLevelTests
    {
        const string LevelFolder = "Assets/_Game/ScriptableObjects/Levels/";
        const string SceneFolder = "Assets/_Game/Scenes/";

        static LevelDefinition Load(int number)
        {
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelFolder}Level{number:00}.asset");
            Assert.That(definition, Is.Not.Null, $"Missing Level{number:00}");
            return definition;
        }

        /// <summary>Every enemy prefab a level spawns, by name.</summary>
        static HashSet<string> EnemiesIn(LevelDefinition level)
        {
            var names = new HashSet<string>();
            foreach (var wave in level.Waves)
            foreach (var group in wave.groups)
                if (group.enemy != null) names.Add(group.enemy.name);

            return names;
        }

        [TestCase(1, "Magenta Nebula", "HiveCarrier")]
        [TestCase(2, "Asteroid Belt", "RockCrusher")]
        [TestCase(3, "Solar Corona", "Sunforge")]
        [TestCase(4, "Ice Rings", "FrostLancer")]
        [TestCase(5, "Derelict Fleet", "Dreadnought")]
        [TestCase(6, "Ion Storm", "Tempest")]
        [TestCase(7, "Black Hole's Edge", "SingularityEngine")]
        [TestCase(8, "Enemy Homeworld", "Overmind")]
        public void EveryLevel_MatchesGdd(int number, string setting, string boss)
        {
            var level = Load(number);

            Assert.That(level.LevelNumber, Is.EqualTo(number));
            Assert.That(level.DisplayName, Is.EqualTo(setting));
            Assert.That(level.BossPrefab, Is.Not.Null);
            Assert.That(level.BossPrefab.name, Is.EqualTo(boss));
            Assert.That(level.Validate(), Is.Null, $"Level{number:00} is not playable: {level.Validate()}");
            Assert.That(level.Waves.Count, Is.InRange(10, 14), "a level is about three to four minutes of waves");
        }

        /// <summary>The "First level" column of the GDD's enemy table: nothing turns up before its level.</summary>
        [TestCase("SwarmDrone", 2)]
        [TestCase("Gunship", 2)]
        [TestCase("Diver", 3)]
        [TestCase("MineLayer", 4)]
        [TestCase("Frigate", 5)]
        [TestCase("Sniper", 6)]
        public void EnemiesArriveOnTheLevelTheGddSays(string enemy, int firstLevel)
        {
            for (var number = 1; number < firstLevel; number++)
                Assert.That(EnemiesIn(Load(number)), Has.No.Member(enemy),
                    $"{enemy} turns up in Level{number:00}, before the GDD introduces it");

            Assert.That(EnemiesIn(Load(firstLevel)), Has.Member(enemy),
                $"{enemy} is supposed to be introduced in Level{firstLevel:00}");
        }

        [Test]
        public void TheCampaignListsEveryLevelInOrder()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<Object>("Assets/_Game/Resources/Campaign.asset");
            Assert.That(campaign, Is.Not.Null);

            var scenes = new SerializedObject(campaign).FindProperty("levelScenes");
            Assert.That(scenes.arraySize, Is.EqualTo(8));
            for (var i = 0; i < 8; i++)
                Assert.That(scenes.GetArrayElementAtIndex(i).stringValue, Is.EqualTo($"Level{i + 1:00}"));
        }

        [Test]
        public void EveryLevelSceneIsInTheBuild()
        {
            var paths = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) paths.Add(scene.path);

            for (var number = 1; number <= 8; number++)
                Assert.That(paths, Has.Member($"{SceneFolder}Level{number:00}.unity"),
                    $"Level{number:00} is not in the build, so the campaign cannot load it");
        }

        /// <summary>
        /// A wave whose members would spawn on top of one another reads as one enemy, not a formation
        /// (GDD "Wave System"). <see cref="LevelDefinition.Validate"/> checks this, so this test is about the
        /// levels we actually ship having been built with sensible spacing.
        /// </summary>
        [Test]
        public void NoWaveStacksItsMembers()
        {
            for (var number = 1; number <= 8; number++)
            {
                var level = Load(number);
                for (var w = 0; w < level.Waves.Count; w++)
                foreach (var group in level.Waves[w].groups)
                {
                    if (!FormationLayout.IsSequential(group.formation) || group.count <= 1) continue;
                    Assert.That(group.spacing, Is.GreaterThan(0f),
                        $"Level{number:00} wave {w + 1} ({level.Waves[w].name}) spawns a group with no spacing");
                }
            }
        }
    }
}
