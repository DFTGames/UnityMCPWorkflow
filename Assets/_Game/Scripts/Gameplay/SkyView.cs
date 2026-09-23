using System.Collections.Generic;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// The sky: one backdrop in a campaign level, or the drifting ring of an Endless run (GDD "Core Loop",
    /// Endless mode), painted as two renderers with the arriving one fading in over the one being left.
    /// <see cref="SkyCycle"/> decides which two those are and how far the dissolve has got.
    /// </summary>
    /// <remarks>
    /// The skies are held by path through a <see cref="ContentCache"/>, never referenced directly, so a run
    /// holds the sky on screen and nothing else. The one coming next is fetched in the background a little
    /// before its dissolve begins, and the one being left is given back the moment that dissolve ends, so the
    /// pair overlap only for the seconds they are both on screen. Each is 1.13 MB resident, so the ring is
    /// 6.8 MB if it is all held at once and 1.13 MB if it is not.
    ///
    /// Both renderers sit below everything else (GDD "Art Direction": background -20), the arriving one a
    /// single sorting step above the other so it can be drawn over it.
    /// </remarks>
    public sealed class SkyView : MonoBehaviour
    {
        [SerializeField, Tooltip("The sky being left. Also the only one a campaign level ever shows.")]
        SpriteRenderer showing;

        [SerializeField, Tooltip("Drawn over the other one, its transparency carrying the dissolve.")]
        SpriteRenderer arriving;

        [SerializeField, Min(0f), Tooltip("Seconds a sky is held whole before it starts to go.")]
        float holdSeconds = 75f;

        [SerializeField, Min(0.5f), Tooltip("Seconds one sky takes to dissolve into the next.")]
        float fadeSeconds = 8f;

        [SerializeField, Min(1f), Tooltip("How long before a dissolve the next sky starts loading, in seconds.")]
        float prefetchSeconds = 15f;

        readonly List<string> _wanted = new List<string>(2);

        ContentCache _cache;
        IReadOnlyList<string> _skyPaths;
        SkyCycle _cycle;
        string _showingPath;
        string _arrivingPath;

        /// <summary>The dissolve in progress, for tests. Null until a run has given it a ring of skies.</summary>
        internal SkyCycle Cycle => _cycle;

        internal float ArrivingAlpha => arriving != null ? arriving.color.a : 0f;

        /// <summary>The timings the scene was built with, so a test drives the drift the way the game does.</summary>
        internal float HoldSeconds => holdSeconds;
        internal float FadeSeconds => fadeSeconds;

        /// <summary>What the cache is holding, counting a sky still on its way in. For tests.</summary>
        internal int CacheCount => _cache != null ? _cache.Count : 0;

        /// <summary>
        /// Shows one sky and leaves it there: a campaign level's own backdrop, which never changes while it is
        /// being played. Anything held for a previous level or run is given back.
        /// </summary>
        public void Show(ContentCache cache, string backdropPath)
        {
            if (showing == null)
            {
                Debug.LogError($"{nameof(SkyView)}: no renderer to show a backdrop on.", this);
                return;
            }

            _cache = cache;
            _cycle = null;
            _skyPaths = null;
            _showingPath = backdropPath;
            _arrivingPath = null;

            // Exactly this one sky: anything held for a previous level or run is freed by the same call.
            if (cache != null)
            {
                _wanted.Clear();
                if (!string.IsNullOrEmpty(backdropPath)) _wanted.Add(backdropPath);
                cache.Require<Sprite>(_wanted);
            }

            showing.sprite = cache != null ? cache.Get<Sprite>(backdropPath) : null;

            if (arriving != null)
            {
                arriving.sprite = null;
                arriving.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Starts the drift. <paramref name="startIndex"/> is which sky to open on, so two runs do not begin
        /// the same way. A ring with nothing in it leaves the scene's own backdrop alone.
        /// </summary>
        public void Begin(ContentCache cache, IReadOnlyList<string> skyPaths, int startIndex)
        {
            if (showing == null || arriving == null)
            {
                Debug.LogError($"{nameof(SkyView)}: both sky renderers are needed for a dissolve.", this);
                return;
            }

            _cache = cache;
            _showingPath = null;
            _arrivingPath = null;

            if (cache == null || skyPaths == null || skyPaths.Count == 0)
            {
                cache?.Clear();
                _cycle = null;
                _skyPaths = null;
                arriving.gameObject.SetActive(false);
                return;
            }

            _skyPaths = skyPaths;
            _cycle = new SkyCycle(skyPaths.Count, holdSeconds, fadeSeconds, startIndex);

            arriving.gameObject.SetActive(true);
            Paint();
        }

        /// <summary>Moves the sky on by one step of the runner's clock.</summary>
        public void Tick(float deltaSeconds)
        {
            if (_cycle == null) return;

            _cycle.Tick(deltaSeconds);
            Paint();
        }

        /// <summary>Gives back whatever is held, for a level that is over.</summary>
        public void ReleaseAll()
        {
            // Nothing may be drawing an asset as it is freed.
            if (showing != null) showing.sprite = null;
            if (arriving != null) arriving.sprite = null;

            _cache?.Clear();
            _showingPath = null;
            _arrivingPath = null;
            _cycle = null;
            _skyPaths = null;
            _cache = null;
        }

        void Paint()
        {
            var wantedShowing = _skyPaths[_cycle.Showing];

            // The next sky is only wanted once it is nearly due: through the rest of the hold a run keeps one
            // sky, not two. Fetched early enough that it has arrived before the first frame it is drawn on,
            // so nothing is ever loaded at the moment it appears.
            var due = _cycle.Blend > 0f || _cycle.SecondsUntilDissolve <= prefetchSeconds;
            var wantedArriving = due ? _skyPaths[_cycle.Arriving] : null;

            if (wantedShowing != _showingPath || wantedArriving != _arrivingPath)
            {
                _wanted.Clear();
                _wanted.Add(wantedShowing);
                if (wantedArriving != null && wantedArriving != wantedShowing) _wanted.Add(wantedArriving);

                // Exactly these: the sky the ring has finished leaving is freed by the same call, and only
                // once nothing is drawing it.
                _cache.Require<Sprite>(_wanted);

                _showingPath = wantedShowing;
                _arrivingPath = wantedArriving;
            }

            showing.sprite = _cache.Get<Sprite>(wantedShowing);

            // While it is only being fetched ahead of time, take it if it has arrived and carry on if it has
            // not. Once the dissolve is actually running, wait for it: a fade to nothing would be worse than
            // the stall, and the prefetch means it has already arrived unless the disc is very slow indeed.
            if (wantedArriving == null) arriving.sprite = null;
            else if (_cycle.Blend > 0f) arriving.sprite = _cache.Get<Sprite>(wantedArriving);
            else arriving.sprite = _cache.TryGet<Sprite>(wantedArriving);

            var colour = arriving.color;
            colour.a = arriving.sprite != null ? _cycle.Blend : 0f;
            arriving.color = colour;
        }
    }
}
