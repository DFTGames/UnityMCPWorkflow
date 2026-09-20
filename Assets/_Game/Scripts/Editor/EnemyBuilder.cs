using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using YASS.Gameplay;

namespace YASS.Editor
{
    /// <summary>
    /// Builds the enemy roster: one <see cref="EnemyDefinition"/> asset and one prefab per enemy, plus the Mine
    /// Layer's mine (GDD "Enemies and Hazards"). Re-runnable, like the other builders: change a number here and
    /// rebuild rather than hand-editing assets, and re-run it once new art lands to pick the sprites up.
    /// Ships are built from the Dart prefab, so they inherit its rigidbody, hit flash and engine exhaust.
    /// </summary>
    public static class EnemyBuilder
    {
        const string TemplatePrefab = "Assets/_Game/Prefabs/Dart.prefab";
        const string PrefabFolder = "Assets/_Game/Prefabs";
        const string DefinitionFolder = "Assets/_Game/ScriptableObjects/Enemies";
        const string ShipSpriteFolder = "Assets/_Game/Sprites/Ships";
        const string HazardSpriteFolder = "Assets/_Game/Sprites/Hazards";
        const string WhiteSpritePath = "Assets/_Game/Sprites/Procedural/UIWhite.png";

        /// <summary>Enemies and meteors draw at 1, enemy shots at 3 (GDD "Art Direction", Rendering conventions).</summary>
        const int EnemySortingOrder = 1;
        const int BeamSortingOrder = 3;

        /// <summary>
        /// How much of the sprite's box the collider fills. Shoot-'em-ups are forgiving, and more so vertically:
        /// a ship's wings and fins should not kill you, which is how the Dart's own hitbox was tuned.
        /// </summary>
        const float ColliderWidthFraction = 0.8f;
        const float ColliderHeightFraction = 0.55f;

        /// <summary>One enemy as the GDD describes it: everything the definition and the prefab need.</summary>
        sealed class Spec
        {
            public string Name;
            public string Sprite;
            public EnemySize Size;
            public float Health;
            public float Speed;
            public float ContactDamage;
            public MotionPattern Pattern = MotionPattern.Straight;
            public float WaveAmplitude = 1.5f;
            public float WaveFrequency = 0.4f;
            public float StationFromRight = 1f / 3f;
            public float DiveEntrySeconds = 0.8f;
            public float DiveLockSeconds = 0.6f;
            public float DiveChargeSpeed = 12f;
            public bool Fires;
            public int ShotsPerBurst = 1;
            public float BurstShotInterval = 0.15f;
            public float FireInterval = 2f;
            public float FirstShotDelay = 0.8f;
            public float BulletSpeed = 6f;
            public float BulletDamage = 10f;
            public bool HasFrontShield;
            public bool LaysMines;
            public float MineInterval = 1.6f;
            public bool FiresBeam;
            public float BeamWarningSeconds = 1.2f;
            public float BeamSeconds = 0.25f;
            public float BeamRecoverySeconds = 1.5f;
            public float BeamDamage = 20f;
            public float BeamHalfWidth = 0.15f;
            public float BeamLength = 40f;

            /// <summary>Sprite width in world units (GDD "Asset List", Target world sizes).</summary>
            public float WorldWidth = 1f;

            /// <summary>Where its shots (or its beam) leave the hull: just off the nose of a ship this wide.</summary>
            public Vector2 Muzzle => new Vector2(-(WorldWidth * 0.5f + 0.05f), 0f);
        }

        /// <summary>The six enemies added after Level 1's Dart and Weaver, with the GDD's provisional values.</summary>
        static Spec[] Specs() => new[]
        {
            new Spec
            {
                Name = "SwarmDrone", Sprite = "SwarmDrone", Size = EnemySize.Small,
                Health = 1f, Speed = 6f, ContactDamage = 15f, WorldWidth = 0.6f
            },
            new Spec
            {
                Name = "Gunship", Sprite = "Gunship", Size = EnemySize.Large,
                Health = 12f, Speed = 2.5f, ContactDamage = 30f, WorldWidth = 1.7f,
                Pattern = MotionPattern.HoldPosition, StationFromRight = 1f / 3f,
                Fires = true, ShotsPerBurst = 3, BurstShotInterval = 0.15f, FireInterval = 2.2f,
                FirstShotDelay = 0.8f, BulletSpeed = 7f, BulletDamage = 10f
            },
            new Spec
            {
                Name = "Diver", Sprite = "Diver", Size = EnemySize.Medium,
                Health = 4f, Speed = 3f, ContactDamage = 30f, WorldWidth = 0.95f,
                Pattern = MotionPattern.Dive
            },
            new Spec
            {
                Name = "MineLayer", Sprite = "MineLayer", Size = EnemySize.Medium,
                Health = 5f, Speed = 2.5f, ContactDamage = 20f, WorldWidth = 1.1f,
                LaysMines = true, MineInterval = 1.6f
            },
            new Spec
            {
                Name = "Frigate", Sprite = "Frigate", Size = EnemySize.Large,
                Health = 16f, Speed = 2f, ContactDamage = 30f, WorldWidth = 2f,
                HasFrontShield = true
            },
            new Spec
            {
                Name = "Sniper", Sprite = "Sniper", Size = EnemySize.Medium,
                Health = 3f, Speed = 3f, ContactDamage = 20f, WorldWidth = 1f,
                Pattern = MotionPattern.HoldPosition, StationFromRight = 0.25f,
                FiresBeam = true, BeamDamage = 20f, BeamHalfWidth = 0.15f, BeamLength = 40f
            }
        };

        [MenuItem("Tools/YASS/Build Enemies")]
        public static void Build()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab);
            if (template == null)
            {
                Debug.LogError($"{nameof(EnemyBuilder)}: the template prefab '{TemplatePrefab}' is missing.");
                return;
            }

            Directory.CreateDirectory(DefinitionFolder);
            var specs = Specs();
            foreach (var spec in specs)
            {
                var definition = BuildDefinition(spec);
                BuildShipPrefab(spec, definition);
            }

            BuildMinePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{nameof(EnemyBuilder)}: built {specs.Length} enemies and the proximity mine.");
        }

        static EnemyDefinition BuildDefinition(Spec spec)
        {
            var path = $"{DefinitionFolder}/{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);
            void Set(string field, Action<SerializedProperty> apply)
            {
                var property = serialized.FindProperty(field);
                if (property == null) throw new InvalidOperationException($"EnemyDefinition has no field '{field}'.");
                apply(property);
            }

            void Number(string field, float value) => Set(field, p => p.floatValue = value);
            void Flag(string field, bool value) => Set(field, p => p.boolValue = value);
            void Integer(string field, int value) => Set(field, p => p.intValue = value);

            Integer("size", (int)spec.Size);
            Number("maxHealth", spec.Health);
            Number("speed", spec.Speed);
            Number("contactDamage", spec.ContactDamage);
            Integer("pattern", (int)spec.Pattern);
            Number("waveAmplitude", spec.WaveAmplitude);
            Number("waveFrequency", spec.WaveFrequency);
            Number("stationFromRight", spec.StationFromRight);
            Number("diveEntrySeconds", spec.DiveEntrySeconds);
            Number("diveLockSeconds", spec.DiveLockSeconds);
            Number("diveChargeSpeed", spec.DiveChargeSpeed);
            Flag("fires", spec.Fires);
            Integer("shotsPerBurst", spec.ShotsPerBurst);
            Number("burstShotInterval", spec.BurstShotInterval);
            Number("fireInterval", spec.FireInterval);
            Number("firstShotDelay", spec.FirstShotDelay);
            Number("bulletSpeed", spec.BulletSpeed);
            Number("bulletDamage", spec.BulletDamage);
            Flag("hasFrontShield", spec.HasFrontShield);
            Flag("laysMines", spec.LaysMines);
            Number("mineInterval", spec.MineInterval);
            Flag("firesBeam", spec.FiresBeam);
            Number("beamWarningSeconds", spec.BeamWarningSeconds);
            Number("beamSeconds", spec.BeamSeconds);
            Number("beamRecoverySeconds", spec.BeamRecoverySeconds);
            Number("beamDamage", spec.BeamDamage);
            Number("beamHalfWidth", spec.BeamHalfWidth);
            Number("beamLength", spec.BeamLength);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(definition);
            return definition;
        }

        static void BuildShipPrefab(Spec spec, EnemyDefinition definition)
        {
            var path = $"{PrefabFolder}/{spec.Name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(TemplatePrefab);
            try
            {
                root.name = spec.Name;

                var renderer = root.GetComponent<SpriteRenderer>();
                var sprite = FindSprite($"{ShipSpriteFolder}/{spec.Sprite}.png");
                if (sprite != null) renderer.sprite = sprite;
                renderer.sortingOrder = EnemySortingOrder;

                // Sprites are imported at their own pixels per unit; scale is the builder's way of holding a
                // roster to the sizes the GDD lists while the art is still provisional.
                var width = renderer.sprite != null ? renderer.sprite.bounds.size.x : 1f;
                var scale = width > 0.001f ? spec.WorldWidth / width : 1f;
                root.transform.localScale = new Vector3(scale, scale, 1f);

                FitCollider(root.GetComponent<Collider2D>(), renderer.sprite);

                // Explicit, so the sniper's beam is never mistaken for part of the ship and flashed with it.
                SetReference(root.GetComponent<HitFlash>(), "renderers", renderer);

                var view = root.GetComponent<EnemyView>();
                SetReference(view, "definition", definition);
                SetVector(view, "muzzleOffset", spec.Muzzle);
                SetReference(view, "beam", spec.FiresBeam ? BuildBeam(root) : null);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The sniper's warning line and beam: one stretched white sprite it drives itself.</summary>
        static BeamView BuildBeam(GameObject root)
        {
            var existing = root.transform.Find("Beam");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var beam = new GameObject("Beam");
            beam.transform.SetParent(root.transform, false);
            beam.layer = root.layer;

            var renderer = beam.AddComponent<SpriteRenderer>();
            renderer.sprite = FindSprite(WhiteSpritePath);
            renderer.sortingOrder = BeamSortingOrder;
            renderer.enabled = false;

            var view = beam.AddComponent<BeamView>();
            SetReference(view, "lineRenderer", renderer);
            return view;
        }

        /// <summary>The mine itself: a small drifting hazard the player can also shoot (GDD "Enemies and Hazards").</summary>
        static void BuildMinePrefab()
        {
            const float worldSize = 0.45f;
            var root = new GameObject("Mine");
            try
            {
                var hazardLayer = LayerMask.NameToLayer("Hazard");
                if (hazardLayer < 0) throw new InvalidOperationException("There is no 'Hazard' physics layer.");
                root.layer = hazardLayer;

                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.gravityScale = 0f;

                var renderer = root.AddComponent<SpriteRenderer>();
                var sprite = FindSprite($"{HazardSpriteFolder}/Mine.png");
                if (sprite != null) renderer.sprite = sprite;
                renderer.sortingOrder = EnemySortingOrder;

                var width = renderer.sprite != null ? renderer.sprite.bounds.size.x : 1f;
                var scale = width > 0.001f ? worldSize / width : 1f;
                root.transform.localScale = new Vector3(scale, scale, 1f);

                var collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = renderer.sprite != null
                    ? renderer.sprite.bounds.size.x * 0.5f * ColliderWidthFraction
                    : 0.5f;

                var view = root.AddComponent<MineView>();
                SetReference(view, "body", body);
                SetReference(view, "spriteRenderer", renderer);

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Mine.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Sizes whatever collider the template carries to this ship's own sprite. Without this every enemy
        /// built from the Dart would keep the Dart's hitbox, which a Frigate three times its size would wear
        /// like a pinhead.
        /// </summary>
        static void FitCollider(Collider2D collider, Sprite sprite)
        {
            if (collider == null || sprite == null) return;

            var bounds = (Vector2)sprite.bounds.size;
            var size = new Vector2(bounds.x * ColliderWidthFraction, bounds.y * ColliderHeightFraction);

            switch (collider)
            {
                case CapsuleCollider2D capsule:
                    capsule.size = size;
                    capsule.direction = size.x >= size.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                    capsule.offset = Vector2.zero;
                    break;
                case BoxCollider2D box:
                    box.size = size;
                    box.offset = Vector2.zero;
                    break;
                case CircleCollider2D circle:
                    circle.radius = Mathf.Min(size.x, size.y) * 0.5f;
                    circle.offset = Vector2.zero;
                    break;
                default:
                    Debug.LogWarning($"{nameof(EnemyBuilder)}: cannot size a {collider.GetType().Name}; " +
                                     "it keeps the template's shape.");
                    break;
            }
        }

        /// <summary>
        /// Loads a sprite, complaining if it is not there. A silently missing sprite is how an invisible hazard
        /// gets built, so the builder says so rather than leaving a blank renderer behind.
        /// </summary>
        static Sprite FindSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"{nameof(EnemyBuilder)}: no sprite at '{path}'; art still to come.");
            return sprite;
        }

        /// <summary>Assigns a serialized Vector2 field that is private to its component.</summary>
        static void SetVector(Component component, string field, Vector2 value)
        {
            var serialized = new SerializedObject(component);
            var property = Property(serialized, component, field);
            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty Property(SerializedObject serialized, Component component, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{component.GetType().Name} has no field '{field}'.");
            return property;
        }

        /// <summary>
        /// Assigns a serialized field that is private to its component (every field in this project is), coping
        /// with both single references and arrays of them.
        /// </summary>
        static void SetReference(Component component, string field, UnityEngine.Object value)
        {
            if (component == null)
                throw new InvalidOperationException($"The template is missing the component that owns '{field}'.");

            var serialized = new SerializedObject(component);
            var property = Property(serialized, component, field);

            if (property.isArray)
            {
                property.arraySize = value != null ? 1 : 0;
                if (value != null) property.GetArrayElementAtIndex(0).objectReferenceValue = value;
            }
            else
            {
                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
