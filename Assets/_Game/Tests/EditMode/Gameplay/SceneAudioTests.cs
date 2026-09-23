using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Core;
using YASS.Feedback;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Every scene the player can be in has to carry its own sound (GDD "Audio Direction"). A scene that has
    /// lost its feedback objects looks exactly like one that is fine until somebody notices the silence, which
    /// is how the title screen's music once went missing.
    /// </summary>
    public class SceneAudioTests
    {
        const string SceneFolder = "Assets/_Game/Scenes/";

        static void InScene(string name, System.Action<Scene> check)
        {
            var path = SceneFolder + name + ".unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, "Missing scene " + path);

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                check(scene);
            }
            finally
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// The component has to be there and awake. Inactive counts as missing: the search deliberately looks
        /// inside inactive objects so that a scene saved with its feedback switched off is caught here rather
        /// than sounding broken in the game, where a disabled component never reaches OnEnable.
        /// </summary>
        static T Require<T>(Scene scene, string sceneName, string what) where T : Component
        {
            var found = Find<T>(scene);
            Assert.That(found, Is.Not.Null, $"{sceneName} has no {typeof(T).Name}: {what}");
            Assert.That(found.gameObject.activeInHierarchy, Is.True,
                $"{sceneName}'s {typeof(T).Name} sits on an inactive object, so it will never run");

            if (found is Behaviour behaviour)
                Assert.That(behaviour.enabled, Is.True,
                    $"{sceneName}'s {typeof(T).Name} is disabled, so it will never run");

            return found;
        }

        [TestCase("Title")]
        [TestCase("Level")]
        public void EveryPlayableScene_CanBeHeard(string name)
        {
            InScene(name, scene =>
            {
                Require<AudioDirector>(scene, name, "it is silent");
                Require<MusicPlayer>(scene, name, "there is no music");
                Require<AudioListener>(scene, name, "nothing would be heard");

                var volumes = Require<MixerVolumes>(scene, name, "the settings sliders would do nothing");

                // The reference is what carries the sliders to the groups: without it the component logs and
                // gives up, and every other assertion here still passes.
                var mixer = new SerializedObject(volumes).FindProperty("mixer");
                Assert.That(mixer, Is.Not.Null);
                Assert.That(mixer.objectReferenceValue, Is.Not.Null,
                    name + "'s mixer volumes have no mixer assigned, so the volume settings would do nothing");
            });
        }

        /// <summary>
        /// The settings are applied by a component in the scene, so a scene that lost it would open with the
        /// window setting ignored, looking exactly like a scene that is fine (see the audio above).
        /// </summary>
        [TestCase("Title")]
        [TestCase("Level")]
        public void EveryPlayableScene_AppliesTheSavedSettings(string name)
        {
            InScene(name, scene => Require<SettingsApplier>(scene, name, "saved settings would be ignored"));
        }

        /// <summary>The title plays the menu theme; a level or an Endless run plays the level theme.</summary>
        [TestCase("Title", Track.Menu)]
        [TestCase("Level", Track.Level)]
        public void EveryPlayableScene_StartsItsOwnTrack(string name, Track expected)
        {
            InScene(name, scene =>
            {
                var music = Find<MusicPlayer>(scene);
                Assert.That(music, Is.Not.Null, name + " has no music player");

                var playOnStart = new SerializedObject(music).FindProperty("playOnStart");
                Assert.That(playOnStart, Is.Not.Null);
                Assert.That((Track)playOnStart.enumValueIndex, Is.EqualTo(expected),
                    name + " starts the wrong music");
            });
        }
    }
}
