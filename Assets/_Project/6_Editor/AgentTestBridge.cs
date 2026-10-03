using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Card.Editor.AgentBridge
{
    [Serializable]
    public sealed class TestRunSummary
    {
        public string generatedAtUtc;
        public string mode;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public double durationSeconds;
        public string result;
        public List<string> failures = new List<string>();
    }

    /// <summary>
    /// Editor-only test runner used by the AI workflow: runs EditMode/PlayMode tests
    /// from the menu and writes Logs/agent-tests.json so the agent can read the result
    /// without a GUI. Menu: Tools > Card > Agent > Run * Tests.
    /// </summary>
    internal static class AgentTestBridge
    {
        private const string TestFileName = "agent-tests.json";

        private static TestRunnerApi s_api;
        private static TestCallbacks s_callbacks;

        [MenuItem("Tools/Card/Agent/Run EditMode Tests")]
        private static void MenuRunEditModeTests()
        {
            RunTests(TestMode.EditMode);
        }

        [MenuItem("Tools/Card/Agent/Run PlayMode Tests")]
        private static void MenuRunPlayModeTests()
        {
            RunTests(TestMode.PlayMode);
        }

        private static void RunTests(TestMode mode)
        {
            if (s_api == null) s_api = ScriptableObject.CreateInstance<TestRunnerApi>();

            if (s_callbacks != null)
            {
                s_api.UnregisterCallbacks(s_callbacks);
                s_callbacks = null;
            }

            s_callbacks = new TestCallbacks(mode.ToString());
            s_api.RegisterCallbacks(s_callbacks);
            s_api.Execute(new ExecutionSettings(new Filter { testMode = mode }));

            AgentConsoleBridge.SetTestSummary(mode + " run started");
        }

        private static void ReleaseCallbacks()
        {
            EditorApplication.delayCall += () =>
            {
                if (s_api != null && s_callbacks != null)
                {
                    s_api.UnregisterCallbacks(s_callbacks);
                    s_callbacks = null;
                }
            };
        }

        private sealed class TestCallbacks : ICallbacks
        {
            private readonly TestRunSummary _summary = new TestRunSummary();

            public TestCallbacks(string mode)
            {
                _summary.mode = mode;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed && !result.HasChildren)
                {
                    _summary.failures.Add(result.Name + " :: " + result.Message);
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _summary.generatedAtUtc = DateTime.UtcNow.ToString("o");
                _summary.passed = result.PassCount;
                _summary.failed = result.FailCount;
                _summary.skipped = result.SkipCount;
                _summary.inconclusive = result.InconclusiveCount;
                _summary.durationSeconds = result.Duration;
                _summary.result = result.TestStatus.ToString();

                AgentConsoleBridge.WriteJson(TestFileName, _summary);
                AgentConsoleBridge.SetTestSummary(string.Format(
                    "{0}: passed={1} failed={2} skipped={3}",
                    _summary.mode, _summary.passed, _summary.failed, _summary.skipped));

                Console.WriteLine("[AgentTestBridge] results written to Logs/" + TestFileName);
                ReleaseCallbacks();
            }
        }
    }
}
