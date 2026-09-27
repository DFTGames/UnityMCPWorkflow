using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YASS.Gameplay;

namespace YASS.Editor
{
    /// <summary>One thing to look at, and how long to look at it.</summary>
    struct ParadeAct
    {
        public string Path;
        public string Name;

        /// <summary>Whether it has a beam to demonstrate, which is what decides how long it gets.</summary>
        public bool HasBeam;

        public float Seconds;
    }

    /// <summary>
    /// What the parade shows and in what order: every boss in turn, then the enemies that carry a beam.
    /// </summary>
    /// <remarks>
    /// Separate from the window so the running order is one readable list rather than something buried in a
    /// repaint. Everything here is found by looking at the prefabs, not by a list somebody has to remember
    /// to add to: a ninth boss appears in the parade the day its prefab does.
    /// </remarks>
    static class BossParade
    {
        const string Folder = "Assets/_Game/Prefabs";

        /// <summary>Long enough to read the shape of a ship without the whole run becoming a wait.</summary>
        public const float IdleSeconds = 1.5f;

        /// <summary>Idle, then the telegraph building, then the sweep. The three things worth seeing.</summary>
        public const float BeamSeconds = 3.2f;

        public static List<ParadeAct> Cast()
        {
            var bosses = new List<ParadeAct>();
            var others = new List<ParadeAct>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // The template is a starting point to duplicate, not a boss, and it has no art to show.
                if (prefab.name.StartsWith("_")) continue;

                var isBoss = prefab.GetComponent<BossView>() != null;
                var beam = prefab.GetComponentInChildren<BeamView>(true) != null;

                // Everything else in the folder is a bullet, a pickup or an enemy with no beam, and a
                // parade of those is not what anybody opened this for.
                if (!isBoss && !beam) continue;

                var act = new ParadeAct
                {
                    Path = path,
                    Name = prefab.name,
                    HasBeam = beam,
                    Seconds = beam ? BeamSeconds : IdleSeconds
                };

                (isBoss ? bosses : others).Add(act);
            }

            bosses.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            others.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            bosses.AddRange(others);
            return bosses;
        }

        public static float TotalSeconds(List<ParadeAct> cast)
        {
            var total = 0f;
            foreach (var act in cast) total += act.Seconds;

            return total;
        }

        /// <summary>Which act a moment in the run belongs to, and how far into it that moment is.</summary>
        public static int ActAt(List<ParadeAct> cast, float seconds, out float into)
        {
            into = 0f;
            if (cast.Count == 0) return -1;

            var t = Mathf.Repeat(seconds, Mathf.Max(0.0001f, TotalSeconds(cast)));

            for (var i = 0; i < cast.Count; i++)
            {
                if (t < cast[i].Seconds)
                {
                    into = t;
                    return i;
                }

                t -= cast[i].Seconds;
            }

            into = 0f;
            return cast.Count - 1;
        }

        /// <summary>What the beam is doing at a moment within an act.</summary>
        public enum BeamPhase { Off, Warning, Firing }

        public static BeamPhase PhaseAt(float into, out float progress)
        {
            const float idle = 0.5f;
            const float warn = 1.1f;

            if (into < idle)
            {
                progress = 0f;
                return BeamPhase.Off;
            }

            if (into < idle + warn)
            {
                progress = (into - idle) / warn;
                return BeamPhase.Warning;
            }

            // The rest of the act is the sweep, so the beam is seen moving rather than held still.
            progress = Mathf.Clamp01((into - idle - warn) / Mathf.Max(0.0001f, BeamSeconds - idle - warn));
            return BeamPhase.Firing;
        }
    }
}
