using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using YASS.UI;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Every scene the player can be in has to carry its own sound (GDD "Audio Direction"). A scene that has
    /// lost its feedback objects looks exactly like one that is fine until somebody notices the silence, which
    /// is how the title screen's music once went missing.
    /// </summary>
    public class SceneAudioTests
    {
        [TestCase("Title")]
        [TestCase("Level")]
        public void EveryPlayableScene_CanBeHeard(string name)
        {
            ScenePeek.In(name, scene =>
            {
                ScenePeek.Require<AudioDirector>(scene, name, "it is silent");
                ScenePeek.Require<MusicPlayer>(scene, name, "there is no music");
                ScenePeek.Require<AudioListener>(scene, name, "nothing would be heard");

                var volumes = ScenePeek.Require<MixerVolumes>(scene, name, "the settings sliders would do nothing");

                // The reference is what carries the sliders to the groups: without it the component logs and
                // gives up, and every other assertion here still passes.
                var mixer = new SerializedObject(volumes).FindProperty("mixer");
                Assert.That(mixer, Is.Not.Null);
                Assert.That(mixer.objectReferenceValue, Is.Not.Null,
                    name + "'s mixer volumes have no mixer assigned, so the volume settings would do nothing");
            });
        }

        /// <summary>
        /// The settings are applied by a component in the scene, so a scene that lost it would open with the
        /// window setting ignored, looking exactly like a scene that is fine (see the audio above).
        /// </summary>
        [TestCase("Title")]
        [TestCase("Level")]
        public void EveryPlayableScene_AppliesTheSavedSettings(string name)
        {
            ScenePeek.In(name, scene => ScenePeek.Require<SettingsApplier>(scene, name, "saved settings would be ignored"));
        }

        /// <summary>The title plays the menu theme; a level or an Endless run plays the level theme.</summary>
        [TestCase("Title", Track.Menu)]
        [TestCase("Level", Track.Level)]
        public void EveryPlayableScene_StartsItsOwnTrack(string name, Track expected)
        {
            ScenePeek.In(name, scene =>
            {
                var music = ScenePeek.Find<MusicPlayer>(scene);
                Assert.That(music, Is.Not.Null, name + " has no music player");

                var playOnStart = new SerializedObject(music).FindProperty("playOnStart");
                Assert.That(playOnStart, Is.Not.Null);
                Assert.That((Track)playOnStart.enumValueIndex, Is.EqualTo(expected),
                    name + " starts the wrong music");
            });
        }
    }
}
