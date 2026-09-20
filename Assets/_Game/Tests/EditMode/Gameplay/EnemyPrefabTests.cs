using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Checks the enemy prefabs the builder produces. A hitbox that does not match the art, or a missing sprite,
    /// is invisible in the Inspector and silent at run time: one enemy simply cannot be hit where it looks solid.
    /// Re-run <c>Tools/YASS/Build Enemies</c> if any of these fail.
    /// </summary>
    public class EnemyPrefabTests
    {
        const string PrefabDir = "Assets/_Game/Prefabs/";

        /// <summary>The builder's own figures (GDD "Asset List", Target world sizes).</summary>
        const float WidthFraction = 0.8f;
        const float HeightFraction = 0.55f;

        static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, "Missing prefab " + name);
            return prefab;
        }

        [TestCase("Dart")]
        [TestCase("Weaver")]
        [TestCase("SwarmDrone")]
        [TestCase("Gunship")]
        [TestCase("Diver")]
        [TestCase("MineLayer")]
        [TestCase("Frigate")]
        [TestCase("Sniper")]
        public void EveryEnemy_HasItsOwnSpriteAndDefinition(string name)
        {
            var prefab = Load(name);
            var renderer = prefab.GetComponent<SpriteRenderer>();

            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null, name + " has no sprite");
            Assert.That(renderer.sprite.name, Is.EqualTo(name), "each enemy wears its own art, not the template's");

            var view = prefab.GetComponent<EnemyView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Definition, Is.Not.Null, name + " has no definition");
            Assert.That(view.Definition.name, Is.EqualTo(name));
        }

        [TestCase("SwarmDrone")]
        [TestCase("Gunship")]
        [TestCase("Diver")]
        [TestCase("MineLayer")]
        [TestCase("Frigate")]
        [TestCase("Sniper")]
        public void EveryEnemy_HasAHitboxThatMatchesItsArt(string name)
        {
            var prefab = Load(name);
            var sprite = prefab.GetComponent<SpriteRenderer>().sprite;
            var collider = prefab.GetComponent<CapsuleCollider2D>();

            Assert.That(collider, Is.Not.Null, name + " has no capsule collider");
            Assert.That(collider.isTrigger, Is.True, "all gameplay bodies use trigger colliders");
            Assert.That(collider.size.x, Is.EqualTo(sprite.bounds.size.x * WidthFraction).Within(1e-3f));
            Assert.That(collider.size.y, Is.EqualTo(sprite.bounds.size.y * HeightFraction).Within(1e-3f));
            Assert.That(prefab.transform.localScale.x, Is.EqualTo(1f).Within(1e-3f),
                "the sprite's pixels per unit carries the world size, so the prefab needs no scaling");
        }

        [TestCase("SwarmDrone")]
        [TestCase("Gunship")]
        [TestCase("Diver")]
        [TestCase("MineLayer")]
        [TestCase("Frigate")]
        [TestCase("Sniper")]
        public void EveryEnemy_FlashesOnlyItsOwnSprite(string name)
        {
            var prefab = Load(name);

            // Left empty, HitFlash sweeps up every child renderer, which would include the Sniper's beam.
            var flash = new SerializedObject(prefab.GetComponent<HitFlash>()).FindProperty("renderers");
            Assert.That(flash.arraySize, Is.EqualTo(1));
            Assert.That(flash.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.EqualTo(prefab.GetComponent<SpriteRenderer>()));
        }

        [Test]
        public void TheSniper_CarriesTheBeamItDraws()
        {
            var prefab = Load("Sniper");
            var beam = prefab.transform.Find("Beam");

            Assert.That(beam, Is.Not.Null, "the Sniper has no beam object");
            Assert.That(beam.gameObject.layer, Is.EqualTo(prefab.layer), "the beam belongs to the same layer");
            Assert.That(beam.GetComponent<BeamView>(), Is.Not.Null);
            Assert.That(new SerializedObject(prefab.GetComponent<EnemyView>()).FindProperty("beam")
                .objectReferenceValue, Is.Not.Null, "the enemy does not know about its beam");
        }

        [Test]
        public void TheMine_IsShootableAndItsOwnSize()
        {
            var prefab = Load("Mine");
            var sprite = prefab.GetComponent<SpriteRenderer>().sprite;
            var collider = prefab.GetComponent<CircleCollider2D>();

            Assert.That(sprite, Is.Not.Null, "an invisible mine is an unfair one");
            Assert.That(prefab.GetComponent<MineView>(), Is.Not.Null);
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(collider.radius, Is.EqualTo(sprite.bounds.size.x * 0.5f * WidthFraction).Within(1e-3f));
            Assert.That(collider.radius, Is.LessThan(MineSpec.TriggerRadius),
                "the blast reaches further than the hitbox: a mine goes off before it is touched");
            Assert.That(prefab.GetComponent<HitFlash>(), Is.Null,
                "a mine detonates rather than flashing, and two components must not own its colour");
        }

        /// <summary>Enemies and meteors draw at 1, enemy shots at 3 (GDD "Art Direction").</summary>
        [TestCase("SwarmDrone")]
        [TestCase("Gunship")]
        [TestCase("Diver")]
        [TestCase("MineLayer")]
        [TestCase("Frigate")]
        [TestCase("Sniper")]
        [TestCase("Mine")]
        public void EveryHazard_DrawsInTheHazardLayerAndOrder(string name)
        {
            var prefab = Load(name);

            Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("Hazard")));
            Assert.That(prefab.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(1));
            Assert.That(prefab.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        }
    }
}
