using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.Editor
{
    /// <summary>
    /// Builds levels 2 to 8: their wave scripts (<see cref="LevelDefinition"/> assets) and their scenes, each a
    /// copy of Level 1 with its own backdrop and level data (GDD "Levels" and the per-level pages). Level 1 is
    /// left alone: it was built by hand and its data is the worked example the other seven follow.
    /// Re-runnable: change a wave here and rebuild.
    /// </summary>
    public static class LevelBuilder
    {
        const string TemplateScene = "Assets/_Game/Scenes/Level01.unity";
        const string SceneFolder = "Assets/_Game/Scenes";
        const string LevelFolder = "Assets/_Game/ScriptableObjects/Levels";
        const string EnemyFolder = "Assets/_Game/Prefabs";
        const string MeteorFolder = "Assets/_Game/ScriptableObjects/Meteors";
        const string BossFolder = "Assets/_Game/Prefabs";
        const string BackdropFolder = "Assets/_Game/Textures/Backgrounds";
        const string CampaignAsset = "Assets/_Game/Resources/Campaign.asset";

        /// <summary>One spawn group, before it is turned into level data.</summary>
        readonly struct Group
        {
            public readonly string Enemy;
            public readonly string Meteor;
            public readonly int Count;
            public readonly Formation Formation;
            public readonly float EntryHeight;
            public readonly float Spacing;
            public readonly float StartDelay;

            Group(string enemy, string meteor, int count, Formation formation, float entryHeight, float spacing,
                float startDelay)
            {
                Enemy = enemy;
                Meteor = meteor;
                Count = count;
                Formation = formation;
                EntryHeight = entryHeight;
                Spacing = spacing;
                StartDelay = startDelay;
            }

            public static Group E(string enemy, int count, Formation formation, float entryHeight = 0.5f,
                float spacing = 0.4f, float startDelay = 0f) =>
                new Group(enemy, null, count, formation, entryHeight, spacing, startDelay);

            public static Group M(string meteor, int count, float entryHeight = 0.5f, float spacing = 0.8f,
                float startDelay = 0f) =>
                new Group(null, meteor, count, Formation.Scatter, entryHeight, spacing, startDelay);
        }

        readonly struct Wave
        {
            public readonly string Name;
            public readonly Group[] Groups;

            public Wave(string name, params Group[] groups)
            {
                Name = name;
                Groups = groups;
            }
        }

        sealed class Level
        {
            public int Number;
            public string Setting;
            public string Boss;
            public string Backdrop;
            public Wave[] Waves;

            public string SceneName => $"Level{Number:00}";
        }

        // Shorthands, so a wave script reads like the table in the GDD.
        static Group E(string enemy, int count, Formation formation, float entryHeight = 0.5f, float spacing = 0.4f,
            float startDelay = 0f) => Group.E(enemy, count, formation, entryHeight, spacing, startDelay);

        static Group M(string meteor, int count, float entryHeight = 0.5f, float spacing = 0.8f,
            float startDelay = 0f) => Group.M(meteor, count, entryHeight, spacing, startDelay);

        const string Large = "MeteorLarge";
        const string Solid = "MeteorSolid";

        static Level[] Levels() => new[]
        {
            new Level
            {
                Number = 2, Setting = "Asteroid Belt", Boss = "RockCrusher", Backdrop = "AsteroidBeltBackground",
                Waves = new[]
                {
                    new Wave("Rolling stones", M(Large, 4)),
                    new Wave("Drone swarm", E("SwarmDrone", 8, Formation.V)),
                    new Wave("Darts and rocks",
                        E("Dart", 5, Formation.Line),
                        M(Solid, 2, startDelay: 1f)),
                    new Wave("First gunship",
                        E("Gunship", 1, Formation.Line, 0.6f),
                        E("Dart", 4, Formation.Line, 0.35f, startDelay: 1.5f)),
                    new Wave("Meteor field", M(Large, 5)),
                    new Wave("Swarm pincer",
                        E("SwarmDrone", 6, Formation.Column, 0.8f),
                        E("SwarmDrone", 6, Formation.Column, 0.2f, startDelay: 0.8f)),
                    new Wave("Weavers", E("Weaver", 4, Formation.Staggered, spacing: 0.8f)),
                    new Wave("Gunship pair",
                        E("Gunship", 2, Formation.Staggered, spacing: 1.2f),
                        E("SwarmDrone", 6, Formation.Line, 0.5f, startDelay: 2f)),
                    new Wave("Rock storm", M(Large, 4), M(Solid, 2, startDelay: 1f)),
                    new Wave("Pressure",
                        E("Dart", 6, Formation.Line, 0.65f),
                        E("SwarmDrone", 4, Formation.Line, 0.3f, startDelay: 1f),
                        E("Gunship", 1, Formation.Line, 0.5f, startDelay: 2.5f)),
                    new Wave("Final rush",
                        E("SwarmDrone", 8, Formation.V, 0.6f),
                        E("Dart", 6, Formation.Line, 0.35f, startDelay: 1.2f),
                        M(Large, 3, startDelay: 2.5f))
                }
            },

            new Level
            {
                Number = 3, Setting = "Solar Corona", Boss = "Sunforge", Backdrop = "SolarCoronaBackground",
                Waves = new[]
                {
                    new Wave("Flare runners", E("Dart", 6, Formation.Line)),
                    new Wave("First divers", E("Diver", 3, Formation.Staggered, spacing: 1.2f)),
                    new Wave("Weaver screen", E("Weaver", 5, Formation.Staggered, spacing: 0.8f)),
                    new Wave("Divers and drones",
                        E("Diver", 4, Formation.Staggered, spacing: 1f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 2f)),
                    new Wave("Burnt rock", M(Solid, 4)),
                    new Wave("Gun line",
                        E("Gunship", 2, Formation.Staggered, spacing: 1.2f),
                        E("Dart", 5, Formation.Line, 0.5f, startDelay: 1.5f)),
                    new Wave("Dive pincer",
                        E("Diver", 3, Formation.Column, 0.85f),
                        E("Diver", 3, Formation.Column, 0.15f, startDelay: 0.8f)),
                    new Wave("Swarm and weavers",
                        E("SwarmDrone", 8, Formation.V, 0.55f),
                        E("Weaver", 4, Formation.Staggered, 0.5f, 0.8f, 2f)),
                    new Wave("Heat",
                        E("Dart", 6, Formation.Line, 0.7f),
                        E("Diver", 3, Formation.Staggered, 0.4f, 1f, 1.5f)),
                    new Wave("Corona storm", M(Large, 4), M(Solid, 2, startDelay: 1f)),
                    new Wave("Final rush",
                        E("Diver", 4, Formation.Staggered, spacing: 0.9f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 1.5f),
                        E("Dart", 6, Formation.Line, 0.4f, startDelay: 3f))
                }
            },

            new Level
            {
                Number = 4, Setting = "Ice Rings", Boss = "FrostLancer", Backdrop = "IceRingsBackground",
                Waves = new[]
                {
                    new Wave("Ice shards", M(Large, 5)),
                    new Wave("First layer",
                        E("MineLayer", 1, Formation.Line, 0.6f),
                        E("Dart", 4, Formation.Line, 0.35f, startDelay: 1.5f)),
                    new Wave("Drone wedge", E("SwarmDrone", 8, Formation.V)),
                    new Wave("Minefield", E("MineLayer", 2, Formation.Staggered, spacing: 1.5f)),
                    new Wave("Divers", E("Diver", 4, Formation.Staggered, spacing: 1f)),
                    new Wave("Gunship and mines",
                        E("Gunship", 1, Formation.Line, 0.65f),
                        E("MineLayer", 2, Formation.Staggered, 0.35f, 1.5f, 1.5f)),
                    new Wave("Ring debris", M(Solid, 4), M(Large, 3, startDelay: 1f)),
                    new Wave("Weaver screen", E("Weaver", 6, Formation.Staggered, spacing: 0.7f)),
                    new Wave("Mixed",
                        E("MineLayer", 2, Formation.Staggered, 0.6f, 1.5f),
                        E("Dart", 6, Formation.Line, 0.35f, startDelay: 1.5f),
                        E("SwarmDrone", 4, Formation.Line, 0.5f, startDelay: 3f)),
                    new Wave("Dive storm", E("Diver", 5, Formation.Staggered, spacing: 0.8f)),
                    new Wave("Final rush",
                        E("Gunship", 2, Formation.Staggered, spacing: 1.2f),
                        E("MineLayer", 3, Formation.Staggered, 0.4f, 1.2f, 1.5f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 3f))
                }
            },

            new Level
            {
                Number = 5, Setting = "Derelict Fleet", Boss = "Dreadnought", Backdrop = "DerelictFleetBackground",
                Waves = new[]
                {
                    new Wave("Salvage",
                        E("Dart", 5, Formation.Line),
                        M(Solid, 3, startDelay: 1f)),
                    new Wave("First frigate",
                        E("Frigate", 1, Formation.Line, 0.5f),
                        E("SwarmDrone", 4, Formation.Line, 0.75f, startDelay: 1.5f)),
                    new Wave("Gun line", E("Gunship", 2, Formation.Staggered, spacing: 1.2f)),
                    new Wave("Frigate wall", E("Frigate", 2, Formation.Staggered, spacing: 2f)),
                    new Wave("Divers", E("Diver", 5, Formation.Staggered, spacing: 0.9f)),
                    new Wave("Minefield", E("MineLayer", 3, Formation.Staggered, spacing: 1.4f)),
                    new Wave("Escort",
                        E("Frigate", 1, Formation.Line, 0.45f),
                        E("Gunship", 2, Formation.Staggered, 0.6f, 1.2f, 1.5f)),
                    new Wave("Swarm",
                        E("SwarmDrone", 8, Formation.V, 0.6f),
                        E("Dart", 6, Formation.Line, 0.35f, startDelay: 1.5f)),
                    new Wave("Hulks", M(Solid, 5)),
                    new Wave("Pressure",
                        E("Frigate", 2, Formation.Staggered, spacing: 2f),
                        E("Diver", 4, Formation.Staggered, 0.5f, 0.9f, 2f)),
                    new Wave("Final rush",
                        E("Frigate", 2, Formation.Staggered, spacing: 2f),
                        E("Gunship", 2, Formation.Staggered, 0.5f, 1.2f, 1.5f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 3f))
                }
            },

            new Level
            {
                Number = 6, Setting = "Ion Storm", Boss = "Tempest", Backdrop = "IonStormBackground",
                Waves = new[]
                {
                    new Wave("Static", E("Dart", 6, Formation.Line)),
                    new Wave("First sniper",
                        E("Sniper", 1, Formation.Line, 0.6f),
                        E("Dart", 4, Formation.Line, 0.35f, startDelay: 1.5f)),
                    new Wave("Crossfire", E("Sniper", 2, Formation.Staggered, spacing: 1.5f)),
                    new Wave("Swarm and weavers",
                        E("SwarmDrone", 8, Formation.V, 0.6f),
                        E("Weaver", 4, Formation.Staggered, 0.4f, 0.8f, 2f)),
                    new Wave("Sniper and gunship",
                        E("Sniper", 1, Formation.Line, 0.7f),
                        E("Gunship", 1, Formation.Line, 0.3f, startDelay: 1f)),
                    new Wave("Divers", E("Diver", 5, Formation.Staggered, spacing: 0.8f)),
                    new Wave("Mines and snipers",
                        E("MineLayer", 2, Formation.Staggered, 0.35f, 1.4f),
                        E("Sniper", 2, Formation.Staggered, 0.7f, 1.5f, 1.5f)),
                    new Wave("Frigate wall", E("Frigate", 2, Formation.Staggered, spacing: 2f)),
                    new Wave("Storm debris", M(Large, 4), M(Solid, 2, startDelay: 1f)),
                    new Wave("Pressure",
                        E("Sniper", 3, Formation.Staggered, 0.65f, 1.2f),
                        E("Dart", 6, Formation.Line, 0.35f, startDelay: 1.5f)),
                    new Wave("Final rush",
                        E("Sniper", 2, Formation.Staggered, 0.7f, 1.5f),
                        E("Frigate", 2, Formation.Staggered, 0.35f, 2f, 1.5f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 3f))
                }
            },

            new Level
            {
                Number = 7, Setting = "Black Hole's Edge", Boss = "SingularityEngine",
                Backdrop = "BlackHoleBackground",
                Waves = new[]
                {
                    new Wave("Event horizon",
                        E("Dart", 6, Formation.Line, 0.65f),
                        E("SwarmDrone", 4, Formation.Line, 0.35f, startDelay: 1f)),
                    new Wave("Interceptors", E("Diver", 5, Formation.Staggered, spacing: 0.8f)),
                    new Wave("Crossfire", E("Sniper", 3, Formation.Staggered, spacing: 1.3f)),
                    new Wave("Frigate wall",
                        E("Frigate", 2, Formation.Staggered, spacing: 2f),
                        E("SwarmDrone", 6, Formation.Line, 0.5f, startDelay: 2f)),
                    new Wave("Drawn in", M(Solid, 6)),
                    new Wave("Gun line", E("Gunship", 3, Formation.Staggered, spacing: 1.2f)),
                    new Wave("Mines and weavers",
                        E("MineLayer", 3, Formation.Staggered, 0.4f, 1.3f),
                        E("Weaver", 4, Formation.Staggered, 0.7f, 0.8f, 2f)),
                    new Wave("Swarm",
                        E("SwarmDrone", 8, Formation.V, 0.7f),
                        E("SwarmDrone", 8, Formation.V, 0.3f, startDelay: 1.5f)),
                    new Wave("Mixed assault",
                        E("Frigate", 2, Formation.Staggered, spacing: 2f),
                        E("Diver", 3, Formation.Staggered, 0.5f, 0.9f, 1.5f),
                        E("Sniper", 2, Formation.Staggered, 0.7f, 1.4f, 3f)),
                    new Wave("Debris storm", M(Large, 5), M(Solid, 3, startDelay: 1f)),
                    new Wave("Final rush",
                        E("Gunship", 3, Formation.Staggered, spacing: 1.2f),
                        E("Diver", 4, Formation.Staggered, 0.5f, 0.9f, 1.5f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 3f)),
                    new Wave("Last stand",
                        E("Frigate", 2, Formation.Staggered, spacing: 2f),
                        E("Sniper", 3, Formation.Staggered, 0.7f, 1.3f, 1.5f),
                        E("Dart", 6, Formation.Line, 0.35f, startDelay: 3f))
                }
            },

            new Level
            {
                Number = 8, Setting = "Enemy Homeworld", Boss = "Overmind", Backdrop = "HomeworldBackground",
                Waves = new[]
                {
                    new Wave("Defence line",
                        E("Dart", 8, Formation.Line, 0.65f, 0.3f),
                        E("SwarmDrone", 8, Formation.V, 0.35f, startDelay: 1f)),
                    new Wave("Interceptors", E("Diver", 6, Formation.Staggered, spacing: 0.7f)),
                    new Wave("Gun line", E("Gunship", 3, Formation.Staggered, spacing: 1.1f)),
                    new Wave("Crossfire", E("Sniper", 4, Formation.Staggered, spacing: 1.2f)),
                    new Wave("Frigate wall", E("Frigate", 3, Formation.Staggered, spacing: 1.8f)),
                    new Wave("Minefield", E("MineLayer", 4, Formation.Staggered, spacing: 1.2f)),
                    new Wave("Bombardment", M(Large, 5), M(Solid, 3, startDelay: 1f)),
                    new Wave("The swarm",
                        E("SwarmDrone", 8, Formation.V, 0.75f),
                        E("SwarmDrone", 8, Formation.V, 0.25f, startDelay: 1.2f)),
                    new Wave("Mixed assault",
                        E("Frigate", 2, Formation.Staggered, spacing: 2f),
                        E("Diver", 3, Formation.Staggered, 0.5f, 0.9f, 1.5f),
                        E("Gunship", 2, Formation.Staggered, 0.6f, 1.2f, 3f)),
                    new Wave("Sniper nest",
                        E("Sniper", 4, Formation.Staggered, 0.7f, 1.2f),
                        E("Weaver", 6, Formation.Staggered, 0.4f, 0.7f, 1.5f)),
                    new Wave("Pressure",
                        E("MineLayer", 3, Formation.Staggered, 0.35f, 1.2f),
                        E("Diver", 4, Formation.Staggered, 0.65f, 0.8f, 1.5f),
                        E("SwarmDrone", 8, Formation.V, 0.5f, startDelay: 3f)),
                    new Wave("Final rush",
                        E("Frigate", 3, Formation.Staggered, spacing: 1.8f),
                        E("Gunship", 3, Formation.Staggered, 0.5f, 1.1f, 1.5f),
                        E("Sniper", 4, Formation.Staggered, 0.7f, 1.2f, 3f)),
                    new Wave("Last stand",
                        E("Dart", 10, Formation.Line, 0.65f, 0.25f),
                        E("SwarmDrone", 8, Formation.V, 0.35f, startDelay: 1f),
                        M(Large, 4, startDelay: 2.5f))
                }
            }
        };

        [MenuItem("Tools/YASS/Build Levels")]
        public static void Build()
        {
            Directory.CreateDirectory(LevelFolder);
            AssetDatabase.Refresh();

            var levels = Levels();
            foreach (var level in levels)
            {
                var definition = BuildDefinition(level);
                BuildScene(level, definition);
            }

            UpdateCampaign(levels);
            UpdateBuildSettings(levels);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{nameof(LevelBuilder)}: built levels 2 to {levels[levels.Length - 1].Number}.");
        }

        static LevelDefinition BuildDefinition(Level level)
        {
            var path = $"{LevelFolder}/{level.SceneName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);
            Find(serialized, "levelNumber").intValue = level.Number;
            Find(serialized, "displayName").stringValue = level.Setting;
            Find(serialized, "firstWaveDelay").floatValue = 1f;
            Find(serialized, "bossPrefab").objectReferenceValue = LoadBoss(level.Boss);

            var waves = Find(serialized, "waves");
            waves.arraySize = level.Waves.Length;
            for (var w = 0; w < level.Waves.Length; w++)
            {
                var wave = level.Waves[w];
                var element = waves.GetArrayElementAtIndex(w);
                element.FindPropertyRelative("name").stringValue = wave.Name;

                var groups = element.FindPropertyRelative("groups");
                groups.arraySize = wave.Groups.Length;
                for (var g = 0; g < wave.Groups.Length; g++)
                {
                    var group = wave.Groups[g];
                    var row = groups.GetArrayElementAtIndex(g);
                    row.FindPropertyRelative("enemy").objectReferenceValue =
                        group.Enemy != null ? LoadEnemy(group.Enemy) : null;
                    row.FindPropertyRelative("meteor").objectReferenceValue =
                        group.Meteor != null ? LoadMeteor(group.Meteor) : null;
                    row.FindPropertyRelative("count").intValue = group.Count;
                    row.FindPropertyRelative("formation").enumValueIndex = (int)group.Formation;
                    row.FindPropertyRelative("entryHeight").floatValue = group.EntryHeight;
                    row.FindPropertyRelative("spacing").floatValue = group.Spacing;
                    row.FindPropertyRelative("startDelay").floatValue = group.StartDelay;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            if (definition.Validate() is string problem)
                Debug.LogError($"{nameof(LevelBuilder)}: {level.SceneName} is not playable: {problem}.");

            return definition;
        }

        /// <summary>
        /// A level scene is Level 1 with a different backdrop and a different wave script. It is copied fresh
        /// every time rather than patched in place: the whole point is that eight scenes cannot drift apart, so
        /// Level 1 is the only one anybody edits and the other seven are regenerated from it.
        /// </summary>
        static void BuildScene(Level level, LevelDefinition definition)
        {
            var path = $"{SceneFolder}/{level.SceneName}.unity";

            // A scene cannot be replaced underneath the editor while it is open.
            var open = SceneManager.GetSceneByPath(path);
            if (open.IsValid() && open.isLoaded)
            {
                Debug.LogWarning($"{nameof(LevelBuilder)}: {level.SceneName} is open in the editor, so it was " +
                                 "left as it is. Close it and rebuild to pick up changes made to Level 1.");
                return;
            }

            if (File.Exists(path) && !AssetDatabase.DeleteAsset(path))
            {
                Debug.LogError($"{nameof(LevelBuilder)}: could not replace {path}.");
                return;
            }

            if (!AssetDatabase.CopyAsset(TemplateScene, path))
            {
                Debug.LogError($"{nameof(LevelBuilder)}: could not create {path} from the Level 1 scene.");
                return;
            }

            var scene = MenuSceneParts.OpenForBuilding(path, out var openedByBuilder);
            var wired = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Background") SetBackdrop(root, level);

                var runner = root.GetComponentInChildren<GameRunner>(true);
                if (runner == null) continue;

                var serialized = new SerializedObject(runner);
                Find(serialized, "level").objectReferenceValue = definition;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                wired = true;
            }

            if (!wired)
                Debug.LogError($"{nameof(LevelBuilder)}: no {nameof(GameRunner)} in {path}, so it would play " +
                               "Level 1's waves.");

            // No layout pass: the menus came with the copy, already laid out by the builder that made them.
            MenuSceneParts.SaveAndRelease(scene, path, openedByBuilder, layout: false);
        }

        static void SetBackdrop(GameObject background, Level level)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{BackdropFolder}/{level.Backdrop}.png");
            if (sprite == null)
            {
                Debug.LogWarning($"{nameof(LevelBuilder)}: no backdrop '{level.Backdrop}'; art still to come.");
                return;
            }

            var renderer = background.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Debug.LogError($"{nameof(LevelBuilder)}: the Background object has no sprite renderer.");
                return;
            }

            renderer.sprite = sprite;
        }

        /// <summary>The campaign is the ordered list of level scenes the flow layer walks through.</summary>
        static void UpdateCampaign(IReadOnlyList<Level> levels)
        {
            var campaign = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CampaignAsset);
            if (campaign == null)
            {
                Debug.LogError($"{nameof(LevelBuilder)}: no campaign asset at {CampaignAsset}.");
                return;
            }

            var serialized = new SerializedObject(campaign);
            var scenes = serialized.FindProperty("levelScenes");
            if (scenes == null)
            {
                Debug.LogError($"{nameof(LevelBuilder)}: the campaign asset has no 'levelScenes' field.");
                return;
            }

            scenes.arraySize = levels.Count + 1;
            scenes.GetArrayElementAtIndex(0).stringValue = "Level01";
            for (var i = 0; i < levels.Count; i++)
                scenes.GetArrayElementAtIndex(i + 1).stringValue = levels[i].SceneName;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(campaign);
        }

        /// <summary>
        /// Every level scene has to be in the build, or loading the next one fails at run time. The list is
        /// rebuilt rather than added to: the title first, then the levels in order, then whatever else was in
        /// there (the template's sample scene), so a renamed level cannot leave a dead entry behind.
        /// </summary>
        static void UpdateBuildSettings(IReadOnlyList<Level> levels)
        {
            var ours = new List<string> { $"{SceneFolder}/Title.unity", $"{SceneFolder}/Level01.unity" };
            foreach (var level in levels) ours.Add($"{SceneFolder}/{level.SceneName}.unity");

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in ours)
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));

            foreach (var existing in EditorBuildSettings.scenes)
                if (!ours.Contains(existing.path)) scenes.Add(existing);

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static SerializedProperty Find(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{serialized.targetObject.name} has no field '{field}'.");
            return property;
        }

        static EnemyView LoadEnemy(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}/{name}.prefab");
            if (prefab == null) throw new InvalidOperationException($"No enemy prefab '{name}'.");
            return prefab.GetComponent<EnemyView>();
        }

        static BossView LoadBoss(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BossFolder}/{name}.prefab");
            if (prefab == null) throw new InvalidOperationException($"No boss prefab '{name}'.");
            return prefab.GetComponent<BossView>();
        }

        static MeteorDefinition LoadMeteor(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MeteorDefinition>($"{MeteorFolder}/{name}.asset");
            if (asset == null) throw new InvalidOperationException($"No meteor definition '{name}'.");
            return asset;
        }
    }
}
