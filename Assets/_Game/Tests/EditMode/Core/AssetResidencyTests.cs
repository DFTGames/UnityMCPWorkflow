using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The game holds only the assets it is using: a level's own sky and boss, and in Endless the two skies a
    /// dissolve needs. This is the arithmetic behind that, kept away from the engine so it can be tested.
    /// </summary>
    public class AssetResidencyTests
    {
        readonly List<string> _load = new List<string>();
        readonly List<string> _free = new List<string>();

        static AssetResidency Holding(params string[] keys)
        {
            var residency = new AssetResidency();
            foreach (var key in keys) residency.NoteLoaded(key);
            return residency;
        }

        [Test]
        public void NothingHeld_LoadsEverythingAsked()
        {
            var residency = new AssetResidency();

            residency.Plan(new[] { "sky/a", "boss/b" }, _load, _free);

            Assert.That(_load, Is.EquivalentTo(new[] { "sky/a", "boss/b" }));
            Assert.That(_free, Is.Empty);
        }

        [Test]
        public void WhatIsAlreadyHeld_IsNotLoadedAgain()
        {
            var residency = Holding("sky/a");

            residency.Plan(new[] { "sky/a", "sky/b" }, _load, _free);

            Assert.That(_load, Is.EquivalentTo(new[] { "sky/b" }), "a held asset must not be loaded twice");
            Assert.That(_free, Is.Empty, "and must not be freed when it is still wanted");
        }

        [Test]
        public void WhatIsNoLongerWanted_IsFreed()
        {
            var residency = Holding("level01/sky", "level01/boss");

            residency.Plan(new[] { "level02/sky", "level02/boss" }, _load, _free);

            Assert.That(_load, Is.EquivalentTo(new[] { "level02/sky", "level02/boss" }));
            Assert.That(_free, Is.EquivalentTo(new[] { "level01/sky", "level01/boss" }),
                "the level that has been left has to give its memory back");
        }

        [Test]
        public void AnAssetSharedByBoth_SurvivesTheChange()
        {
            // Two levels can want the same sky; freeing and reloading it would be a stall for nothing.
            var residency = Holding("shared/sky", "level01/boss");

            residency.Plan(new[] { "shared/sky", "level02/boss" }, _load, _free);

            Assert.That(_load, Is.EquivalentTo(new[] { "level02/boss" }));
            Assert.That(_free, Is.EquivalentTo(new[] { "level01/boss" }));
        }

        [Test]
        public void AskingForTheSameSetTwice_ChangesNothing()
        {
            var residency = Holding("sky/a", "sky/b");

            residency.Plan(new[] { "sky/a", "sky/b" }, _load, _free);

            Assert.That(_load, Is.Empty);
            Assert.That(_free, Is.Empty);
        }

        [Test]
        public void AskingForNothing_FreesTheLot()
        {
            // Leaving a level for the menus: nothing is being played, so nothing should be held.
            var residency = Holding("sky/a", "boss/b");

            residency.Plan(new string[0], _load, _free);
            Assert.That(_free, Is.EquivalentTo(new[] { "sky/a", "boss/b" }));

            residency.Plan(null, _load, _free);
            Assert.That(_free, Is.EquivalentTo(new[] { "sky/a", "boss/b" }), "null means nothing is wanted");
        }

        [Test]
        public void ThePlanIsOnlyAPlan_UntilTheCallerSaysItHappened()
        {
            // Loading is asynchronous, so residency follows what actually landed, not what was intended.
            var residency = new AssetResidency();
            residency.Plan(new[] { "sky/a" }, _load, _free);

            Assert.That(residency.Holds("sky/a"), Is.False, "nothing is held until the load finishes");

            residency.NoteLoaded("sky/a");
            Assert.That(residency.Holds("sky/a"), Is.True);

            residency.NoteFreed("sky/a");
            Assert.That(residency.Holds("sky/a"), Is.False);
        }

        [Test]
        public void ADuplicateInTheRequest_IsLoadedOnce()
        {
            var residency = new AssetResidency();

            residency.Plan(new[] { "sky/a", "sky/a" }, _load, _free);

            Assert.That(_load, Is.EqualTo(new[] { "sky/a" }));
        }

        [Test]
        public void EmptyKeys_AreIgnored()
        {
            // A level with no boss, or a definition whose path was never filled in: not an asset to load.
            var residency = new AssetResidency();

            residency.Plan(new[] { "sky/a", null, "" }, _load, _free);

            Assert.That(_load, Is.EqualTo(new[] { "sky/a" }));
            Assert.That(residency.Holds(null), Is.False);
        }

        [Test]
        public void Clearing_ForgetsEverything()
        {
            var residency = Holding("sky/a", "sky/b");

            residency.Clear();

            Assert.That(residency.Count, Is.Zero);
            residency.Plan(new[] { "sky/a" }, _load, _free);
            Assert.That(_load, Is.EqualTo(new[] { "sky/a" }), "after a teardown everything has to be loaded again");
        }
    }
}
