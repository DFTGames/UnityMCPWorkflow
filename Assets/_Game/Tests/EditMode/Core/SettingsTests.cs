using System.Collections.Generic;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    /// <summary>An in-memory settings store, standing in for PlayerPrefs.</summary>
    sealed class FakeSettingsStore : ISettingsStore
    {
        readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
        readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>();

        public int Saves { get; private set; }

        public float GetFloat(string key, float fallback) => _floats.TryGetValue(key, out var v) ? v : fallback;
        public void SetFloat(string key, float value) => _floats[key] = value;
        public bool GetBool(string key, bool fallback) => _bools.TryGetValue(key, out var v) ? v : fallback;
        public void SetBool(string key, bool value) => _bools[key] = value;
        public void Save() => Saves++;
    }

    public class GameSettingsTests
    {
        [Test]
        public void Default_MatchesTheGdd()
        {
            var settings = GameSettings.Default;

            Assert.That(settings.MusicVolume, Is.EqualTo(0.7f));
            Assert.That(settings.SfxVolume, Is.EqualTo(0.9f));
            Assert.That(settings.ScreenShake, Is.True);
            Assert.That(settings.Fullscreen, Is.True);
        }

        [TestCase(-0.5f, 0f)]
        [TestCase(1.5f, 1f)]
        [TestCase(0.25f, 0.25f)]
        public void Volumes_AreClampedToZeroOne(float value, float expected)
        {
            Assert.That(GameSettings.Default.WithMusicVolume(value).MusicVolume, Is.EqualTo(expected));
            Assert.That(GameSettings.Default.WithSfxVolume(value).SfxVolume, Is.EqualTo(expected));
        }

        [Test]
        public void CorruptVolume_FallsBackToTheDefault()
        {
            // A NaN in the save file must not silently mute the game.
            Assert.That(GameSettings.Default.WithMusicVolume(float.NaN).MusicVolume,
                Is.EqualTo(GameSettings.DefaultMusicVolume));
            Assert.That(GameSettings.Default.WithSfxVolume(float.NaN).SfxVolume,
                Is.EqualTo(GameSettings.DefaultSfxVolume));
        }

        [Test]
        public void With_ChangesOnlyOneValue()
        {
            var settings = GameSettings.Default.WithScreenShake(false);

            Assert.That(settings.ScreenShake, Is.False);
            Assert.That(settings.MusicVolume, Is.EqualTo(GameSettings.DefaultMusicVolume));
            Assert.That(settings.SfxVolume, Is.EqualTo(GameSettings.DefaultSfxVolume));
            Assert.That(settings.Fullscreen, Is.EqualTo(GameSettings.DefaultFullscreen));
        }

        [Test]
        public void Equality_ComparesEveryValue()
        {
            Assert.That(GameSettings.Default, Is.EqualTo(GameSettings.Default));
            Assert.That(GameSettings.Default.WithFullscreen(false), Is.Not.EqualTo(GameSettings.Default));
            Assert.That(GameSettings.Default.WithMusicVolume(0.1f) == GameSettings.Default, Is.False);
            Assert.That(GameSettings.Default.WithMusicVolume(0.1f) != GameSettings.Default, Is.True);
        }
    }

    public class SettingsServiceTests
    {
        static SettingsService Create(out FakeSettingsStore store)
        {
            store = new FakeSettingsStore();
            return new SettingsService(store);
        }

        [Test]
        public void EmptyStore_LoadsTheDefaults()
        {
            Assert.That(Create(out _).Settings, Is.EqualTo(GameSettings.Default));
        }

        [Test]
        public void Changes_ArePersistedImmediately()
        {
            var service = Create(out var store);

            service.SetMusicVolume(0.25f);
            service.SetScreenShake(false);

            Assert.That(store.GetFloat(SettingsService.MusicVolumeKey, -1f), Is.EqualTo(0.25f));
            Assert.That(store.GetBool(SettingsService.ScreenShakeKey, true), Is.False);
        }

        [Test]
        public void SavedSettings_ComeBackOnTheNextSession()
        {
            var store = new FakeSettingsStore();
            var first = new SettingsService(store);
            first.SetSfxVolume(0.1f);
            first.SetFullscreen(false);

            var second = new SettingsService(store);

            Assert.That(second.Settings.SfxVolume, Is.EqualTo(0.1f));
            Assert.That(second.Settings.Fullscreen, Is.False);
            Assert.That(second.Settings.MusicVolume, Is.EqualTo(GameSettings.DefaultMusicVolume), "untouched values keep their default");
        }

        [Test]
        public void Changed_FiresOnlyWhenSomethingActuallyChanges()
        {
            var service = Create(out var store);
            var events = 0;
            service.Changed += _ => events++;

            service.SetMusicVolume(0.5f);
            service.SetMusicVolume(0.5f);

            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void Changed_CarriesTheNewSettings()
        {
            var service = Create(out _);
            GameSettings? seen = null;
            service.Changed += s => seen = s;

            service.SetSfxVolume(0.2f);

            Assert.That(seen, Is.Not.Null);
            Assert.That(seen.Value.SfxVolume, Is.EqualTo(0.2f));
        }

        [Test]
        public void Apply_ReannouncesWithoutChangingAnything()
        {
            var service = Create(out var store);
            service.SetMusicVolume(0.3f);
            var events = 0;
            service.Changed += _ => events++;

            service.Apply();

            Assert.That(events, Is.EqualTo(1));
            Assert.That(store.Saves, Is.Zero, "Apply does not write");
            Assert.That(service.Settings.MusicVolume, Is.EqualTo(0.3f));
        }

        [Test]
        public void OutOfRangeSavedValue_IsClampedOnLoad()
        {
            var store = new FakeSettingsStore();
            store.SetFloat(SettingsService.MusicVolumeKey, 4f);
            store.SetFloat(SettingsService.SfxVolumeKey, float.NaN);

            var service = new SettingsService(store);

            Assert.That(service.Settings.MusicVolume, Is.EqualTo(1f));
            Assert.That(service.Settings.SfxVolume, Is.EqualTo(GameSettings.DefaultSfxVolume));
        }

        [Test]
        public void ResetToDefaults_RestoresAndSaves()
        {
            var service = Create(out var store);
            service.SetMusicVolume(0f);
            service.SetScreenShake(false);

            service.ResetToDefaults();

            Assert.That(service.Settings, Is.EqualTo(GameSettings.Default));
            Assert.That(store.GetFloat(SettingsService.MusicVolumeKey, -1f), Is.EqualTo(GameSettings.DefaultMusicVolume));
        }

        [Test]
        public void Flush_WritesOnceHoweverManyChanges()
        {
            // Dragging a slider changes the value every frame; flushing each time would hitch the game.
            var service = Create(out var store);

            for (var i = 1; i <= 20; i++) service.SetMusicVolume(i / 20f);
            Assert.That(store.Saves, Is.Zero, "changes are held until the settings are flushed");

            service.Flush();
            Assert.That(store.Saves, Is.EqualTo(1));
            Assert.That(store.GetFloat(SettingsService.MusicVolumeKey, -1f), Is.EqualTo(1f),
                "the last value reaches the store even before the flush");
        }

        [Test]
        public void Flush_WithNothingPending_DoesNotWrite()
        {
            var service = Create(out var store);

            service.Flush();
            service.SetSfxVolume(0.5f);
            service.Flush();
            service.Flush();

            Assert.That(store.Saves, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsAMissingStore()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SettingsService(null));
        }

        [Test]
        public void TheSavedFullscreen_WinsOnceASession()
        {
            // The first scene of a session applies what was saved; a later scene adopts the window instead,
            // because by then the player may have changed it themselves with Alt+Enter or the window chrome.
            Assert.That(SettingsService.ShouldAdoptTheWindow(false, false), Is.False);
            Assert.That(SettingsService.ShouldAdoptTheWindow(true, false), Is.True);
        }

        [Test]
        public void TheEditorsWindow_IsNeverAdopted()
        {
            // In the Editor the window is the Editor's, not the game's, so adopting it would write the Game
            // view's state over the player's saved choice. This is a real setting, kept in real PlayerPrefs.
            Assert.That(SettingsService.ShouldAdoptTheWindow(true, true), Is.False);
            Assert.That(SettingsService.ShouldAdoptTheWindow(false, true), Is.False);
        }
    }
}
