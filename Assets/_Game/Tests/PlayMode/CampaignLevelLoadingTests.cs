using NUnit.Framework;
using UnityEngine;
using YASS.Core;
using YASS.Gameplay;
using Object = UnityEngine.Object;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// One scene plays all eight levels, so the only thing that decides which level a player is actually in is
    /// <c>GameRunner.ResolveLevel</c> (GDD "Levels"). Nothing else proves it: every other scene test plays the
    /// level assigned in the scene, so a run that silently replayed level 1 eight times would pass them all.
    /// </summary>
    public class CampaignLevelLoadingTests : LevelSceneFixture
    {
        static Campaign TheCampaign => LoadAsset<Campaign>("Assets/_Game/Resources/Campaign.asset");

        /// <summary>
        /// Starts a campaign run sitting on its last level, so the scene loads something other than its own
        /// fallback. The last one rather than the second because it is the furthest thing from the fallback:
        /// a different backdrop, a different boss and a different number of waves.
        /// </summary>
        protected override void ConfigureRun()
        {
            var run = TheCampaign.NewRun(Difficulty.Pilot);
            run.SkipToFinalLevel();
            RunContext.Begin(run);
        }

        [Test]
        public void TheRunnerPlays_TheLevelTheRunHasReached()
        {
            var campaign = TheCampaign;
            var expected = campaign.LevelFor(campaign.LevelCount - 1);
            Assert.That(expected, Is.Not.Null, "the campaign has no last level");

            Assert.That(Runner.Director.WaveCount, Is.EqualTo(expected.Waves.Count),
                $"the wave script is not {expected.name}'s: the run's level was not the one loaded");
        }

        [Test]
        public void TheSkyIsTheLevelsOwn_NotTheScenesOwn()
        {
            // The backdrop is the most visible half of this: it used to be baked into each level's scene, and
            // is now painted from the level's data. A wrong level here means the player sees the wrong place.
            var campaign = TheCampaign;
            var expected = Resources.Load<Sprite>(campaign.LevelFor(campaign.LevelCount - 1).BackdropPath);
            Assert.That(expected, Is.Not.Null, "the last level has no backdrop");

            var sky = Object.FindAnyObjectByType<SkyView>();
            Assert.That(sky, Is.Not.Null, "no sky view in the level scene");

            var showing = new SerializedShowingSprite(sky);
            Assert.That(showing.Sprite, Is.EqualTo(expected),
                $"the sky shows {showing.Name}, not {expected.name}");
        }

        [Test]
        public void TheBossIsTheLevelsOwn()
        {
            var campaign = TheCampaign;
            var expected = campaign.LevelFor(campaign.LevelCount - 1).BossPrefab;
            Assert.That(expected, Is.Not.Null);

            Runner.StartBossFightNow();
            Assert.That(Runner.Boss, Is.Not.Null, "no boss arrived");
            Assert.That(Runner.Boss.name, Does.StartWith(expected.name),
                "the boss is not the one the run's level says");
        }

        /// <summary>The sprite the sky is actually showing, read off the renderer rather than the data.</summary>
        readonly struct SerializedShowingSprite
        {
            readonly SpriteRenderer _renderer;

            public SerializedShowingSprite(SkyView view)
            {
                // The renderer sits on the same object as the view; the arriving one is its child.
                _renderer = view.GetComponent<SpriteRenderer>();
            }

            public Sprite Sprite => _renderer != null ? _renderer.sprite : null;
            public string Name => Sprite != null ? Sprite.name : "nothing";
        }
    }
}
