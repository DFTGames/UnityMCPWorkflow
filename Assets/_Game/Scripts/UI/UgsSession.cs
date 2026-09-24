using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using YASS.Core;

namespace YASS.UI
{
    /// <summary>
    /// The one place Unity Gaming Services is started, and the record of which environment it was started
    /// against. <see cref="UgsEnvironment"/> holds the rule; this holds the fact.
    /// </summary>
    /// <remarks>
    /// The services are a process-wide singleton: whoever starts them first chooses the environment for the
    /// whole session, and there is no public way to read that choice back. So the choice is made here, once,
    /// and everything that talks to the service asks <see cref="MayTalk"/> first. A session somebody else
    /// started is refused rather than trusted, which is what stops a test run reaching the live boards.
    /// </remarks>
    public static class UgsSession
    {
        /// <summary>What the services were started against, or null if this session did not start them.</summary>
        public static string StartedWith { get; private set; }

        /// <summary>The only environment this session may touch: test in the editor, production in a build.</summary>
        public static string Wanted => UgsEnvironment.For(Application.isEditor);

        /// <summary>
        /// Domain reloading is off, so this would otherwise survive into the next play session and claim a
        /// session that has already gone. A stale "we are on test" is the exact belief this class exists to
        /// prevent anybody holding.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => StartedWith = null;

        /// <summary>Starts the services against <see cref="Wanted"/>, once. False when they cannot be started.</summary>
        public static async Task<bool> Start(Func<Task, Task> withDeadline)
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return StartedWith != null;

            var wanted = Wanted;
            var options = new InitializationOptions();
            options.SetEnvironmentName(wanted);

            await withDeadline(UnityServices.InitializeAsync(options));

            if (UnityServices.State != ServicesInitializationState.Initialized) return false;

            StartedWith = wanted;
            return true;
        }

        /// <summary>
        /// Test seam: says that the caller started the services itself, against this environment. Only the
        /// live leaderboard fixture uses it, which signs in as the test user on the test environment before
        /// any game code has run.
        /// </summary>
        internal static void Declare(string environment) => StartedWith = environment;

        /// <summary>Whether this session may talk to the service, and why not when it may not.</summary>
        public static bool MayTalk(out string problem) =>
            UgsEnvironment.MayTalkToTheService(StartedWith, Application.isEditor, out problem);
    }
}
