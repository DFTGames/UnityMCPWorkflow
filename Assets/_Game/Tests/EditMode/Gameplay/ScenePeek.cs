using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Opens a scene in the Editor and looks inside it, for the tests that guard scene wiring. Shared because
    /// more than one of them needs the same three things: open it additively, find a component whatever it is
    /// parented to, and read a private serialised field.
    /// </summary>
    public static class ScenePeek
    {
        const string SceneFolder = "Assets/_Game/Scenes/";

        /// <summary>
        /// Opens the scene additively, runs the check, and closes it again. Additively, because opening a
        /// scene on top of a dirty one prompts, and a prompt in a test run hangs it.
        /// </summary>
        public static void In(string name, Action<Scene> check)
        {
            var path = SceneFolder + name + ".unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, "Missing scene " + path);

            // Asking for a scene the editor already has open returns that open copy rather than loading the
            // asset, so the test would read whatever is in the editor at the time. Allowed, because the test
            // run has to open one of these scenes anyway, but only when it has been saved: unsaved editor
            // state passing or failing a test about the asset is worse than either answer.
            var alreadyOpen = SceneManager.GetSceneByPath(path).isLoaded;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (alreadyOpen)
                Assert.That(scene.isDirty, Is.False,
                    name + " is open in the editor with unsaved changes, so this test would be checking " +
                    "those rather than the saved scene. Save it and run again.");

            try
            {
                check(scene);
            }
            finally
            {
                if (!alreadyOpen && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>The first component of this type in the scene, including inside inactive objects.</summary>
        public static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>Every component of this type in the scene, including inside inactive objects.</summary>
        public static System.Collections.Generic.List<T> FindAll<T>(Scene scene) where T : Component
        {
            var all = new System.Collections.Generic.List<T>();
            foreach (var root in scene.GetRootGameObjects()) all.AddRange(root.GetComponentsInChildren<T>(true));

            return all;
        }

        /// <summary>
        /// The component has to be there and awake. Inactive counts as missing: the search deliberately looks
        /// inside inactive objects so that a scene saved with a component switched off is caught here rather
        /// than doing nothing in the game, where a disabled component never reaches OnEnable.
        /// </summary>
        public static T Require<T>(Scene scene, string sceneName, string what) where T : Component
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

        /// <summary>
        /// Reads a private serialised reference and insists it points at something. This is the failure the
        /// scene tests exist for: a panel can be present, laid out and completely inert because the one field
        /// that connects it to its script is empty, and nothing else about the scene looks wrong.
        /// </summary>
        public static UnityEngine.Object RequireReference(Component owner, string field, string what)
        {
            var property = new SerializedObject(owner).FindProperty(field);
            Assert.That(property, Is.Not.Null, $"{owner.GetType().Name} has no field called {field}");
            Assert.That(property.objectReferenceValue, Is.Not.Null,
                $"{owner.GetType().Name}.{field} is empty on '{owner.name}', so {what}");

            return property.objectReferenceValue;
        }
    }
}
