using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YASS.Tests
{
    /// <summary>
    /// Test infrastructure, not a test: reports a summary of every Test Runner run, to
    /// Temp/YASS-TestResults-*.txt and to the console. PlayMode runs are asynchronous and survive a domain
    /// reload, so the file is how automation (MCP) reads their results; the console line is so the run can be
    /// read in the Editor and in the Unity log without going to look for a file. Both come from the same text,
    /// so they cannot disagree. Registered on every editor load.
    /// </summary>
    [InitializeOnLoad]
    static class TestResultsWriter
    {
        static TestResultsWriter()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                using var writer = new StringWriter();
                writer.WriteLine($"pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount} " +
                                 $"inconclusive={result.InconclusiveCount} duration={result.Duration:F1}s");
                WriteFailures(result, writer);

                var mode = result.Test.TestMode == TestMode.PlayMode ? "PlayMode" : "EditMode";
                var summary = writer.ToString();
                File.WriteAllText(Path.Combine("Temp", $"YASS-TestResults-{mode}.txt"), summary);

                // An error rather than a log when anything failed, so a bad run cannot be scrolled past and
                // shows up in any check of the console.
                var message = $"[YASS tests] {mode}{System.Environment.NewLine}{summary.TrimEnd()}";
                if (result.FailCount > 0 || result.InconclusiveCount > 0) Debug.LogError(message);
                else Debug.Log(message);
            }

            static void WriteFailures(ITestResultAdaptor result, TextWriter writer)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                        writer.WriteLine($"FAIL {result.FullName}: {result.Message}\n{result.StackTrace}");
                    return;
                }

                foreach (var child in result.Children) WriteFailures(child, writer);
            }
        }
    }
}
