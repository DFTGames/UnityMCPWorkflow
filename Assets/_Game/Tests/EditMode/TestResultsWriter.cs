using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YASS.Tests
{
    /// <summary>
    /// Test infrastructure, not a test: writes a summary of every Test Runner run to Temp/YASS-TestResults-*.txt.
    /// PlayMode runs are asynchronous and survive a domain reload, so this is how automation (MCP) reads their
    /// results. Registered on every editor load.
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
                File.WriteAllText(Path.Combine("Temp", $"YASS-TestResults-{mode}.txt"), writer.ToString());
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
