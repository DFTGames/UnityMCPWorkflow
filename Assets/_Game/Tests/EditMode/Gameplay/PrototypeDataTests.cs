using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Checks that the tuning data assets match the provisional values recorded in the GDD
    /// ("Enemies and Hazards" and "Mechanics"). Update the GDD first, then the asset, then this test.
    /// </summary>
    public class PrototypeDataTests
    {
        const string DataDir = "Assets/_Game/ScriptableObjects/";

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(DataDir + path);
            Assert.That(asset, Is.Not.Null, "Missing " + path);
            return asset;
        }

        [Test]
        public void Player_MatchesGdd()
        {
            var player = Load<PlayerDefinition>("Player/Player.asset");

            Assert.That(player.Speed, Is.EqualTo(8f));
            Assert.That(player.EdgeMargin, Is.EqualTo(0.5f));
            Assert.That(player.ProjectileSpeed, Is.EqualTo(18f));
            Assert.That(player.ProjectileDamage, Is.EqualTo(1f));
        }

        [Test]
        public void Dart_MatchesGdd()
        {
            var dart = Load<EnemyDefinition>("Enemies/Dart.asset");

            Assert.That(dart.Size, Is.EqualTo(EnemySize.Small));
            Assert.That(dart.MaxHealth, Is.EqualTo(1f));
            Assert.That(dart.Speed, Is.EqualTo(7f));
            Assert.That(dart.ContactDamage, Is.EqualTo(20f));
            Assert.That(dart.Pattern, Is.EqualTo(MotionPattern.Straight));
            Assert.That(dart.Fires, Is.False);
        }

        [Test]
        public void Weaver_MatchesGdd()
        {
            var weaver = Load<EnemyDefinition>("Enemies/Weaver.asset");

            Assert.That(weaver.Size, Is.EqualTo(EnemySize.Small));
            Assert.That(weaver.MaxHealth, Is.EqualTo(3f));
            Assert.That(weaver.Speed, Is.EqualTo(3f));
            Assert.That(weaver.ContactDamage, Is.EqualTo(20f));
            Assert.That(weaver.Pattern, Is.EqualTo(MotionPattern.SineWave));
            Assert.That(weaver.WaveAmplitude, Is.EqualTo(1.5f));
            Assert.That(weaver.WaveFrequency, Is.EqualTo(0.4f));
            Assert.That(weaver.Fires, Is.True);
            Assert.That(weaver.FireInterval, Is.EqualTo(2f));
            Assert.That(weaver.FirstShotDelay, Is.EqualTo(0.8f));
            Assert.That(weaver.BulletSpeed, Is.EqualTo(6f));
            Assert.That(weaver.BulletDamage, Is.EqualTo(10f));
        }

        [TestCase("MeteorLarge", true, MeteorSize.Large, 4f, 1.5f, 2.5f, 25f, 1.6f)]
        [TestCase("MeteorMedium", true, MeteorSize.Medium, 2f, 2f, 3f, 15f, 1.0f)]
        [TestCase("MeteorSmall", true, MeteorSize.Small, 1f, 2.5f, 3.5f, 10f, 0.6f)]
        [TestCase("MeteorSolid", false, MeteorSize.Large, 8f, 1f, 2f, 30f, 1.4f)]
        public void Meteors_MatchGdd(string name, bool splitting, MeteorSize size, float health, float minSpeed,
            float maxSpeed, float contactDamage, float scale)
        {
            var meteor = Load<MeteorDefinition>("Meteors/" + name + ".asset");

            Assert.That(meteor.Splitting, Is.EqualTo(splitting));
            Assert.That(meteor.Size, Is.EqualTo(size));
            Assert.That(meteor.MaxHealth, Is.EqualTo(health));
            Assert.That(meteor.MinSpeed, Is.EqualTo(minSpeed));
            Assert.That(meteor.MaxSpeed, Is.EqualTo(maxSpeed));
            Assert.That(meteor.ContactDamage, Is.EqualTo(contactDamage));
            Assert.That(meteor.Scale, Is.EqualTo(scale));
            Assert.That(meteor.MaxVerticalSpeed, Is.EqualTo(0.5f));
            Assert.That(meteor.MaxSpinDegrees, Is.EqualTo(60f));
            Assert.That(meteor.Sprite, Is.Not.Null);
        }

        [Test]
        public void SplittingMeteors_ChainLargeToMediumToSmall()
        {
            var large = Load<MeteorDefinition>("Meteors/MeteorLarge.asset");

            Assert.That(large.Fragment, Is.SameAs(Load<MeteorDefinition>("Meteors/MeteorMedium.asset")));
            Assert.That(large.Fragment.Fragment, Is.SameAs(Load<MeteorDefinition>("Meteors/MeteorSmall.asset")));
            Assert.That(large.Fragment.Fragment.Fragment, Is.Null);
        }
    }
}
