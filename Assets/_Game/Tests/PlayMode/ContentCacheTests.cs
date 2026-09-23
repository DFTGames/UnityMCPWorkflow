using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The cache is what turns the residency arithmetic into memory actually being given back. Every other
    /// test in the project asserts the bookkeeping (how many paths the cache thinks it holds), which a cache
    /// that never unloaded anything would satisfy perfectly. These ask the engine instead.
    /// </summary>
    /// <remarks>
    /// **How to ask.** Not with Unity's fake null: after <c>Resources.UnloadAsset</c> the managed wrapper is
    /// still there (the editor can rebuild the asset from the AssetDatabase if anything touches it again), so
    /// <c>texture == null</c> stays false however thoroughly the memory was freed. What does change is whether
    /// the engine still has the object loaded, which <see cref="Resources.FindObjectsOfTypeAll"/> reports.
    /// Verified both ways on Unity 6000.6.0f1: after unloading, fake-null says "live" and
    /// FindObjectsOfTypeAll says "gone".
    ///
    /// PlayMode rather than EditMode because that is where the game runs; the skies used here are ones no
    /// scene references, so nothing else can be holding them.
    /// </remarks>
    public class ContentCacheTests
    {
        const string First = "Skies/Sky01";
        const string Second = "Skies/Sky02";

        /// <summary>Whether the engine still has this texture loaded. The asset names are unique.</summary>
        static bool IsResident(string textureName)
        {
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (texture.name == textureName) return true;

            return false;
        }

        [Test]
        public void AnAssetNoLongerWanted_IsActuallyUnloaded()
        {
            var cache = new ContentCache();
            cache.Require<Sprite>(new[] { First });
            Assert.That(cache.Get<Sprite>(First), Is.Not.Null, "nothing was loaded, so this proves nothing");
            Assert.That(IsResident("Sky01"), Is.True);

            cache.Require<Sprite>(new[] { Second });

            Assert.That(IsResident("Sky01"), Is.False,
                "the texture was dropped from the cache but never unloaded, so no memory was reclaimed");

            cache.Clear();
        }

        [Test]
        public void Clearing_UnloadsEverything()
        {
            var cache = new ContentCache();
            cache.Require<Sprite>(new[] { First, Second });
            Assert.That(cache.Get<Sprite>(First), Is.Not.Null);
            Assert.That(cache.Get<Sprite>(Second), Is.Not.Null);

            cache.Clear();

            Assert.That(cache.Count, Is.Zero);
            Assert.That(IsResident("Sky01"), Is.False, "leaving a level has to give its memory back");
            Assert.That(IsResident("Sky02"), Is.False, "all of it, not just the first");
        }

        [Test]
        public void WhatIsStillWanted_IsNotDisturbed()
        {
            var cache = new ContentCache();
            cache.Require<Sprite>(new[] { First, Second });
            cache.Get<Sprite>(First);
            cache.Get<Sprite>(Second);

            cache.Require<Sprite>(new[] { First });

            Assert.That(IsResident("Sky01"), Is.True, "an asset wanted by both sets must not be freed");
            Assert.That(cache.Get<Sprite>(First), Is.Not.Null, "and must still be usable");
            Assert.That(IsResident("Sky02"), Is.False, "while the one dropped from the set goes");

            cache.Clear();
        }

        [Test]
        public void AskingForOneThatIsMissing_FailsLoudlyAndOnlyOnce()
        {
            var cache = new ContentCache();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("nothing loadable"));
            cache.Require<Sprite>(new[] { "Skies/NotASky" });
            Assert.That(cache.Get<Sprite>("Skies/NotASky"), Is.Null);

            // A second ask must not log again, or a missing sky would fill the console every time the set
            // changes, which in an Endless run is every eighty seconds.
            cache.Require<Sprite>(new[] { "Skies/NotASky" });
            cache.Clear();
        }

        [Test]
        public void TheTypeAskedFor_IsTheTypeReturned()
        {
            // A texture imported as a sprite holds both, and loading it untyped returns the texture: the
            // sprite cast then silently yields null and the sky goes blank.
            var cache = new ContentCache();
            cache.Require<Sprite>(new[] { First });

            Assert.That(cache.Get<Sprite>(First), Is.InstanceOf<Sprite>());

            cache.Clear();
        }
    }
}
