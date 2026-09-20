using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YASS.Tests
{
    /// <summary>
    /// Test infrastructure, not a test: menu entries that start a test run, so automation (MCP) can trigger one
    /// without <c>Unity_RunCommand</c>, which is not always available. Results land in Temp/YASS-TestResults-*.txt
    /// (see <see cref="TestResultsWriter"/>).
    /// </summary>
    static class TestRunnerMenu
    {
        [MenuItem("Tools/YASS/Run EditMode Tests")]
        static void RunEditMode() => Run(TestMode.EditMode, "YASS.Game.Tests.EditMode");

        [MenuItem("Tools/YASS/Run PlayMode Tests")]
        static void RunPlayMode() => Run(TestMode.PlayMode, "YASS.Game.Tests.PlayMode");

        static void Run(TestMode mode, string assemblyName)
        {
            // Play mode does not advance while the Editor is unfocused unless this is on, and an automated run has
            // nobody to focus the window: without it a PlayMode run started over MCP simply hangs.
            if (mode == TestMode.PlayMode && !PlayerSettings.runInBackground) PlayerSettings.runInBackground = true;

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = mode,
                assemblyNames = new[] { assemblyName }
            }));
        }
    }
}
