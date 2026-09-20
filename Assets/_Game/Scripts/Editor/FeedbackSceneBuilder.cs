using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Feedback;

namespace YASS.Editor
{
    /// <summary>
    /// Puts the game-feel pieces into the scenes and prefabs: the audio director and effect pools in both
    /// scenes, the camera shaker on the level camera, and a <see cref="HitFlash"/> on everything that can be
    /// damaged (GDD "Art Direction", Visual effects). Re-runnable, like the other builders.
    /// </summary>
    public static class FeedbackSceneBuilder
    {
        const string FeedbackObjectName = "Feedback";
        const string LevelScenePath = "Assets/_Game/Scenes/Level01.unity";

        /// <summary>
        /// What flashes when it is damaged, and which sprite to flash. Naming the renderer matters: left to
        /// find its own, the flash would also whiten the boss's core (the player's one tell for when it can be
        /// hurt) and the player's shield bubble.
        /// </summary>
        static readonly (string Prefab, string Renderer)[] FlashPrefabs =
        {
            ("Assets/_Game/Prefabs/PlayerShip.prefab", "Visual"),
            ("Assets/_Game/Prefabs/Dart.prefab", null),
            ("Assets/_Game/Prefabs/Weaver.prefab", null),
            ("Assets/_Game/Prefabs/Meteor.prefab", null),
            ("Assets/_Game/Prefabs/HiveCarrier.prefab", "")
        };

        [MenuItem("Tools/YASS/Build Feedback Wiring")]
        public static void Build()
        {
            AddFlashToPrefabs();

            AddFeedbackTo(TitleSceneBuilder.ScenePath, withShaker: false);
            AddFeedbackTo(LevelScenePath, withShaker: true);

            AssetDatabase.SaveAssets();
            Debug.Log("Wired the feedback objects into the scenes and prefabs");
        }

        /// <summary>Anything that can be damaged flashes white when it is hit but survives.</summary>
        static void AddFlashToPrefabs()
        {
            foreach (var (path, rendererPath) in FlashPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"No prefab at {path}; skipped its hit flash.");
                    continue;
                }

                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var flash = contents.GetComponent<HitFlash>();
                    if (flash == null) flash = contents.AddComponent<HitFlash>();

                    MenuSceneParts.Serialize(flash, o =>
                    {
                        var array = MenuSceneParts.Find(o, "renderers");
                        var renderer = RendererFor(contents, rendererPath);
                        array.arraySize = renderer == null ? 0 : 1;
                        if (renderer != null) array.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                    });

                    PrefabUtility.SaveAsPrefabAsset(contents, path, out var saved);
                    if (!saved) Debug.LogError($"Failed to save {path} after adding its hit flash.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        /// <summary>
        /// The sprite to flash: the one on a named child, the root's own for an empty path, or none (let the
        /// component take every sprite it finds) when no path is given.
        /// </summary>
        static SpriteRenderer RendererFor(GameObject prefab, string rendererPath)
        {
            if (rendererPath == null) return null;

            var target = rendererPath.Length == 0 ? prefab.transform : prefab.transform.Find(rendererPath);
            if (target == null)
            {
                Debug.LogWarning($"{prefab.name} has no child '{rendererPath}' to flash.");
                return null;
            }

            return target.GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Without a listener in the scene, every sound plays into nothing and Unity says not a word about it.
        /// </summary>
        static void EnsureAudioListener(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<AudioListener>(true) != null) return;
            }

            var camera = scene.GetRootGameObjects()
                .Select(r => r.GetComponentInChildren<Camera>(true))
                .FirstOrDefault(c => c != null);

            if (camera != null) camera.gameObject.AddComponent<AudioListener>();
            else Debug.LogError($"No camera in {scene.path}: the scene has nowhere to hear from.");
        }

        static void AddFeedbackTo(string scenePath, bool withShaker)
        {
            var scene = MenuSceneParts.OpenForBuilding(scenePath, out var openedByBuilder);

            foreach (var existing in scene.GetRootGameObjects().Where(r => r != null && r.name == FeedbackObjectName).ToList())
                UnityEngine.Object.DestroyImmediate(existing);

            var go = new GameObject(FeedbackObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);

            EnsureAudioListener(scene);

            var audio = go.AddComponent<AudioDirector>();
            MenuSceneParts.Serialize(audio, o =>
                MenuSceneParts.Find(o, "bank").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<SoundBank>(FeedbackBuilder.BankPath));

            var spawner = go.AddComponent<EffectSpawner>();
            MenuSceneParts.Serialize(spawner, o =>
            {
                var array = MenuSceneParts.Find(o, "effects");
                var values = Enum.GetValues(typeof(Effect)).Cast<Effect>().Where(e => e != Effect.None).ToList();
                array.arraySize = values.Count;

                for (var i = 0; i < values.Count; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackBuilder.PrefabPathFor(values[i]));
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Effect").enumValueIndex = (int)values[i];
                    element.FindPropertyRelative("Prefab").objectReferenceValue =
                        prefab != null ? prefab.GetComponent<PooledEffect>() : null;
                }
            });

            if (withShaker)
            {
                var camera = scene.GetRootGameObjects()
                    .Select(r => r.GetComponentInChildren<Camera>(true))
                    .FirstOrDefault(c => c != null);

                if (camera == null) Debug.LogError($"No camera in {scenePath}: the screen shake has nothing to move.");
                else
                {
                    var shaker = go.AddComponent<CameraShaker>();
                    MenuSceneParts.Serialize(shaker, o =>
                        MenuSceneParts.Find(o, "target").objectReferenceValue = camera.transform);
                }
            }

            MenuSceneParts.SaveAndRelease(scene, scenePath, openedByBuilder);
        }
    }
}
