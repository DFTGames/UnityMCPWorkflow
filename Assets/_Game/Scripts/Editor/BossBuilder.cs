using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YASS.Core;
using YASS.Feedback;
using YASS.Gameplay;

namespace YASS.Editor
{
    /// <summary>
    /// Builds the campaign's eight bosses: one <see cref="BossDefinition"/> asset and one prefab each
    /// (GDD "Levels" and the per-level pages). Every boss is the same <see cref="BossView"/> and
    /// <see cref="BossBrain"/> with different data, so a boss is designed here rather than in a new class.
    /// Re-runnable: change a number and rebuild.
    /// </summary>
    public static class BossBuilder
    {
        /// <summary>
        /// The shape every boss is built from: rigidbody, hull colliders, core child and engines. It is a
        /// separate asset rather than the Hive Carrier's own prefab, because the Hive Carrier is one of this
        /// builder's outputs: reading and writing the same file would make each run inherit the last one's
        /// rounding, and a bad run would poison the template it reads next time.
        /// </summary>
        const string TemplatePrefab = "Assets/_Game/Prefabs/_BossTemplate.prefab";
        const string TemplateSource = "Assets/_Game/Prefabs/HiveCarrier.prefab";
        const string PrefabFolder = "Assets/_Game/Prefabs";
        const string DefinitionFolder = "Assets/_Game/ScriptableObjects/Bosses";
        const string BossSpriteFolder = "Assets/_Game/Sprites/Bosses";
        const string EnemyPrefabFolder = "Assets/_Game/Prefabs";
        const string MeteorFolder = "Assets/_Game/ScriptableObjects/Meteors";
        const string WhiteSpritePath = "Assets/_Game/Sprites/Procedural/UIWhite.png";

        /// <summary>Enemies and meteors draw at 1, enemy shots at 3 (GDD "Art Direction").</summary>
        const int BossSortingOrder = 1;
        const int BeamSortingOrder = 3;

        /// <summary>
        /// The Hive Carrier's own proportions, as fractions of its sprite. Every boss is laid out to these, so a
        /// hull three times the size still has armour above and below its core rather than over it.
        /// </summary>
        const float CoreXFraction = -0.1548f;
        const float CoreRadiusFraction = 0.1283f;
        const float ArmourWidthFraction = 0.857f;
        const float ArmourHeightFraction = 0.2566f;
        const float ArmourYFraction = 0.3048f;
        const float RearWidthFraction = 0.5238f;
        const float RearHeightFraction = 0.2887f;
        const float RearXFraction = 0.2143f;

        sealed class Spec
        {
            public string Name;
            public string DisplayName;
            public int Level;
            public float WorldWidth = 4.2f;
            public float Health;
            public float CycleSeconds = 8f;
            public float OpenSeconds = 4f;
            public float ContactDamage = 40f;
            public float HoldInset = 3f;
            public float DriftAmplitude = 2.2f;
            public float DriftPeriod = 6f;
            public float SpreadAngle = 60f;
            public float BulletSpeed = 6f;
            public float BulletDamage = 10f;
            public string Minion;
            public string Meteor;
            public float MeteorSpeed = 5f;
            public float SweepFrom = 150f;
            public float SweepTo = 210f;
            public float SweepSeconds = 2.5f;
            public float BeamDamage = 15f;
            public float BeamHalfWidth = 0.35f;
            public float EntrySpeed = 2f;
            public float BeamDamageInterval = 0.5f;
            public float BeamLength = 40f;
            public float DashWindUp = 0.7f;
            public float DashSpeed = 22f;
            public float DashReturnSpeed = 7f;
            public float DashOvershoot = 1f;
            public float PullSeconds = 2f;
            public float PullStrength = 4f;
            public float PullRadius = 14f;
            public BossDefinition.AttackRow[] Attacks;

            /// <summary>Whether this boss needs the beam object (only the ones that sweep).</summary>
            public bool NeedsBeam
            {
                get
                {
                    foreach (var row in Attacks)
                        if (row.type == BossActionType.BeamSweep) return true;
                    return false;
                }
            }
        }

        /// <summary>One row of a boss's attack table. Health bands default to "always".</summary>
        static BossDefinition.AttackRow Attack(BossActionType type, int count, BossPhase when,
            float firstDelaySeconds = 0f, float intervalSeconds = 0f, float above = 0f, float atOrBelow = 1f) =>
            new BossDefinition.AttackRow
            {
                type = type,
                count = count,
                when = when,
                firstDelaySeconds = firstDelaySeconds,
                intervalSeconds = intervalSeconds,
                activeAbove = above,
                activeAtOrBelow = atOrBelow
            };

        /// <summary>Half health: where every boss in the campaign turns nastier.</summary>
        const float Half = BossBrain.EnragedAtHealthFraction;

        static Spec[] Specs() => new[]
        {
            // 1. Hive Carrier: launches Darts while shut, fans bullets while open (GDD "Hive Carrier").
            new Spec
            {
                Name = "HiveCarrier", DisplayName = "Hive Carrier", Level = 1, Health = 200f,
                WorldWidth = 4.2f, Minion = "Dart",
                Attacks = new[]
                {
                    Attack(BossActionType.LaunchDarts, 3, BossPhase.Armoured),
                    Attack(BossActionType.LaunchDarts, 3, BossPhase.Armoured, firstDelaySeconds: 2f, atOrBelow: Half),
                    Attack(BossActionType.FireSpread, 5, BossPhase.Vulnerable, intervalSeconds: 1.5f, above: Half),
                    Attack(BossActionType.FireSpread, 7, BossPhase.Vulnerable, intervalSeconds: 1.5f, atOrBelow: Half)
                }
            },

            // 2. Rock Crusher: throws the belt at you.
            new Spec
            {
                Name = "RockCrusher", DisplayName = "Rock Crusher", Level = 2, Health = 260f,
                WorldWidth = 4f, CycleSeconds = 8f, OpenSeconds = 3.5f, Meteor = "MeteorLarge", MeteorSpeed = 5f,
                SpreadAngle = 40f, DriftAmplitude = 2.6f,
                Attacks = new[]
                {
                    Attack(BossActionType.HurlMeteor, 1, BossPhase.Armoured, intervalSeconds: 1.6f, above: Half),
                    Attack(BossActionType.HurlMeteor, 2, BossPhase.Armoured, intervalSeconds: 1.4f, atOrBelow: Half),
                    Attack(BossActionType.FireSpread, 3, BossPhase.Vulnerable, intervalSeconds: 2f)
                }
            },

            // 3. Sunforge: sweeping flame beams you have to be somewhere else for.
            new Spec
            {
                Name = "Sunforge", DisplayName = "Sunforge", Level = 3, Health = 320f,
                // Its armoured phase is 5 s, and a sweep has to finish inside the phase that started it:
                // two 2 s sweeps fit, two 2.5 s ones do not.
                WorldWidth = 4.4f, CycleSeconds = 9f, OpenSeconds = 4f, SweepSeconds = 2f,
                SweepFrom = 140f, SweepTo = 220f, BeamDamage = 15f, BulletSpeed = 6.5f,
                Attacks = new[]
                {
                    Attack(BossActionType.BeamSweep, 1, BossPhase.Armoured, firstDelaySeconds: 0.6f),
                    Attack(BossActionType.BeamSweep, 1, BossPhase.Armoured, firstDelaySeconds: 2.8f, atOrBelow: Half),
                    Attack(BossActionType.FireSpread, 5, BossPhase.Vulnerable, intervalSeconds: 1.8f)
                }
            },

            // 4. Frost Lancer: dashes across the screen, then comes back for another run.
            new Spec
            {
                // A dash crosses about 16 units and comes back, which takes roughly 2.5 s all told: the
                // armoured phase is 6 s so that the second dash of its enraged half has somewhere to happen.
                Name = "FrostLancer", DisplayName = "Frost Lancer", Level = 4, Health = 360f,
                WorldWidth = 3.6f, CycleSeconds = 9f, OpenSeconds = 3f, ContactDamage = 45f,
                DashWindUp = 0.7f, DashSpeed = 22f, DashReturnSpeed = 14f,
                DriftAmplitude = 3f, DriftPeriod = 5f, BulletSpeed = 7f,
                Attacks = new[]
                {
                    Attack(BossActionType.Dash, 1, BossPhase.Armoured, firstDelaySeconds: 0.5f),
                    Attack(BossActionType.Dash, 1, BossPhase.Armoured, firstDelaySeconds: 3.2f, atOrBelow: Half),
                    Attack(BossActionType.FireSpread, 3, BossPhase.Vulnerable, intervalSeconds: 1.4f)
                }
            },

            // 5. Dreadnought: a wall of turrets, and drones from its bays.
            new Spec
            {
                Name = "Dreadnought", DisplayName = "Dreadnought", Level = 5, Health = 460f,
                WorldWidth = 5f, CycleSeconds = 10f, OpenSeconds = 4f, ContactDamage = 45f,
                Minion = "SwarmDrone", SpreadAngle = 70f, BulletSpeed = 6.5f, DriftAmplitude = 1.8f,
                Attacks = new[]
                {
                    Attack(BossActionType.TurretVolley, 1, BossPhase.Armoured, intervalSeconds: 1.4f, above: Half),
                    Attack(BossActionType.TurretVolley, 1, BossPhase.Armoured, intervalSeconds: 0.9f, atOrBelow: Half),
                    Attack(BossActionType.LaunchDarts, 3, BossPhase.Armoured, firstDelaySeconds: 3f),
                    Attack(BossActionType.FireSpread, 7, BossPhase.Vulnerable, intervalSeconds: 2f)
                }
            },

            // 6. Tempest: lightning arcs that make half the screen dangerous at a time.
            new Spec
            {
                Name = "Tempest", DisplayName = "Tempest", Level = 6, Health = 500f,
                WorldWidth = 4.4f, CycleSeconds = 9f, OpenSeconds = 3.5f,
                SweepFrom = 200f, SweepTo = 160f, SweepSeconds = 1.6f, BeamDamage = 12f, BeamHalfWidth = 0.25f,
                SpreadAngle = 90f, BulletSpeed = 7f, DriftAmplitude = 3.2f, DriftPeriod = 4.5f,
                Attacks = new[]
                {
                    Attack(BossActionType.BeamSweep, 1, BossPhase.Armoured, firstDelaySeconds: 0.4f, intervalSeconds: 2.6f),
                    Attack(BossActionType.FireSpread, 5, BossPhase.Vulnerable, intervalSeconds: 1.2f, above: Half),
                    Attack(BossActionType.FireSpread, 9, BossPhase.Vulnerable, intervalSeconds: 1.2f, atOrBelow: Half)
                }
            },

            // 7. Singularity Engine: drags you in, then fills the space you are dragged into.
            new Spec
            {
                Name = "SingularityEngine", DisplayName = "Singularity Engine", Level = 7, Health = 560f,
                WorldWidth = 4.6f, CycleSeconds = 10f, OpenSeconds = 4f, ContactDamage = 50f,
                Meteor = "MeteorSolid", MeteorSpeed = 4.5f, PullSeconds = 2.5f, PullStrength = 4f, PullRadius = 16f,
                SpreadAngle = 80f, BulletSpeed = 6f, DriftAmplitude = 1.4f,
                Attacks = new[]
                {
                    Attack(BossActionType.GravityPull, 1, BossPhase.Armoured, firstDelaySeconds: 0.5f),
                    Attack(BossActionType.GravityPull, 1, BossPhase.Armoured, firstDelaySeconds: 3.5f, atOrBelow: Half),
                    Attack(BossActionType.HurlMeteor, 2, BossPhase.Armoured, firstDelaySeconds: 1.5f, intervalSeconds: 2.2f),
                    Attack(BossActionType.FireSpread, 7, BossPhase.Vulnerable, intervalSeconds: 1.5f)
                }
            },

            // 8. The Overmind: three phases, each remixing an earlier boss (GDD "Levels").
            new Spec
            {
                Name = "Overmind", DisplayName = "The Overmind", Level = 8, Health = 700f,
                WorldWidth = 5.2f, CycleSeconds = 11f, OpenSeconds = 4.5f, ContactDamage = 50f,
                Minion = "Diver", Meteor = "MeteorLarge", SpreadAngle = 100f, BulletSpeed = 7f, BulletDamage = 12f,
                SweepFrom = 135f, SweepTo = 225f, SweepSeconds = 2.2f, BeamDamage = 15f,
                DashWindUp = 0.6f, DashSpeed = 24f, DashReturnSpeed = 14f,
                DriftAmplitude = 2.4f, DriftPeriod = 5.5f,
                Attacks = new[]
                {
                    // First third: the Hive Carrier's fight.
                    Attack(BossActionType.LaunchDarts, 3, BossPhase.Armoured, intervalSeconds: 3.5f, above: 0.66f),
                    Attack(BossActionType.FireSpread, 7, BossPhase.Vulnerable, intervalSeconds: 1.6f, above: 0.66f),

                    // Second third: the Sunforge and the Dreadnought.
                    Attack(BossActionType.BeamSweep, 1, BossPhase.Armoured, firstDelaySeconds: 0.5f, above: 0.33f,
                        atOrBelow: 0.66f),
                    Attack(BossActionType.TurretVolley, 1, BossPhase.Armoured, firstDelaySeconds: 3f,
                        intervalSeconds: 1.2f, above: 0.33f, atOrBelow: 0.66f),
                    Attack(BossActionType.FireSpread, 9, BossPhase.Vulnerable, intervalSeconds: 1.4f, above: 0.33f,
                        atOrBelow: 0.66f),

                    // Last third: the Frost Lancer, and everything it has left.
                    Attack(BossActionType.Dash, 1, BossPhase.Armoured, firstDelaySeconds: 0.4f, atOrBelow: 0.33f),
                    Attack(BossActionType.LaunchDarts, 4, BossPhase.Armoured, firstDelaySeconds: 4f, atOrBelow: 0.33f),
                    Attack(BossActionType.FireSpread, 11, BossPhase.Vulnerable, intervalSeconds: 1.2f, atOrBelow: 0.33f)
                }
            }
        };

        [MenuItem("Tools/YASS/Build Bosses")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefab) == null &&
                !AssetDatabase.CopyAsset(TemplateSource, TemplatePrefab))
            {
                Debug.LogError($"{nameof(BossBuilder)}: no template at '{TemplatePrefab}', and '{TemplateSource}' " +
                               "could not be copied to make one.");
                return;
            }

            Directory.CreateDirectory(DefinitionFolder);
            Directory.CreateDirectory(BossSpriteFolder);
            AssetDatabase.Refresh();

            var specs = Specs();
            foreach (var spec in specs)
            {
                var definition = BuildDefinition(spec);
                BuildPrefab(spec, definition);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{nameof(BossBuilder)}: built {specs.Length} bosses.");
        }

        static BossDefinition BuildDefinition(Spec spec)
        {
            var path = $"{DefinitionFolder}/{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BossDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);

            SerializedProperty Find(string field)
            {
                var property = serialized.FindProperty(field);
                if (property == null) throw new InvalidOperationException($"BossDefinition has no field '{field}'.");
                return property;
            }

            void Number(string field, float value) => Find(field).floatValue = value;

            Find("displayName").stringValue = spec.DisplayName;
            Find("levelNumber").intValue = spec.Level;
            Number("maxHealth", spec.Health);
            Number("entrySpeed", spec.EntrySpeed);
            Number("beamDamageInterval", spec.BeamDamageInterval);
            Number("beamLength", spec.BeamLength);
            Number("dashReturnSpeed", spec.DashReturnSpeed);
            Number("dashOvershoot", spec.DashOvershoot);
            Number("cycleSeconds", spec.CycleSeconds);
            Number("openSeconds", spec.OpenSeconds);
            Number("contactDamage", spec.ContactDamage);
            Number("holdInset", spec.HoldInset);
            Number("driftAmplitude", spec.DriftAmplitude);
            Number("driftPeriod", spec.DriftPeriod);
            Number("spreadAngle", spec.SpreadAngle);
            Number("bulletSpeed", spec.BulletSpeed);
            Number("bulletDamage", spec.BulletDamage);
            Number("meteorSpeed", spec.MeteorSpeed);
            Number("sweepFromDegrees", spec.SweepFrom);
            Number("sweepToDegrees", spec.SweepTo);
            Number("sweepSeconds", spec.SweepSeconds);
            Number("beamDamage", spec.BeamDamage);
            Number("beamHalfWidth", spec.BeamHalfWidth);
            Number("dashWindUpSeconds", spec.DashWindUp);
            Number("dashSpeed", spec.DashSpeed);
            Number("pullSeconds", spec.PullSeconds);
            Number("pullStrength", spec.PullStrength);
            Number("pullRadius", spec.PullRadius);

            Find("minionPrefab").objectReferenceValue = spec.Minion != null
                ? LoadEnemy(spec.Minion)
                : null;
            Find("meteor").objectReferenceValue = spec.Meteor != null
                ? AssetDatabase.LoadAssetAtPath<MeteorDefinition>($"{MeteorFolder}/{spec.Meteor}.asset")
                : null;

            var attacks = Find("attacks");
            attacks.arraySize = spec.Attacks.Length;
            for (var i = 0; i < spec.Attacks.Length; i++)
            {
                var row = spec.Attacks[i];
                var element = attacks.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("type").enumValueIndex = (int)row.type;
                element.FindPropertyRelative("count").intValue = row.count;
                element.FindPropertyRelative("when").enumValueIndex = (int)row.when;
                element.FindPropertyRelative("firstDelaySeconds").floatValue = row.firstDelaySeconds;
                element.FindPropertyRelative("intervalSeconds").floatValue = row.intervalSeconds;
                element.FindPropertyRelative("activeAbove").floatValue = row.activeAbove;
                element.FindPropertyRelative("activeAtOrBelow").floatValue = row.activeAtOrBelow;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            if (definition.Validate() is string problem)
                Debug.LogError($"{nameof(BossBuilder)}: {spec.Name} is not usable: {problem}.");

            return definition;
        }

        static EnemyView LoadEnemy(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyPrefabFolder}/{name}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"{nameof(BossBuilder)}: no enemy prefab '{name}' to launch.");
                return null;
            }

            return prefab.GetComponent<EnemyView>();
        }

        static void BuildPrefab(Spec spec, BossDefinition definition)
        {
            var path = $"{PrefabFolder}/{spec.Name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(TemplatePrefab);
            try
            {
                root.name = spec.Name;

                var renderer = root.GetComponent<SpriteRenderer>();
                var sprite = FindSprite($"{BossSpriteFolder}/{spec.Name}.png");
                if (sprite != null) renderer.sprite = sprite;
                renderer.sortingOrder = BossSortingOrder;

                var bounds = renderer.sprite != null ? (Vector2)renderer.sprite.bounds.size : new Vector2(4.2f, 3.1f);
                var scale = bounds.x > 0.001f ? spec.WorldWidth / bounds.x : 1f;
                root.transform.localScale = new Vector3(scale, scale, 1f);

                LayOutHull(root, bounds);
                LayOutCore(root, bounds);
                LayOutEngines(root, bounds);

                // The hull flashes when the armour absorbs a shot; the core never does, because its colour is
                // the player's one tell (GDD "Art Direction").
                SetReference(root.GetComponent<HitFlash>(), "renderers", renderer);

                var view = root.GetComponent<BossView>();
                SetReference(view, "definition", definition);

                // Always cleared first: a boss that has stopped sweeping must not keep a beam object behind.
                var existingBeam = root.transform.Find("Beam");
                if (existingBeam != null) UnityEngine.Object.DestroyImmediate(existingBeam.gameObject);
                SetReference(view, "beam", spec.NeedsBeam ? BuildBeam(root) : null);
                SetVectorArray(view, "launchBays", LaunchBays(bounds, spec));
                SetVectorArray(view, "turretMuzzles", TurretMuzzles(bounds));
                SetVector(view, "muzzle", new Vector2(-bounds.x * 0.5f - 0.1f, 0f));

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Armour above, below and behind the core, in the Hive Carrier's proportions. Nothing may overlap the
        /// core, or shots aimed at an open core would be absorbed by the hull instead.
        /// </summary>
        static void LayOutHull(GameObject root, Vector2 bounds)
        {
            var boxes = root.GetComponents<BoxCollider2D>();
            if (boxes.Length < 3)
            {
                Debug.LogWarning($"{nameof(BossBuilder)}: the template has {boxes.Length} armour colliders, expected 3.");
                return;
            }

            boxes[0].size = new Vector2(bounds.x * ArmourWidthFraction, bounds.y * ArmourHeightFraction);
            boxes[0].offset = new Vector2(0f, bounds.y * ArmourYFraction);
            boxes[1].size = boxes[0].size;
            boxes[1].offset = new Vector2(0f, -bounds.y * ArmourYFraction);
            boxes[2].size = new Vector2(bounds.x * RearWidthFraction, bounds.y * RearHeightFraction);
            boxes[2].offset = new Vector2(bounds.x * RearXFraction, 0f);
        }

        static void LayOutCore(GameObject root, Vector2 bounds)
        {
            var core = root.transform.Find("Core");
            if (core == null)
            {
                Debug.LogWarning($"{nameof(BossBuilder)}: the template has no Core child.");
                return;
            }

            var radius = CoreRadius(bounds);
            core.localPosition = new Vector3(bounds.x * CoreXFraction, 0f, 0f);

            // The core sprite keeps its own art, scaled to the hole in the armour. The collider shares this
            // transform, so its radius has to be divided by that scale or the hitbox ends up a fraction of the
            // core the player is aiming at.
            var scale = 1f;
            var coreRenderer = core.GetComponent<SpriteRenderer>();
            if (coreRenderer != null && coreRenderer.sprite != null)
            {
                var own = coreRenderer.sprite.bounds.size.x;
                if (own > 0.001f) scale = radius * 2f / own;
            }

            core.localScale = new Vector3(scale, scale, 1f);

            var circle = core.GetComponent<CircleCollider2D>();
            if (circle != null)
            {
                circle.radius = radius / scale;
                circle.offset = Vector2.zero;
            }
        }

        /// <summary>
        /// The core's radius in world units. It is a fraction of the hull's height, but it also has to stay clear
        /// of the rear armour: the sprite generator returns square images, so a boss whose hull is nearly as tall
        /// as it is wide would otherwise end up with armour lying over its one weak spot.
        /// </summary>
        static float CoreRadius(Vector2 bounds)
        {
            var wanted = bounds.y * CoreRadiusFraction;

            // Where the rear armour begins, less a margin, measured from the core's centre.
            var rearEdge = bounds.x * RearXFraction - bounds.x * RearWidthFraction * 0.5f;
            var clearance = rearEdge - bounds.x * CoreXFraction - 0.05f;

            return Mathf.Min(wanted, Mathf.Max(0.1f, clearance));
        }

        /// <summary>
        /// The exhausts sit just behind the hull, wherever its rear edge is. Left where the template put them,
        /// a small boss trails its engines in empty space and a large one buries them inside itself.
        /// </summary>
        static void LayOutEngines(GameObject root, Vector2 bounds)
        {
            var engines = root.GetComponentsInChildren<EngineExhaust>(true);
            for (var i = 0; i < engines.Length; i++)
            {
                var transform = engines[i].transform;
                if (transform.parent != root.transform) continue; // a nested engine is laid out by its own parent

                var spread = engines.Length == 1 ? 0f : (i / (float)(engines.Length - 1) - 0.5f) * bounds.y * 0.45f;
                transform.localPosition = new Vector3(bounds.x * 0.48f, spread, 0f);
            }
        }

        /// <summary>Bays are spread down the hull's leading edge, one per enemy of a launch.</summary>
        static Vector2[] LaunchBays(Vector2 bounds, Spec spec)
        {
            var most = 1;
            foreach (var row in spec.Attacks)
                if (row.type == BossActionType.LaunchDarts) most = Mathf.Max(most, row.count);

            var bays = new Vector2[most];
            for (var i = 0; i < most; i++)
            {
                // Spread evenly across the hull's height, centred: 1 bay is on the nose, 3 are nose and shoulders.
                var t = most == 1 ? 0.5f : i / (float)(most - 1);
                bays[i] = new Vector2(-bounds.x * 0.3f, (t - 0.5f) * bounds.y * 0.6f);
            }

            return bays;
        }

        static Vector2[] TurretMuzzles(Vector2 bounds) => new[]
        {
            new Vector2(-bounds.x * 0.2f, bounds.y * 0.38f),
            new Vector2(-bounds.x * 0.2f, -bounds.y * 0.38f),
            new Vector2(bounds.x * 0.15f, 0f)
        };

        /// <summary>The sweeping beam: one stretched white sprite the boss drives itself.</summary>
        static BeamView BuildBeam(GameObject root)
        {
            var beam = new GameObject("Beam");
            beam.transform.SetParent(root.transform, false);
            beam.layer = root.layer;

            var renderer = beam.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            renderer.sortingOrder = BeamSortingOrder;
            renderer.enabled = false;

            var view = beam.AddComponent<BeamView>();
            SetReference(view, "lineRenderer", renderer);
            return view;
        }

        static Sprite FindSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"{nameof(BossBuilder)}: no sprite at '{path}'; art still to come.");
            return sprite;
        }

        static SerializedProperty Property(SerializedObject serialized, Component component, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{component.GetType().Name} has no field '{field}'.");
            return property;
        }

        static void SetReference(Component component, string field, UnityEngine.Object value)
        {
            if (component == null)
                throw new InvalidOperationException($"The template is missing the component that owns '{field}'.");

            var serialized = new SerializedObject(component);
            var property = Property(serialized, component, field);
            if (property.isArray && property.propertyType != SerializedPropertyType.ObjectReference)
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

        static void SetVector(Component component, string field, Vector2 value)
        {
            var serialized = new SerializedObject(component);
            Property(serialized, component, field).vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVectorArray(Component component, string field, IReadOnlyList<Vector2> values)
        {
            var serialized = new SerializedObject(component);
            var property = Property(serialized, component, field);
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).vector2Value = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
