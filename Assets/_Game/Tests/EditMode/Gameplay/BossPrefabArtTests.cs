using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// A boss prefab must not carry its own body art.
    /// </summary>
    /// <remarks>
    /// The art is the heaviest thing in the game and is held by path rather than by reference, which is why
    /// it was taken out of the shared atlas: that halved the page, from 2048x2048 to 1024x1024. One `Sprite`
    /// reference on a prefab undoes it, because the prefab is referenced by its definition, the definition by
    /// the level, and the level by the run. Nothing would look wrong if it happened; the game would simply
    /// start holding eight bosses' art instead of one, and the only sign would be memory.
    ///
    /// That is exactly the kind of regression this project has had before, and it is one nudge of an engine
    /// in the Inspector away: <c>BossArtPreview</c> puts the art on screen while a prefab is open so the
    /// children can be placed, and marks it <c>DontSave</c> so it cannot be written back. This test is what
    /// proves that held, rather than trusting the flag.
    /// </remarks>
    public class BossPrefabArtTests
    {
        static IEnumerable<string> BossPrefabs()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.GetComponent<BossView>() != null) yield return path;
            }
        }

        [Test]
        public void EveryBoss_LoadsItsBodyArtByPath_AndKeepsNoneInThePrefab()
        {
            var found = 0;

            foreach (var path in BossPrefabs())
            {
                found++;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var body = prefab.GetComponent<SpriteRenderer>();

                Assert.That(body, Is.Not.Null, path + " has no body renderer to put art into at run time");
                Assert.That(body.sprite, Is.Null,
                    $"{path} has its body art baked in. That reference pulls the boss's sprite into the " +
                    "dependency graph of everything that points at this prefab, which is what taking it out " +
                    "of the atlas was for. The art belongs in the definition's SpritePath.");

                var definition = prefab.GetComponent<BossView>().Definition;
                Assert.That(definition, Is.Not.Null, path + " has no definition, so it can never find its art");
                Assert.That(definition.SpritePath, Is.Not.Empty,
                    path + " has no sprite path, so it would fight as an invisible boss");
                Assert.That(Resources.Load<Sprite>(definition.SpritePath), Is.Not.Null,
                    $"{path} points at Resources/{definition.SpritePath}, which does not exist");
            }

            Assert.That(found, Is.GreaterThan(0), "no boss prefabs were found, so this test proved nothing");
        }

        /// <summary>
        /// The editor's preview object must never reach the asset either. It is marked DontSave, but a
        /// prefab saved from a broken build, or by an older editor, could still carry one.
        /// </summary>
        [Test]
        public void NoBossPrefab_CarriesAPreviewObject()
        {
            foreach (var path in BossPrefabs())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(child.name, Does.Not.Contain("preview").IgnoreCase,
                        path + " has an editor preview object saved into it");
            }
        }
    }
}
