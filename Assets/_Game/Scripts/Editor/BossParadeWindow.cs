using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YASS.Gameplay;

namespace YASS.Editor
{
    /// <summary>
    /// Shows every boss in turn, and every enemy that carries a beam, so the art and the beams can be
    /// looked at rather than reasoned about.
    /// </summary>
    /// <remarks>
    /// **Deliberately not a test.** Nothing here asserts anything and nothing here can fail a run: it is a
    /// window to look through. The checks that can be automated already are, in `BeamPrefabTests`,
    /// `BossDataTests` and `BossPrefabArtTests`; what those cannot tell anybody is whether a ship looks
    /// right, whether an engine is in the wrong place, or whether a beam comes out of the gun.
    ///
    /// **And deliberately not play mode.** A PlayMode run in this project costs a domain reload and several
    /// minutes, which is enough friction that nobody looks twice. This renders prefabs straight into a
    /// preview scene from the editor, so opening it is instant and it can be left running while the prefabs
    /// underneath are edited.
    ///
    /// The beams are driven by calling <see cref="BeamView.Show"/> directly rather than by running the
    /// game: a boss's own brain needs a runner, a session and a player to shoot at, none of which say
    /// anything about how the beam looks.
    /// </remarks>
    sealed class BossParadeWindow : EditorWindow
    {
        PreviewRenderUtility _preview;
        List<ParadeAct> _cast;

        GameObject _subject;
        int _showing = -1;

        double _lastTick;
        float _clock;
        bool _running = true;

        /// <summary>
        /// How much of the world to show for the subject on screen, worked out from its own art. A fixed
        /// framing suits whichever ship it was chosen for and nothing else: the bosses are five or six units
        /// across and the Sniper is one, so a frame that fits a boss reduces the Sniper to a speck.
        /// </summary>
        float _framing = 5.5f;

        /// <summary>The viewer's own adjustment on top of that, so it stays useful across subjects.</summary>
        float _zoom = 1f;

        [MenuItem("Tools/YASS/Boss Parade")]
        static void Open()
        {
            var window = GetWindow<BossParadeWindow>("Boss Parade");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        void OnEnable()
        {
            _cast = BossParade.Cast();
            _lastTick = EditorApplication.timeSinceStartup;

            _preview = new PreviewRenderUtility();
            _preview.camera.orthographic = true;
            _preview.camera.orthographicSize = 5.5f;
            _preview.camera.nearClipPlane = 0.1f;
            _preview.camera.farClipPlane = 100f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;

            // The game's night sky. An additive beam judged against grey tells you nothing about how it
            // will look against the thing it is actually drawn over.
            _preview.camera.backgroundColor = new Color(0.04f, 0.03f, 0.09f);
            _preview.camera.transform.position = new Vector3(-2f, 0f, -20f);
            _preview.camera.transform.rotation = Quaternion.identity;

            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;

            Clear();

            // A PreviewRenderUtility owns a camera and a scene, and leaks both if it is not told to go.
            if (_preview != null)
            {
                _preview.Cleanup();
                _preview = null;
            }
        }

        void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var step = (float)(now - _lastTick);
            _lastTick = now;

            if (_running) _clock += Mathf.Min(step, 0.1f); // a paused editor must not skip a whole act

            Repaint();
        }

        void OnGUI()
        {
            DrawControls();

            var frame = GUILayoutUtility.GetRect(position.width, Mathf.Max(120f, position.height - 62f));
            if (Event.current.type != EventType.Repaint) return;
            if (_cast == null || _cast.Count == 0)
            {
                EditorGUI.LabelField(frame, "No bosses or beam enemies found under Assets/_Game/Prefabs.");
                return;
            }

            var index = BossParade.ActAt(_cast, _clock, out var into);
            Present(index);
            Pose(_cast[index], into);

            _preview.camera.orthographicSize = Mathf.Max(0.5f, _framing * _zoom);

            _preview.BeginPreview(frame, GUIStyle.none);
            _preview.camera.Render();
            var rendered = _preview.EndPreview();
            GUI.DrawTexture(frame, rendered, ScaleMode.StretchToFill, false);

            DrawCaption(frame, _cast[index], into);
        }

        void DrawControls()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(_running ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    _running = !_running;

                if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    _clock = 0f;

                if (GUILayout.Button("Previous", EditorStyles.toolbarButton, GUILayout.Width(70f))) Step(-1);
                if (GUILayout.Button("Next", EditorStyles.toolbarButton, GUILayout.Width(50f))) Step(1);

                GUILayout.FlexibleSpace();

                // Rebuilding the cast is how a prefab edited while this is open gets picked up.
                if (GUILayout.Button("Reload prefabs", EditorStyles.toolbarButton))
                {
                    Clear();
                    _cast = BossParade.Cast();
                }

                GUILayout.Label($"{_cast?.Count ?? 0} subjects, " +
                                $"{BossParade.TotalSeconds(_cast ?? new List<ParadeAct>()):0.0}s a lap",
                    EditorStyles.miniLabel);
            }

            _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.4f, 3f);
        }

        void Step(int by)
        {
            var index = BossParade.ActAt(_cast, _clock, out _);
            var wanted = (index + by + _cast.Count) % _cast.Count;

            _clock = 0f;
            for (var i = 0; i < wanted; i++) _clock += _cast[i].Seconds;
        }

        /// <summary>Puts the right subject in the preview scene, swapping only when it actually changes.</summary>
        void Present(int index)
        {
            if (index == _showing && _subject != null) return;

            Clear();
            _showing = index;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_cast[index].Path);
            if (prefab == null) return;

            _subject = Instantiate(prefab);
            _subject.hideFlags = HideFlags.HideAndDontSave;
            _subject.transform.position = Vector3.zero;

            // A boss keeps its art out of its prefab and loads it by path at run time, so nothing is in the
            // renderer here until it is put there. Same route the game uses, just without the cache.
            var view = _subject.GetComponent<BossView>();
            var body = _subject.GetComponent<SpriteRenderer>();
            if (view != null && body != null && body.sprite == null && view.Definition != null)
                body.sprite = Resources.Load<Sprite>(view.Definition.SpritePath);

            _framing = FramingFor(_subject);

            _preview.AddSingleGO(_subject);
        }

        /// <summary>
        /// How far out to stand, from the widest piece of the ship itself. The beam is ignored on purpose:
        /// it is forty units long, and framing to include it would push every ship into the distance.
        /// </summary>
        static float FramingFor(GameObject subject)
        {
            var widest = 0f;

            foreach (var renderer in subject.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.GetComponent<BeamView>() != null) continue;
                if (renderer.name.StartsWith("Beam", System.StringComparison.Ordinal)) continue;
                if (renderer.sprite == null) continue;

                var size = renderer.sprite.bounds.size * renderer.transform.lossyScale.x;
                widest = Mathf.Max(widest, size.x, size.y);
            }

            // Roughly one ship-and-a-half of sky, and never so tight that a small ship fills the frame.
            return Mathf.Max(1.6f, widest * 0.85f);
        }

        /// <summary>Drives the beam for this moment of the act. Nothing else about the ship moves.</summary>
        void Pose(ParadeAct act, float into)
        {
            if (_subject == null) return;

            var beam = _subject.GetComponentInChildren<BeamView>(true);
            if (beam == null) return;

            if (!act.HasBeam)
            {
                beam.Hide();
                return;
            }

            var phase = BossParade.PhaseAt(into, out var progress);
            if (phase == BossParade.BeamPhase.Off)
            {
                beam.Hide();
                return;
            }

            // Every number comes from the same definition the game reads. A parade that showed a beam of a
            // width nobody fires would be worse than no parade: it would look like confirmation.
            float from, to, half, length;

            // The beam's own object is where it comes from, exactly as the game reads it. Asking the muzzle
            // field instead would draw the parade's beam somewhere the real one never appears, which is the
            // one thing a window like this must not do.
            var origin = beam.LocalOrigin;

            var boss = _subject.GetComponent<BossView>();
            if (boss != null && boss.Definition != null)
            {
                from = boss.Definition.SweepFromDegrees;
                to = boss.Definition.SweepToDegrees;
                half = boss.Definition.BeamHalfWidth;
                length = boss.Definition.BeamLength;
            }
            else
            {
                var enemy = _subject.GetComponent<EnemyView>();
                half = enemy != null && enemy.Definition != null ? enemy.Definition.BeamHalfWidth : 0.15f;
                length = enemy != null && enemy.Definition != null ? enemy.Definition.BeamLength : 40f;

                // A Sniper does not sweep: it picks a line and fires down it. Swung a little here anyway,
                // so the shot is seen at more than one angle in a lap.
                from = 195f;
                to = 165f;
            }

            // The warning holds at the angle the sweep starts from, because that is what it is warning about.
            var angle = phase == BossParade.BeamPhase.Firing ? Mathf.Lerp(from, to, progress) : from;
            var aim = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            beam.Show((Vector2)_subject.transform.position + origin, aim, length, half,
                phase == BossParade.BeamPhase.Firing, progress);
        }

        void DrawCaption(Rect frame, ParadeAct act, float into)
        {
            var phase = act.HasBeam
                ? BossParade.PhaseAt(into, out _).ToString().ToLowerInvariant()
                : "idle";

            var caption = new Rect(frame.x + 10f, frame.y + 8f, frame.width - 20f, 40f);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };

            GUI.Label(caption, act.Name, style);
            GUI.Label(new Rect(caption.x, caption.y + 22f, caption.width, 20f),
                act.HasBeam ? $"beam: {phase}" : "no beam",
                new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.8f, 0.8f, 0.9f) } });
        }

        void Clear()
        {
            if (_subject != null) DestroyImmediate(_subject);

            _subject = null;
            _showing = -1;
        }
    }
}
