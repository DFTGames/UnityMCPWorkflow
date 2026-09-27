using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YASS.UI
{
    /// <summary>
    /// The real browser, through the WebGL plugin. A <see cref="MonoBehaviour"/> because the plugin answers
    /// by <c>SendMessage</c>, which needs a named object in the scene to answer to.
    /// </summary>
    /// <remarks>
    /// In its own file because it is a MonoBehaviour, and a MonoBehaviour sharing another script's file
    /// serialises with <c>m_Script: {fileID: 0}</c>: the component exists and nothing can reference it.
    /// </remarks>
    public sealed class ItchBrowser : MonoBehaviour, IItchBrowser
    {
        /// <summary>The object the plugin sends its answer to. Named, because SendMessage takes a name.</summary>
        const string ObjectName = "YASS Itch Sign-In";

        static ItchBrowser _shared;

        TaskCompletionSource<string> _waiting;

        /// <summary>
        /// Created on demand and kept across scenes, because a sign-in outlives the screen that started it:
        /// the player may be gone for minutes, and a scene load in the meantime must not take away the only
        /// object the plugin knows how to answer to.
        /// </summary>
        public static ItchBrowser Shared
        {
            get
            {
                if (_shared != null) return _shared;

                var host = new GameObject(ObjectName);
                DontDestroyOnLoad(host);
                _shared = host.AddComponent<ItchBrowser>();

                return _shared;
            }
        }

        /// <summary>
        /// Domain reloading is off, so the static above outlives a play session without this. Named
        /// <c>ResetStatics</c> like every other one in this project, and not <c>Reset</c>, which is a
        /// MonoBehaviour message name that Unity refuses to dispatch when it is declared static.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _shared = null;

        public bool CanOpenWindows
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            get { return YassItchCanOpenWindows() != 0; }
#else
            get { return false; }
#endif
        }

        public async Task<string> OpenSignIn(string page, string callbackOrigin, float timeoutSeconds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // One at a time. The plugin refuses a second window anyway, and answering the first caller with
            // the second's result would sign somebody in as whoever happened to finish last.
            if (_waiting != null && !_waiting.Task.IsCompleted) return "busy";

            // Held in a local as well as the field. OnItchAnswer clears the field before completing the
            // task, and whether that completion resumes this method inline or on a later update is the
            // runtime's business, so reading the field again afterwards can find a null that was there all
            // along on the *success* path: a sign-in that worked, reported as a network failure.
            var pending = new TaskCompletionSource<string>();
            _waiting = pending;

            YassItchOpenSignIn(page, ObjectName, callbackOrigin);

            using (var timer = new CancellationTokenSource())
            {
                // Cancelled when the answer wins, so a finished sign-in does not leave a five minute timer
                // behind it. UgsCalls.WithDeadline is not used here because there is nothing to throw: the
                // timeout is an ordinary answer rather than a fault.
                var deadline = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), timer.Token);
                var first = await Task.WhenAny(pending.Task, deadline);

                timer.Cancel();

                if (first != pending.Task)
                {
                    // Nobody is listening any more, so the window must not be left sitting over the game.
                    YassItchCancelSignIn();
                    _waiting = null;
                    return "cancelled";
                }
            }

            return await pending.Task;
#else
            // Every other platform signs in with Unity Player Accounts and never reaches this. Answering
            // rather than throwing keeps an editor playtest of the web flow harmless.
            await Task.CompletedTask;
            return "cancelled";
#endif
        }

        /// <summary>
        /// Called by the plugin. The only way an answer arrives, and passed on exactly as it came: "token
        /// &lt;fragment&gt;" on success, or a word such as "cancelled", "blocked" or "busy". Reading those
        /// words is <c>ItchOAuth.ReadAnswer</c>'s job, in Core, where it can be tested. This method flattened
        /// them all to the empty string for one revision, which told a player whose browser had blocked the
        /// window that they had simply not finished.
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void OnItchAnswer(string answer)
        {
            var pending = _waiting;
            _waiting = null;

            pending?.TrySetResult(answer ?? string.Empty);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void YassItchOpenSignIn(string url, string listener, string callbackOrigin);

        [DllImport("__Internal")]
        static extern void YassItchCancelSignIn();

        [DllImport("__Internal")]
        static extern int YassItchCanOpenWindows();
#endif
    }
}
