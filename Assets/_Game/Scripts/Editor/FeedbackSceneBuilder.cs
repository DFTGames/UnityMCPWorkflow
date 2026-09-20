using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using YASS.Core;
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
        /// <summary>
        /// What flashes when it is damaged, and which sprite to flash. Naming the renderer matters: left to
        /// find its own, the flash would also whiten the boss's core (the player's one tell for when it can be
        /// hurt) and the player's shield bubble.
        /// The bosses and the generated enemies are not listed: their own builders wire their flashes, because
        /// they know which renderer is the hull. Two builders writing the same field is how the eight bosses
        /// would end up disagreeing with each other.
        /// </summary>
        static readonly (string Prefab, string Renderer)[] FlashPrefabs =
        {
            ("Assets/_Game/Prefabs/PlayerShip.prefab", "Visual"),
            ("Assets/_Game/Prefabs/Dart.prefab", null),
            ("Assets/_Game/Prefabs/Weaver.prefab", null),
            ("Assets/_Game/Prefabs/Meteor.prefab", null)
        };

        [MenuItem("Tools/YASS/Build Feedback Wiring")]
        public static void Build()
        {
            AddFlashToPrefabs();

            AddFeedbackTo(TitleSceneBuilder.ScenePath, withShaker: false, Track.Menu);
            foreach (var path in MenuSceneParts.LevelScenePaths()) AddFeedbackTo(path, withShaker: true, Track.Level);

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
        /// The mixer's groups carry the player's volumes, and the music player feeds the Music group
        /// (GDD "Audio Direction"). The level starts on its own theme and switches to the boss's when the
        /// warning sounds.
        /// </summary>
        static void AddAudioMixing(GameObject feedback, Track music)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixerBuilder.MixerPath);
            if (mixer == null)
            {
                Debug.LogError($"No mixer at {AudioMixerBuilder.MixerPath}; run Tools/YASS/Build Audio Mixer first.");
                return;
            }

            var volumes = feedback.AddComponent<MixerVolumes>();
            MenuSceneParts.Serialize(volumes, o => MenuSceneParts.Find(o, "mixer").objectReferenceValue = mixer);

            var musicObject = new GameObject("Music");
            musicObject.transform.SetParent(feedback.transform, false);
            var player = musicObject.AddComponent<MusicPlayer>();
            MenuSceneParts.Serialize(player, o =>
            {
                MenuSceneParts.Find(o, "output").objectReferenceValue = GroupNamed(mixer, AudioRules.MusicGroup);
                MenuSceneParts.Find(o, "playOnStart").enumValueIndex = (int)music;
                MenuSceneParts.Find(o, "menuTheme").objectReferenceValue = LoadTrack("MenuTheme");
                MenuSceneParts.Find(o, "levelTheme").objectReferenceValue = LoadTrack("Level01");
                MenuSceneParts.Find(o, "bossTheme").objectReferenceValue = LoadTrack("BossTheme");
            });
        }

        static AudioMixerGroup GroupNamed(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            if (groups.Length > 0) return groups[0];

            Debug.LogError($"The mixer has no {name} group.");
            return null;
        }

        static AudioClip LoadTrack(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Game/Audio/Music/{name}.wav");
            if (clip == null) Debug.LogWarning($"No music track named {name}.");

            return clip;
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

        static void AddFeedbackTo(string scenePath, bool withShaker, Track music)
        {
            var scene = MenuSceneParts.OpenForBuilding(scenePath, out var openedByBuilder);

            foreach (var existing in scene.GetRootGameObjects().Where(r => r != null && r.name == FeedbackObjectName).ToList())
                UnityEngine.Object.DestroyImmediate(existing);

            var go = new GameObject(FeedbackObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);

            EnsureAudioListener(scene);

            var audio = go.AddComponent<AudioDirector>();
            MenuSceneParts.Serialize(audio, o =>
            {
                MenuSceneParts.Find(o, "bank").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<SoundBank>(FeedbackBuilder.BankPath);

                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixerBuilder.MixerPath);
                MenuSceneParts.Find(o, "output").objectReferenceValue =
                    mixer == null ? null : GroupNamed(mixer, AudioRules.SfxGroup);
            });

            AddAudioMixing(go, music);

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
