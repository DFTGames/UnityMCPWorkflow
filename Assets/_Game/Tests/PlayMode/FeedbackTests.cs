using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Feedback;
using YASS.Gameplay;
using YASS.UI;
using Object = UnityEngine.Object;

namespace YASS.Tests.Feedback
{
    /// <summary>
    /// Game feel in the real scene (GDD "Art Direction", Visual effects; "Audio Direction"): effects and sounds
    /// fire on the events that should produce them, and the screen shake honours its setting.
    /// </summary>
    public class FeedbackTests
    {
        const string LevelScene = "Level01";

        bool _previousRunInBackground;
        GameRunner _runner;

        sealed class MemoryStore : ISettingsStore
        {
            readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();

            public float GetFloat(string key, float fallback) => _floats.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) => _floats[key] = value;
            public bool GetBool(string key, bool fallback) => _bools.TryGetValue(key, out var v) ? v : fallback;
            public void SetBool(string key, bool value) => _bools[key] = value;
            public void Save() { }
        }

        static AudioDirector Audio => AudioDirector.Instance;
        static CameraShaker Shaker => CameraShaker.Instance;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Loads prefabs through the AssetDatabase, so runs in the Editor only.");
#endif
            _previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            RunContext.Clear();
            GameFlow.UseSettings(new SettingsService(new MemoryStore()));

            yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
            yield return null;

            _runner = Object.FindAnyObjectByType<GameRunner>();
            _runner.SpawningEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            Application.runInBackground = _previousRunInBackground;
            GameFlow.UseSettings(null);
            RunContext.Clear();
        }

        static int ActiveEffects() =>
            Object.FindObjectsByType<PooledEffect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        /// <summary>Active effects spawned from a particular prefab (pooled instances keep the prefab's name).</summary>
        static int ActiveEffectsNamed(string prefabName)
        {
            var count = 0;
            foreach (var effect in Object.FindObjectsByType<PooledEffect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (effect.name.StartsWith(prefabName, StringComparison.Ordinal)) count++;

            return count;
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string waitingFor)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {waitingFor}.");
                yield return null;
            }
        }

        // ---- Wiring ----

        [Test]
        public void TheLevelHasItsFeedbackPieces()
        {
            Assert.That(Audio, Is.Not.Null, "no AudioDirector in the level");
            Assert.That(EffectSpawner.Instance, Is.Not.Null, "no EffectSpawner in the level");
            Assert.That(Shaker, Is.Not.Null, "no CameraShaker in the level");
        }

        [Test]
        public void EverySoundInTheGameHasAClip()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Loads the bank through the AssetDatabase, so runs in the Editor only.");
#endif
            var bank = LoadBank();
            Assert.That(bank, Is.Not.Null);

            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                if (sfx == Sfx.None) continue;

                Assert.That(bank.TryGet(sfx, out var clip, out _), Is.True, $"{sfx} has no clip");
                Assert.That(clip.length, Is.GreaterThan(0f), $"{sfx} is empty");
            }
        }

        [Test]
        public void TheLevelCanActuallyBeHeard()
        {
            // Without a listener every sound plays into nothing, and Unity reports nothing: the whole audio
            // pass was silent in this scene while every "was it played" assertion still passed.
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(listeners.Length, Is.EqualTo(1), "a scene needs exactly one AudioListener");
            Assert.That(listeners[0].isActiveAndEnabled, Is.True);
            Assert.That(AudioListener.volume, Is.EqualTo(1f).Within(1e-4f),
                "the listener is left alone; the effects volume belongs to the director");
        }

        [UnityTest]
        public IEnumerator TheEffectsVolumeIsAppliedOnce()
        {
            // It used to be applied twice (listener and voice), so 0.5 came out at 0.25, and the music slider
            // changed how loud the effects were.
            GameFlow.Settings.SetSfxVolume(0.5f);
            GameFlow.Settings.SetMusicVolume(0f);
            yield return null;

            Audio.Play(Sfx.SectorClear);
            yield return null;

            var source = PlayingSource();
            Assert.That(source, Is.Not.Null, "nothing is playing");
            Assert.That(AudioListener.volume, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(source.volume, Is.EqualTo(0.7f * 0.5f).Within(1e-3f), "clip trim times the slider, once");
        }

        [UnityTest]
        public IEnumerator ChangingTheVolume_ReachesSoundsAlreadyPlaying()
        {
            Audio.Play(Sfx.BossExplosion);
            yield return null;
            Assert.That(PlayingSource(), Is.Not.Null);

            GameFlow.Settings.SetSfxVolume(0f);
            yield return null;

            foreach (var source in Audio.GetComponents<AudioSource>())
                Assert.That(source.volume, Is.Zero.Within(1e-4f), "a long sound must not run on at its old level");
        }

        [UnityTest]
        public IEnumerator ALongSound_IsNotCutOffByTheShotsOverIt()
        {
            Audio.Play(Sfx.BossExplosion);
            yield return null;
            var bossVoice = PlayingSource();

            // Twelve voices, and a shot every 60 ms: round robin used to steal the boss's voice within a second.
            for (var i = 0; i < 20; i++)
            {
                Audio.Play(Sfx.PlayerShot);
                yield return new WaitForSecondsRealtime(0.07f);
            }

            Assert.That(bossVoice.isPlaying, Is.True, "the boss explosion is still going");
            Assert.That(bossVoice.clip.name, Is.EqualTo("BossExplosion"));
        }

        static AudioSource PlayingSource()
        {
            foreach (var source in Audio.GetComponents<AudioSource>())
                if (source.isPlaying) return source;

            return null;
        }

        // ---- Effects and sounds on real events ----

        [UnityTest]
        public IEnumerator ShootingAnEnemy_ExplodesAndIsHeard()
        {
            var dart = Object.Instantiate(LoadPrefab("Dart"));
            dart.GetComponent<EnemyView>().Init(_runner, _runner.Session.Settings,
                _runner.GetPlayerView(0).Position + new Vector2(5f, 0f), null);

            var before = Audio.PlayCount(Sfx.SmallExplosion);
            _runner.SetCommandOverride(0, new PlayerCommand(System.Numerics.Vector2.Zero, true,
                System.Numerics.Vector2.UnitX));

            yield return WaitUntil(() => _runner.Session.Score.Kills == 1, 4f, "the Dart to be shot down");
            yield return null;

            Assert.That(Audio.PlayCount(Sfx.SmallExplosion), Is.GreaterThan(before), "the explosion is heard");
            Assert.That(Audio.PlayCount(Sfx.PlayerShot), Is.GreaterThan(0), "the shots are heard");

            // Named, not merely "some effect": muzzle flashes from this test's own fire would satisfy that.
            Assert.That(ActiveEffectsNamed("SmallExplosion"), Is.GreaterThan(0),
                "an explosion is drawn where the enemy died");
        }

        [UnityTest]
        public IEnumerator BeingHit_ShakesTheScreenAndSoundsTheAlarm()
        {
            var before = Audio.PlayCount(Sfx.PlayerHit);

            _runner.Session.ReportPlayerHit(0, 10f);
            yield return null;

            Assert.That(Audio.PlayCount(Sfx.PlayerHit), Is.EqualTo(before), "the rules alone make no noise");

            // Going through the runner is what the game does, and that is what produces the feedback.
            _runner.FireEnemyProjectile(_runner.GetPlayerView(0).Position + new Vector2(1.5f, 0f), 8f, 10f);
            yield return WaitUntil(() => Audio.PlayCount(Sfx.PlayerHit) > before, 3f, "the hit to be heard");

            Assert.That(Shaker.Trauma, Is.GreaterThan(0f), "a hit shakes the screen");
        }

        [UnityTest]
        public IEnumerator TheShake_DoesNotMoveThePlayfield()
        {
            // The playfield is derived from the camera, so shaking the camera used to drag the bounds (and any
            // ship clamped to them) around the screen, and flare the engine of a ship that was not moving.
            var before = _runner.Playfield;

            Shaker.Shake(ScreenShake.BossDefeatedTrauma);
            for (var i = 0; i < 20; i++)
            {
                yield return null;
                var now = _runner.Playfield;
                Assert.That(now.MinX, Is.EqualTo(before.MinX).Within(1e-3f), "playfield left edge moved");
                Assert.That(now.MaxY, Is.EqualTo(before.MaxY).Within(1e-3f), "playfield top edge moved");
            }
        }

        [UnityTest]
        public IEnumerator TheShake_ReturnsTheCameraToWhereItStarted()
        {
            var camera = Camera.main;
            var rest = camera.transform.position;

            Shaker.Shake(ScreenShake.BossDefeatedTrauma);
            yield return null;
            Assert.That(camera.transform.position, Is.Not.EqualTo(rest), "the camera does move while shaking");

            yield return new WaitForSecondsRealtime(1.2f);

            Assert.That((camera.transform.position - rest).magnitude, Is.LessThan(1e-4f),
                "and comes back to exactly where it was");
        }

        [UnityTest]
        public IEnumerator TheShakeDiesDownOnItsOwn()
        {
            Shaker.Shake(ScreenShake.BossDefeatedTrauma);
            yield return null;
            Assert.That(Shaker.Trauma, Is.GreaterThan(0f));

            yield return new WaitForSecondsRealtime(1.2f);

            Assert.That(Shaker.Trauma, Is.Zero, "the shake must not last");
        }

        [UnityTest]
        public IEnumerator TurningScreenShakeOff_StopsItImmediately()
        {
            Shaker.Shake(ScreenShake.BossDefeatedTrauma);
            yield return null;

            GameFlow.Settings.SetScreenShake(false);
            yield return null;

            Assert.That(Shaker.Trauma, Is.Zero, "the setting stops a shake already running");

            Shaker.Shake(ScreenShake.BossDefeatedTrauma);
            yield return null;
            Assert.That(Shaker.Trauma, Is.Zero, "and no new shake starts");
        }

        [UnityTest]
        public IEnumerator MutingTheEffects_StopsTheSounds()
        {
            GameFlow.Settings.SetSfxVolume(0f);
            yield return null;

            var before = Audio.PlayCount(Sfx.PlayerShot);
            _runner.SetCommandOverride(0, new PlayerCommand(System.Numerics.Vector2.Zero, true,
                System.Numerics.Vector2.UnitX));
            yield return new WaitForSeconds(0.5f);

            Assert.That(Audio.PlayCount(Sfx.PlayerShot), Is.GreaterThan(before), "the game still asks for shots");
            Assert.That(PlayingSource(), Is.Null, "but a muted game makes no sound");
        }

        [UnityTest]
        public IEnumerator RepeatedShots_AreNotPlayedEveryTime()
        {
            // The weapon fires faster than the sound may repeat, so the sound must lag the shots.
            _runner.Session.GetPlayer(0).Collect(PickupType.WeaponUpgrade);
            _runner.Session.GetPlayer(0).Collect(PickupType.WeaponUpgrade);
            _runner.SetCommandOverride(0, new PlayerCommand(System.Numerics.Vector2.Zero, true,
                System.Numerics.Vector2.UnitX));

            yield return new WaitForSeconds(1f);
            var played = Audio.PlayCount(Sfx.PlayerShot);

            Assert.That(played, Is.GreaterThan(0), "shots are heard");
            Assert.That(played, Is.LessThanOrEqualTo(Mathf.CeilToInt(1f / AudioRules.MinimumInterval(Sfx.PlayerShot)) + 2),
                "and they are rate limited rather than stacking into noise");
        }

        [UnityTest]
        public IEnumerator HittingTheHull_LeavesTheCoreLookingClosed()
        {
            _runner.StartBossFightNow();
            var boss = _runner.Boss;
            var core = boss.GetComponentInChildren<BossCoreView>();
            var coreRenderer = core.GetComponent<SpriteRenderer>();
            yield return WaitUntil(() => !boss.Brain.IsCoreOpen, 8f, "the core to be closed");
            yield return null;

            var closedColour = coreRenderer.color;
            boss.TakeHit(10f, 0); // armour: flashes the hull

            yield return new WaitForSeconds(VisualCues.HitFlashSeconds * 3f);

            Assert.That(coreRenderer.color, Is.EqualTo(closedColour),
                "the flash must not leave the closed core looking open");
        }

        [UnityTest]
        public IEnumerator ShootingAClosedCore_DoesNotLookLikeDamage()
        {
            _runner.StartBossFightNow();
            var boss = _runner.Boss;
            yield return WaitUntil(() => !boss.Brain.IsCoreOpen, 8f, "the core to be closed");

            var explosions = Audio.PlayCount(Sfx.SmallExplosion);
            var health = boss.Brain.Health.Current;

            boss.TakeCoreHit(10f, 0);
            yield return null;

            Assert.That(boss.Brain.Health.Current, Is.EqualTo(health), "a closed core takes no damage");
            Assert.That(Audio.PlayCount(Sfx.SmallExplosion), Is.EqualTo(explosions),
                "and must not sound as though it did");
        }

        [UnityTest]
        public IEnumerator AMeteorThatSurvivesAHit_FlashesWhite()
        {
            var meteor = Object.Instantiate(LoadPrefab("Meteor")).GetComponent<MeteorView>();
            var definition = LoadMeteor("MeteorLarge");
            meteor.Init(_runner, definition, _runner.GetPlayerView(0).Position + new Vector2(6f, 0f),
                Vector2.zero, 0f, null);
            var renderer = meteor.GetComponentInChildren<SpriteRenderer>();
            var resting = renderer.color;

            meteor.TakeHit(1f, 0); // large meteors take several hits
            yield return null;

            Assert.That(meteor.IsAlive, Is.True, "the meteor survived, so the hit must read");
            Assert.That(renderer.color, Is.Not.EqualTo(resting), "it flashes");

            yield return new WaitForSeconds(VisualCues.HitFlashSeconds * 3f);
            Assert.That(renderer.color, Is.EqualTo(resting), "and goes back to normal");
        }

        static MeteorDefinition LoadMeteor(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<MeteorDefinition>(
                $"Assets/_Game/ScriptableObjects/Meteors/{name}.asset");
#else
            throw new InvalidOperationException("Editor only");
#endif
        }

        [UnityTest]
        public IEnumerator CollectingAPickup_SparklesAndChimes()
        {
            var before = Audio.PlayCount(Sfx.Pickup);
            var pickup = Object.Instantiate(LoadPrefab("Pickup")).GetComponent<PickupView>();
            pickup.Init(_runner, PickupType.Health, 2f, _runner.GetPlayerView(0).Position + new Vector2(1.5f, 0f), null);

            yield return WaitUntil(() => Audio.PlayCount(Sfx.Pickup) > before, 3f, "the pickup chime");

            Assert.That(ActiveEffects(), Is.GreaterThan(0), "a sparkle is drawn");
        }

        [UnityTest]
        public IEnumerator EffectsAreReused_NotAllocatedForever()
        {
            for (var i = 0; i < 12; i++)
            {
                Cue.Spawn(Effect.SmallExplosion, new Vector2(i, 0f));
                yield return null;
            }

            var peak = Object.FindObjectsByType<PooledEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            yield return new WaitForSeconds(1.5f);

            for (var i = 0; i < 12; i++)
            {
                Cue.Spawn(Effect.SmallExplosion, new Vector2(i, 0f));
                yield return null;
            }

            var after = Object.FindObjectsByType<PooledEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Assert.That(after, Is.EqualTo(peak), "the second burst reused the first one's instances");

            // Six effects are primed with a few instances each at load; a burst of twelve must not multiply that.
            var primed = Enum.GetValues(typeof(Effect)).Length * 4;
            Assert.That(peak, Is.LessThanOrEqualTo(primed + 12), "the pool does not grow without bound");
        }

        static SoundBank LoadBank()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<SoundBank>("Assets/_Game/Audio/SoundBank.asset");
#else
            throw new InvalidOperationException("Editor only");
#endif
        }

        static GameObject LoadPrefab(string name)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/{name}.prefab");
            Assert.That(prefab, Is.Not.Null, $"missing prefab {name}");
            return prefab;
#else
            throw new InvalidOperationException("Editor only");
#endif
        }
    }
}
