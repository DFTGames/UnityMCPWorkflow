using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Gameplay;
using Object = UnityEngine.Object;

namespace YASS.Editor
{
    /// <summary>
    /// Builds Endless mode (GDD "Core Loop", Endless mode): the definition listing the campaign's levels, and
    /// the scene that plays it, which is Level 1's scene with its wave script swapped for the Endless one.
    /// Re-runnable, like the other builders. Run it after <c>Tools/YASS/Build Levels</c>, whose levels it lists.
    /// </summary>
    public static class EndlessBuilder
    {
        const string TemplateScene = "Assets/_Game/Scenes/Level01.unity";
        const string EndlessScenePath = "Assets/_Game/Scenes/Endless.unity";
        const string DefinitionPath = "Assets/_Game/ScriptableObjects/Levels/Endless.asset";
        const string LevelFolder = "Assets/_Game/ScriptableObjects/Levels";
        const string CycleLabelName = "Cycle";

        [MenuItem("Tools/YASS/Build Endless")]
        public static void Build()
        {
            var definition = BuildDefinition();
            if (definition == null) return;

            var built = BuildScene(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (built) Debug.Log($"{nameof(EndlessBuilder)}: built Endless from {definition.LevelCount} levels.");
            else Debug.LogWarning($"{nameof(EndlessBuilder)}: the definition was updated but the scene was not.");
        }

        static EndlessDefinition BuildDefinition()
        {
            var levels = new List<LevelDefinition>();
            for (var number = 1; number <= 99; number++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelFolder}/Level{number:00}.asset");
                if (level == null) break;
                levels.Add(level);
            }

            if (levels.Count == 0)
            {
                Debug.LogError($"{nameof(EndlessBuilder)}: no campaign levels to draw from; build the levels first.");
                return null;
            }

            Directory.CreateDirectory(LevelFolder);
            var definition = AssetDatabase.LoadAssetAtPath<EndlessDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EndlessDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            var array = serialized.FindProperty("levels");
            array.arraySize = levels.Count;
            for (var i = 0; i < levels.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            if (definition.Validate() is string problem)
                Debug.LogError($"{nameof(EndlessBuilder)}: Endless is not playable: {problem}.");

            return definition;
        }

        /// <summary>
        /// The Endless scene is Level 1's, with the level definition taken off its runner and the Endless one put
        /// on: everything else a level needs (player, HUD, menus, feedback) is identical, and copying it keeps it
        /// that way. Regenerated every run, so it cannot drift.
        /// </summary>
        static bool BuildScene(EndlessDefinition definition)
        {
            var open = SceneManager.GetSceneByPath(EndlessScenePath);
            if (open.IsValid() && open.isLoaded)
            {
                Debug.LogWarning($"{nameof(EndlessBuilder)}: the Endless scene is open in the editor, so it was " +
                                 "left as it is. Close it and rebuild.");
                return false;
            }

            if (File.Exists(EndlessScenePath) && !AssetDatabase.DeleteAsset(EndlessScenePath))
            {
                Debug.LogError($"{nameof(EndlessBuilder)}: could not replace {EndlessScenePath}.");
                return false;
            }

            if (!AssetDatabase.CopyAsset(TemplateScene, EndlessScenePath))
            {
                Debug.LogError($"{nameof(EndlessBuilder)}: could not create the Endless scene from Level 1's.");
                return false;
            }

            var scene = MenuSceneParts.OpenForBuilding(EndlessScenePath, out var openedByBuilder);
            var wired = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                var runner = root.GetComponentInChildren<GameRunner>(true);
                if (runner == null) continue;

                var backdrop = FindBackdrop(scene);
                var serialized = new SerializedObject(runner);
                MenuSceneParts.Find(serialized, "endless").objectReferenceValue = definition;
                MenuSceneParts.Find(serialized, "level").objectReferenceValue = null;
                MenuSceneParts.Find(serialized, "backdrop").objectReferenceValue = backdrop;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                wired = true;
            }

            AddCycleLabel(scene);

            if (!wired)
                Debug.LogError($"{nameof(EndlessBuilder)}: no {nameof(GameRunner)} in the Endless scene.");

            // No layout pass: the menus came with the copy, already laid out by the builder that made them.
            MenuSceneParts.SaveAndRelease(scene, EndlessScenePath, openedByBuilder, layout: false);
            AddToBuildSettings();
            return wired;
        }

        /// <summary>
        /// The HUD's cycle counter, which only Endless has anything to put in. It is copied from the kills
        /// label rather than built from scratch, so it wears the HUD's own font, size and anchoring, and sits
        /// directly under it.
        /// </summary>
        static void AddCycleLabel(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var hud = root.GetComponentInChildren<HudView>(true);
                if (hud == null) continue;

                var serialized = new SerializedObject(hud);
                var field = MenuSceneParts.Find(serialized, "cycleText");

                var kills = (TMP_Text)MenuSceneParts.Find(serialized, "killsText").objectReferenceValue;
                if (kills == null)
                {
                    Debug.LogWarning($"{nameof(EndlessBuilder)}: the HUD has no kills label to copy, so the " +
                                     "cycle counter was not added.");
                    return;
                }

                var existing = kills.transform.parent.Find(CycleLabelName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                // Read the HUD's real geometry, not whatever the rects happen to hold before a layout pass.
                Canvas.ForceUpdateCanvases();

                var cycle = Object.Instantiate(kills, kills.transform.parent, false);
                cycle.name = CycleLabelName;
                cycle.text = "Cycle 1";
                cycle.transform.localScale = kills.transform.localScale;

                var from = kills.rectTransform;
                var rect = cycle.rectTransform;
                rect.anchorMin = from.anchorMin;
                rect.anchorMax = from.anchorMax;
                rect.pivot = from.pivot;
                rect.sizeDelta = from.sizeDelta;
                rect.anchoredPosition = from.anchoredPosition - new Vector2(0f, from.rect.height + 4f);

                field.objectReferenceValue = cycle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Debug.LogWarning($"{nameof(EndlessBuilder)}: no HUD in the Endless scene, so it cannot show the cycle.");
        }

        static SpriteRenderer FindBackdrop(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Background") return root.GetComponent<SpriteRenderer>();

            Debug.LogWarning($"{nameof(EndlessBuilder)}: no Background object, so cycles will not change the sky.");
            return null;
        }

        /// <summary>Endless is loaded by name, so it has to be in the build like any other scene.</summary>
        static void AddToBuildSettings()
        {
            // The scene was deleted and copied again, so it has a new GUID: an entry kept from last time would
            // still point at the old one.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == EndlessScenePath);
            scenes.Add(new EditorBuildSettingsScene(EndlessScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
