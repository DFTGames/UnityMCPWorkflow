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

        [TestCase("PlayerShip", false)]
        [TestCase("Dart", true)]
        [TestCase("Weaver", true)]
        public void Ships_HaveALocalSpaceEngineBehindTheShip(string prefabName, bool facesLeft)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/" + prefabName + ".prefab");
            var engine = prefab.GetComponentInChildren<EngineExhaust>();
            Assert.That(engine, Is.Not.Null, prefabName + " has no EngineExhaust");

            var serialized = new SerializedObject(engine);
            var particles = engine.GetComponent<ParticleSystem>();
            Assert.That(serialized.FindProperty("particles").objectReferenceValue, Is.SameAs(particles));
            Assert.That(serialized.FindProperty("shipFacesLeft").boolValue, Is.EqualTo(facesLeft));

            var main = particles.main;
            Assert.That(main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local));
            Assert.That(main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Hierarchy));
            Assert.That(main.startSpeed.constant, Is.EqualTo(0f), "exhaust direction comes from velocity over lifetime only");

            var velocity = particles.velocityOverLifetime;
            Assert.That(velocity.enabled, Is.True);
            Assert.That(velocity.space, Is.EqualTo(ParticleSystemSimulationSpace.Local));
            Assert.That(velocity.x.mode, Is.EqualTo(ParticleSystemCurveMode.Constant));
            Assert.That(velocity.y.mode, Is.EqualTo(ParticleSystemCurveMode.Constant));
            Assert.That(velocity.z.mode, Is.EqualTo(ParticleSystemCurveMode.Constant));
            Assert.That(particles.emission.rateOverTime.mode, Is.EqualTo(ParticleSystemCurveMode.Constant));

            var rearSign = facesLeft ? 1f : -1f;
            Assert.That(Mathf.Sign(engine.transform.localPosition.x), Is.EqualTo(rearSign), "engine sits at the rear");

            var engineRenderer = engine.GetComponent<ParticleSystemRenderer>();
            Assert.That(engineRenderer.sortingOrder, Is.LessThan(ShipSprite(prefab).sortingOrder));
            Assert.That(engineRenderer.sharedMaterial.name, Is.EqualTo("EngineExhaust"));
        }

        [TestCase("PlayerShip", false)]
        [TestCase("Dart", true)]
        public void EngineThrottle_ScalesRateSpeedAndBothEndsOfTheSizeRange(string prefabName, bool facesLeft)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/" + prefabName + ".prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var engine = instance.GetComponentInChildren<EngineExhaust>();
                var particles = engine.GetComponent<ParticleSystem>();
                var serialized = new SerializedObject(engine);
                float Range(string field, string end) => serialized.FindProperty(field + "." + end).floatValue;
                var authored = particles.main.startSize;
                var baseMin = authored.constantMin;
                var baseMax = authored.constantMax;
                var sign = facesLeft ? 1f : -1f;

                engine.SetThrottle(0f);
                Assert.That(particles.emission.rateOverTime.constant, Is.EqualTo(Range("emissionRate", "atZero")).Within(1e-4f));
                Assert.That(particles.velocityOverLifetime.x.constant, Is.EqualTo(sign * Range("exhaustSpeed", "atZero")).Within(1e-4f));
                Assert.That(particles.main.startSize.constantMin, Is.EqualTo(baseMin * Range("sizeScale", "atZero")).Within(1e-4f));
                Assert.That(particles.main.startSize.constantMax, Is.EqualTo(baseMax * Range("sizeScale", "atZero")).Within(1e-4f));

                engine.SetThrottle(1f);
                Assert.That(particles.emission.rateOverTime.constant, Is.EqualTo(Range("emissionRate", "atFull")).Within(1e-4f));
                Assert.That(particles.velocityOverLifetime.x.constant, Is.EqualTo(sign * Range("exhaustSpeed", "atFull")).Within(1e-4f));
                Assert.That(particles.main.startSize.constantMin, Is.EqualTo(baseMin * Range("sizeScale", "atFull")).Within(1e-4f));
                Assert.That(particles.main.startSize.constantMax, Is.EqualTo(baseMax * Range("sizeScale", "atFull")).Within(1e-4f));
                Assert.That(particles.main.startSize.constantMax, Is.LessThan(0.5f), "particles stay smaller than the ship");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>The player ship keeps its sprite on a "Visual" child (tilted for aiming); enemies on the root.</summary>
        static SpriteRenderer ShipSprite(GameObject prefab)
        {
            var visual = prefab.transform.Find("Visual");
            return visual != null ? visual.GetComponent<SpriteRenderer>() : prefab.GetComponent<SpriteRenderer>();
        }

        [Test]
        public void PlayerShip_TiltsItsVisualsButNotItsHitbox()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/PlayerShip.prefab");
            var visual = prefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);

            var view = new SerializedObject(prefab.GetComponent<PlayerShipView>());
            Assert.That(view.FindProperty("visual").objectReferenceValue, Is.SameAs(visual));
            Assert.That(view.FindProperty("shipRenderer").objectReferenceValue, Is.SameAs(visual.GetComponent<SpriteRenderer>()));
            Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Null, "the sprite moved to Visual");
            Assert.That(prefab.GetComponent<CapsuleCollider2D>(), Is.Not.Null, "the hitbox stays on the root");
            Assert.That(visual.GetComponentInChildren<EngineExhaust>(), Is.Not.Null, "the engine tilts with the ship");
            Assert.That(visual.GetComponentsInChildren<Collider2D>(), Is.Empty, "nothing under Visual may collide");
            Assert.That(prefab.transform.Find("Shield"), Is.Not.Null, "the shield bubble stays on the root, untilted");
            // The tilt limit is not serialised: the view reads GameTuning.FiringArcDegrees so the visible nose and
            // the shot direction cannot drift apart (GDD "Controls", Firing arc and aim tilt).
            Assert.That(view.FindProperty("maxTiltDegrees"), Is.Null, "the arc must have a single source of truth");
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
