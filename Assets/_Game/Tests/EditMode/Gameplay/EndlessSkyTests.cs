using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The Endless sky is a pair of renderers in the level scene and a ring of sprites on the Endless
    /// definition (GDD "Core Loop", Endless mode, and "Art Direction", Endless skies). Every part of it is a
    /// serialised reference, which is the kind of thing that goes missing without anything failing: the scene
    /// still opens, the run still plays, and the sky simply never moves.
    /// </summary>
    public class EndlessSkyTests
    {
        const string ScenePath = "Assets/_Game/Scenes/Level.unity";
        const string DefinitionPath = "Assets/_Game/ScriptableObjects/Levels/Endless.asset";

        /// <summary>The starfield sits at -10 (GDD "Art Direction"), so both skies have to stay under it.</summary>
        const int StarfieldOrder = -10;

        static EndlessDefinition Definition =>
            AssetDatabase.LoadAssetAtPath<EndlessDefinition>(DefinitionPath);

        [Test]
        public void TheRing_HasEnoughSkiesToDissolveBetween()
        {
            var definition = Definition;
            Assert.That(definition, Is.Not.Null, "no Endless definition at " + DefinitionPath);
            Assert.That(definition.SkyPaths.Count, Is.GreaterThanOrEqualTo(2),
                "a ring of one sky can only stand still, and of none can only cut");

            foreach (var path in definition.SkyPaths)
                Assert.That(Resources.Load<Sprite>(path), Is.Not.Null, $"no sky at Resources/{path}");
            Assert.That(definition.Validate(), Is.Null);
        }

        [Test]
        public void TheSkies_AreAllTheSameSize()
        {
            // They are drawn on top of one another, so one that is a different size would slide or crop
            // halfway through every dissolve.
            var paths = Definition.SkyPaths;
            var first = Resources.Load<Sprite>(paths[0]);

            foreach (var path in paths)
            {
                var sky = Resources.Load<Sprite>(path);
                Assert.That(sky.rect.size, Is.EqualTo(first.rect.size), $"{sky.name} is not the size of the others");
                Assert.That(sky.pixelsPerUnit, Is.EqualTo(first.pixelsPerUnit),
                    $"{sky.name} would cover a different part of the world");
            }
        }

        [Test]
        public void TheScene_HasBothHalvesOfADissolve()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SkyView view = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    view = root.GetComponentInChildren<SkyView>(true);
                    if (view != null) break;
                }

                Assert.That(view, Is.Not.Null, "the level scene has no sky view, so an Endless run could not drift");

                var serialized = new SerializedObject(view);
                var showing = serialized.FindProperty("showing").objectReferenceValue as SpriteRenderer;
                var arriving = serialized.FindProperty("arriving").objectReferenceValue as SpriteRenderer;

                Assert.That(showing, Is.Not.Null, "no renderer for the sky being left");
                Assert.That(arriving, Is.Not.Null, "no renderer for the sky arriving, so there is nothing to fade");
                Assert.That(arriving, Is.Not.EqualTo(showing), "both halves are the same renderer");

                Assert.That(arriving.sortingOrder, Is.GreaterThan(showing.sortingOrder),
                    "the arriving sky is behind the one it replaces, so the dissolve would never be seen");
                Assert.That(arriving.sortingOrder, Is.LessThan(StarfieldOrder),
                    "the arriving sky is drawn over the starfield");

                Assert.That(arriving.color.a, Is.EqualTo(0f).Within(0.001f),
                    "the arriving sky starts visible, so the run opens on two skies at once");
                Assert.That(arriving.transform.lossyScale, Is.EqualTo(showing.transform.lossyScale),
                    "the two skies are different sizes on screen");
            }
            finally
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheRunner_KnowsAboutTheSky()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameRunner runner = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    runner = root.GetComponentInChildren<GameRunner>(true);
                    if (runner != null) break;
                }

                Assert.That(runner, Is.Not.Null, "no runner in the level scene");
                Assert.That(new SerializedObject(runner).FindProperty("sky").objectReferenceValue, Is.Not.Null,
                    "the runner has no sky to drive, so it would sit on one backdrop for ever");
            }
            finally
            {
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
