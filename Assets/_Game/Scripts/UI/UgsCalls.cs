using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The handful of things every way of signing in does identically: waiting on the service without waiting
    /// for ever, logging a failure without logging anything private, and holding the pilot name.
    /// </summary>
    /// <remarks>
    /// Shared rather than copied because the pilot name in particular has already been got wrong once in a
    /// way no test caught: asking for it with <c>autoGenerate: false</c> returned nothing, so the game showed
    /// a name of its own while the boards printed the service's. Two copies of that lesson is one copy too
    /// many, and the second would have been in the build nobody plays in the editor.
    /// </remarks>
    static class UgsCalls
    {
        /// <summary>
        /// How long to wait for the service. A captive portal or a black-holed connection does not refuse,
        /// it simply never answers, and no screen may wait for ever.
        /// </summary>
        public const float TimeoutSeconds = 15f;

        /// <summary>
        /// Waits for the service, but not for ever. The timer is cancelled when the work wins, so a call does
        /// not leave a timer behind it, and abandoned work is still observed, so a failure arriving late does
        /// not surface as an unobserved task exception.
        /// </summary>
        public static async Task<T> WithDeadline<T>(Task<T> work, float seconds)
        {
            await WithDeadline((Task)work, seconds);
            return work.Result;
        }

        public static async Task WithDeadline(Task work, float seconds)
        {
            using (var timer = new CancellationTokenSource())
            {
                var waiting = Task.Delay(TimeSpan.FromSeconds(seconds), timer.Token);
                if (await Task.WhenAny(work, waiting) != work)
                {
                    Observe(work);
                    throw new TimeoutException("the service did not answer");
                }

                timer.Cancel();
                await work;
            }
        }

        public static void Observe(Task work) =>
            work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

        /// <summary>
        /// The code, never the message. The service writes its messages for developers and they can carry
        /// details of the request; the code is what identifies the fault in a log somebody may send on.
        /// </summary>
        public static void Log(string who, string what, Exception problem)
        {
            var code = problem is RequestFailedException failed
                ? failed.ErrorCode.ToString()
                : problem.GetType().Name;

            Debug.LogWarning($"{who}: could not {what} (code {code}).");
        }

        public static bool ServicesReady
        {
            get
            {
                try { return UnityServices.State == ServicesInitializationState.Initialized; }
                catch (Exception) { return false; }
            }
        }
    }
}
