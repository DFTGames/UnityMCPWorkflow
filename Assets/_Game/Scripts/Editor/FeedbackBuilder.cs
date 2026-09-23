using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;

namespace YASS.Editor
{
    /// <summary>
    /// Builds the game-feel assets: the sound bank (matching generated clips to <see cref="Sfx"/> entries by
    /// name) and the particle effect prefabs (GDD "Art Direction", Visual effects; "Audio Direction").
    /// Re-runnable: tune a value here and rebuild rather than hand-editing prefabs.
    /// </summary>
    public static class FeedbackBuilder
    {
        const string SfxFolder = "Assets/_Game/Audio/SFX";
        public const string BankPath = "Assets/_Game/Audio/SoundBank.asset";

        const string EffectFolder = "Assets/_Game/Prefabs/Effects";
        const string MaterialPath = "Assets/_Game/Materials/Effects.mat";
        const string ParticleTexturePath = "Assets/_Game/Sprites/Procedural/Star.png";

        /// <summary>
        /// Effects draw above the player ship and its shield, which are 5 and 6 (GDD "Art Direction",
        /// Rendering conventions): an explosion on the ship, or a ripple on its bubble, must not be hidden by it.
        /// </summary>
        const int EffectSortingOrder = 7;

        /// <summary>Where an effect's prefab lives.</summary>
        static string PrefabPathFor(Effect effect) => $"{EffectFolder}/{effect}.prefab";

        /// <summary>Per-sound trim, so a long explosion does not drown a shot (GDD "Audio Direction", Mixing).</summary>
        static float VolumeFor(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.PlayerShot: return 0.35f;   // fires many times a second
                case Sfx.EnemyShot: return 0.3f;
                case Sfx.SmallExplosion: return 0.6f;
                case Sfx.MeteorBreak: return 0.55f;
                case Sfx.UiMove: return 0.4f;
                case Sfx.UiConfirm: return 0.5f;
                case Sfx.BossExplosion: return 0.9f;
                default: return 0.7f;
            }
        }

        [MenuItem("Tools/YASS/Build Feedback Assets")]
        public static void Build()
        {
            BuildSoundBank();
            ApplyClipImportSettings();
            BuildEffectPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log("Built the sound bank and the effect prefabs");
        }

        static void BuildSoundBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                Directory.CreateDirectory(Path.GetDirectoryName(BankPath) ?? ".");
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            var entries = new List<SoundBank.Entry>();
            var missing = new List<string>();

            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                if (sfx == Sfx.None) continue;

                var clip = LoadClip(sfx.ToString());
                if (clip == null)
                {
                    missing.Add(sfx.ToString());
                    continue;
                }

                entries.Add(new SoundBank.Entry { Sfx = sfx, Clip = clip, Volume = VolumeFor(sfx) });
            }

            var serialized = new SerializedObject(bank);
            var array = Find(serialized, "entries");
            array.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Sfx").enumValueIndex = (int)entries[i].Sfx;
                element.FindPropertyRelative("Clip").objectReferenceValue = entries[i].Clip;
                element.FindPropertyRelative("Volume").floatValue = entries[i].Volume;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);

            if (missing.Count > 0)
                Debug.LogWarning($"Sound bank: no clip for {string.Join(", ", missing)} (expected in {SfxFolder}).");
        }

        /// <summary>
        /// Generated clips import with their audio data left unloaded, which costs a decompress on the main
        /// thread the first time each sound plays: a stutter on the first shot, the first explosion, the siren.
        /// They are also imported as stereo 3D clips, which is wasted on voices the director plays in 2D.
        /// </summary>
        static void ApplyClipImportSettings()
        {
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                if (sfx == Sfx.None) continue;

                var clip = LoadClip(sfx.ToString());
                if (clip == null) continue;

                var path = AssetDatabase.GetAssetPath(clip);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;

                var settings = importer.defaultSampleSettings;
                var changed = !settings.preloadAudioData || !importer.forceToMono ||
                              settings.loadType != AudioClipLoadType.DecompressOnLoad;

                settings.loadType = AudioClipLoadType.DecompressOnLoad; // short clips: decode once, keep in memory
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.preloadAudioData = true; // no decompress stutter on the first shot or explosion

                importer.forceToMono = true;      // the director plays everything in 2D anyway
                importer.defaultSampleSettings = settings;

                if (changed) importer.SaveAndReimport();
            }
        }

        static AudioClip LoadClip(string name)
        {
            foreach (var extension in new[] { ".wav", ".mp3", ".ogg" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{name}{extension}");
                if (clip != null) return clip;
            }

            return null;
        }

        static void BuildEffectPrefabs()
        {
            Directory.CreateDirectory(EffectFolder);

            // Colours follow the palette: explosions in the player's orange-yellow, sparkles gold, the shield
            // ripple in the accent magenta (GDD "Art Direction").
            CreateEffect(Effect.SmallExplosion, new Color(1f, 0.72f, 0.23f), count: 16, speed: 4.5f,
                size: 0.3f, lifetime: 0.45f, radius: 0.05f);
            CreateEffect(Effect.LargeExplosion, new Color(1f, 0.55f, 0.15f), count: 30, speed: 6.5f,
                size: 0.45f, lifetime: 0.7f, radius: 0.1f);
            CreateEffect(Effect.BossExplosion, new Color(1f, 0.45f, 0.1f), count: 40, speed: 8.5f,
                size: 0.7f, lifetime: 1.8f, radius: 0.3f, bursts: 4);
            CreateEffect(Effect.MuzzleFlash, new Color(1f, 0.95f, 0.6f), count: 5, speed: 2.5f,
                size: 0.18f, lifetime: 0.12f, radius: 0.03f);
            CreateEffect(Effect.PickupSparkle, new Color(1f, 0.85f, 0.35f), count: 12, speed: 2.2f,
                size: 0.16f, lifetime: 0.5f, radius: 0.08f);
            // The shield ripple is the one effect that really is a ring: it traces the bubble's edge.
            CreateEffect(Effect.ShieldRipple, new Color(1f, 0.31f, 0.76f), count: 18, speed: 3.2f,
                size: 0.18f, lifetime: 0.35f, radius: 0.55f, ring: true);
        }

        static void CreateEffect(Effect effect, Color colour, int count, float speed, float size, float lifetime,
            float radius, int bursts = 1, bool ring = false)
        {
            var go = new GameObject(effect.ToString());

            try
            {
                var particles = go.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.duration = Mathf.Max(0.1f, lifetime);
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.15f, speed);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
                main.startColor = new ParticleSystem.MinMaxGradient(colour, Color.Lerp(colour, Color.white, 0.6f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.stopAction = ParticleSystemStopAction.Callback; // how PooledEffect returns itself
                main.maxParticles = count * bursts + 8;

                var emission = particles.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(BurstsFor(count, bursts, lifetime));

                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = Mathf.Max(0.01f, radius);
                shape.radiusThickness = ring ? 0f : 1f; // a ring emits from its edge, a burst from everywhere

                var colourOverLifetime = particles.colorOverLifetime;
                colourOverLifetime.enabled = true;
                colourOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOut());

                var sizeOverLifetime = particles.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, ShrinkCurve(ring));

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = EffectMaterial();
                renderer.sortingOrder = EffectSortingOrder;

                go.AddComponent<PooledEffect>();

                PrefabUtility.SaveAsPrefabAsset(go, PrefabPathFor(effect), out var saved);
                if (!saved) Debug.LogError($"Failed to save the {effect} prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static ParticleSystem.Burst[] BurstsFor(int count, int bursts, float lifetime)
        {
            var result = new ParticleSystem.Burst[bursts];
            for (var i = 0; i < bursts; i++)
            {
                // A boss dies in stages: several bursts spread over the effect's life (GDD "Art Direction").
                var time = bursts == 1 ? 0f : i * (lifetime * 0.45f / bursts);
                result[i] = new ParticleSystem.Burst(time, (short)count);
            }

            return result;
        }

        static Gradient FadeOut()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        static AnimationCurve ShrinkCurve(bool grow) =>
            grow
                ? AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.3f)  // a ripple expands as it fades
                : AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f);  // a spark burns out

        /// <summary>
        /// The effects' own URP material. Unity's built-in particle material still renders, but it is a legacy
        /// shader: this project is URP 2D, and the engine exhaust already uses the URP particle shader
        /// (see CLAUDE.md, "New materials and shaders must be URP/2D compatible").
        /// </summary>
        static Material EffectMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    Debug.LogError("URP particle shader not found; effects would render with no material.");
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath) ?? ".");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            // Transparent and additive, like the engine exhaust: fire and sparks add light rather than occlude.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath);
            if (texture != null) material.SetTexture("_BaseMap", texture);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Finds a serialised field, complaining by name rather than throwing a null reference later.</summary>
        static SerializedProperty Find(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException(
                    $"{serialized.targetObject.GetType().Name} has no serialised field '{field}'.");

            return property;
        }
    }
}
