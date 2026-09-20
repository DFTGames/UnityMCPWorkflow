using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using YASS.Core;

namespace YASS.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Audio/YASS.mixer</c> with the Music and SFX groups the Settings screen controls
    /// (GDD "Audio Direction", Mixing).
    /// </summary>
    /// <remarks>
    /// Unity has no public API for creating a mixer, its groups or its exposed parameters: all of it lives in
    /// the internal <c>UnityEditor.Audio.AudioMixerController</c>, which this drives by reflection. That is a
    /// deliberate trade for keeping the asset reproducible from code like everything else in the project; if a
    /// future Unity version changes those names this fails loudly, and the mixer can be rebuilt by hand.
    /// </remarks>
    public static class AudioMixerBuilder
    {
        public const string MixerPath = "Assets/_Game/Audio/YASS.mixer";

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static;

        [MenuItem("Tools/YASS/Build Audio Mixer")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
            {
                Debug.Log($"{MixerPath} already exists; delete it to rebuild.");
                return;
            }

            var editor = typeof(EditorWindow).Assembly;
            var controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController");
            var groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath");

            if (controllerType == null || groupType == null || pathType == null)
                throw new InvalidOperationException(
                    "Unity's internal audio mixer API has moved; build the mixer by hand and wire it in the scenes.");

            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath) ?? ".");

            var controller = Invoke(controllerType, null, "CreateMixerControllerAtPath", MixerPath);
            var master = Get(controller, "masterGroup");

            CreateGroup(controllerType, pathType, controller, master, AudioRules.MusicGroup, AudioRules.MusicVolumeParameter);
            CreateGroup(controllerType, pathType, controller, master, AudioRules.SfxGroup, AudioRules.SfxVolumeParameter);

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);

            Debug.Log($"Built {MixerPath} with the {AudioRules.MusicGroup} and {AudioRules.SfxGroup} groups");
        }

        /// <summary>Adds a group under the master and exposes its volume under the name the game sets.</summary>
        static void CreateGroup(Type controllerType, Type pathType, object controller, object master,
            string name, string exposedParameter)
        {
            var group = Invoke(controllerType, controller, "CreateNewGroup", name, true);
            Invoke(controllerType, controller, "AddChildToParent", group, master);
            // No AddGroupToCurrentView: that only arranges the Audio Mixer window's view and needs one open.

            // A group's volume is identified by its own GUID; the path pairs the two, and the exposed name is
            // what AudioMixer.SetFloat takes at run time.
            var volumeId = Invoke(group.GetType(), group, "GetGUIDForVolume");
            var path = Activator.CreateInstance(pathType, group, volumeId);
            Invoke(controllerType, controller, "AddExposedParameter", path);

            RenameLastExposedParameter(controller, exposedParameter);
        }

        /// <summary>
        /// A newly exposed parameter is named after its path; this renames the one just added to the name the
        /// game asks the mixer for at run time.
        /// </summary>
        static void RenameLastExposedParameter(object controller, string name)
        {
            var property = controller.GetType().GetProperty("exposedParameters", Flags);
            if (property == null) throw new InvalidOperationException("The mixer has no exposedParameters.");

            if (property.GetValue(controller) is not Array parameters || parameters.Length == 0)
                throw new InvalidOperationException("The mixer exposed no parameter to rename.");

            var last = parameters.GetValue(parameters.Length - 1);
            var field = last.GetType().GetField("name", Flags);
            if (field == null) throw new InvalidOperationException("ExposedAudioParameter has no name field.");

            field.SetValue(last, name);                          // boxed copy
            parameters.SetValue(last, parameters.Length - 1);    // back into the array
            property.SetValue(controller, parameters);           // and back into the mixer
        }

        static object Invoke(Type type, object target, string method, params object[] arguments)
        {
            var info = type.GetMethod(method, Flags);
            if (info == null) throw new InvalidOperationException($"{type.Name} has no {method}.");

            return info.Invoke(target, arguments);
        }

        static object Get(object target, string property)
        {
            var info = target.GetType().GetProperty(property, Flags);
            if (info == null) throw new InvalidOperationException($"{target.GetType().Name} has no {property}.");

            return info.GetValue(target);
        }
    }
}
