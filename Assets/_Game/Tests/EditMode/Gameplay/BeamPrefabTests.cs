using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// What a beam needs from its prefab and its material to be a beam at all.
    /// </summary>
    /// <remarks>
    /// These exist because of a failure worth remembering. The beam was given an additive material by
    /// copying the one the particle effects use, which is URP's <c>Particles/Unlit</c>. It drew, and the
    /// sprite's texture even bound, so every screenshot looked right. But a particle shader declares no
    /// <c>_RendererColor</c>, which is how a <see cref="SpriteRenderer"/> hands over its <c>color</c>, so
    /// every tint, the brightness shimmer and the whole build of the warning were computed correctly and
    /// thrown away one step before the pixel. The telegraph went from invisible to full instantly, which is
    /// a change to how the fight plays, not to how it looks.
    ///
    /// Nothing caught it. <c>BeamLookTests</c> passed throughout, because the arithmetic was never wrong;
    /// it was unreachable. That is the same shape as the failures CLAUDE.md already records, so the guard
    /// belongs here, on the seam between the rule and the pixel, rather than on the rule.
    /// </remarks>
    public class BeamPrefabTests
    {
        static IEnumerable<GameObject> WithBeams()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.GetComponentInChildren<BeamView>(true) != null) yield return prefab;
            }
        }

        [Test]
        public void EveryBeamMaterial_CanActuallyReceiveTheColourTheGameSetsOnIt()
        {
            var found = 0;

            foreach (var prefab in WithBeams())
            foreach (var part in PartsOf(prefab))
            {
                found++;

                var material = part.sharedMaterial;
                Assert.That(material, Is.Not.Null, $"{prefab.name}/{part.name} has no material");

                Assert.That(material.HasProperty("_RendererColor"), Is.True,
                    $"{prefab.name}/{part.name} uses '{material.shader.name}', which declares no " +
                    "_RendererColor. A SpriteRenderer delivers its colour through that property, so every " +
                    "tint, the brightness shimmer and the warning's fade would be silently discarded.");

                Assert.That(material.HasProperty("_MainTex"), Is.True,
                    $"{prefab.name}/{part.name} uses '{material.shader.name}', which declares no _MainTex, " +
                    "so the sprite's own texture has nowhere to bind.");
            }

            Assert.That(found, Is.GreaterThan(0), "no beam parts were found, so this test proved nothing");
        }

        /// <summary>
        /// All three parts, on every prefab. Lose one and the beam quietly goes back to being the flat
        /// rectangle this replaced: <c>Lay</c>, <c>ShowFlare</c> and <c>Hide</c> all null-check in silence.
        /// </summary>
        [Test]
        public void EveryBeam_HasItsThreeLayers()
        {
            foreach (var prefab in WithBeams())
            {
                var beam = prefab.GetComponentInChildren<BeamView>(true);
                var so = new SerializedObject(beam);

                foreach (var part in new[] { "shaft", "glow", "flare" })
                    Assert.That(so.FindProperty(part).objectReferenceValue, Is.Not.Null,
                        $"{prefab.name} has no {part}, so its beam draws flat");
            }
        }

        /// <summary>
        /// The beam's three layers are order-independent among themselves, because addition does not care
        /// what order it happens in. That argument stops at the edge of the additive set: an enemy bullet is
        /// alpha blended, and two renderers sharing a sorting order are tie-broken in a way that is not
        /// stable frame to frame, so a bullet crossing the beam would punch a hole through it on some frames
        /// and not others. The beam therefore gets a band of its own.
        /// </summary>
        [Test]
        public void TheBeam_DoesNotShareItsSortingOrderWithAlphaBlendedShots()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/EnemyProjectile.prefab");

            Assert.That(projectile, Is.Not.Null, "the enemy projectile prefab has moved");
            var shotOrder = projectile.GetComponentInChildren<SpriteRenderer>(true).sortingOrder;

            foreach (var prefab in WithBeams())
            foreach (var part in PartsOf(prefab))
                Assert.That(part.sortingOrder, Is.Not.EqualTo(shotOrder),
                    $"{prefab.name}/{part.name} shares sorting order {shotOrder} with enemy shots");
        }

        /// <summary>Every part of a beam must be out until something asks for it.</summary>
        [Test]
        public void EveryBeam_ShipsSwitchedOff()
        {
            foreach (var prefab in WithBeams())
            foreach (var part in PartsOf(prefab))
                Assert.That(part.enabled, Is.False,
                    $"{prefab.name}/{part.name} is on in the prefab, so it appears the moment one spawns");
        }

        static IEnumerable<SpriteRenderer> PartsOf(GameObject prefab)
        {
            var beam = prefab.GetComponentInChildren<BeamView>(true);
            var so = new SerializedObject(beam);

            foreach (var name in new[] { "shaft", "glow", "flare" })
            {
                var part = so.FindProperty(name).objectReferenceValue as SpriteRenderer;
                if (part != null) yield return part;
            }
        }
    }
}
