using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Card.Editor.AgentBridge
{
    [Serializable]
    public sealed class ConsoleEntry
    {
        public string time = string.Empty;
        public string type = string.Empty;
        public string message = string.Empty;
        public string stack = string.Empty;
    }

    [Serializable]
    public sealed class CompilerEntry
    {
        public string assembly = string.Empty;
        public string severity = string.Empty;
        public string file = string.Empty;
        public int line;
        public string message = string.Empty;
    }

    [Serializable]
    public sealed class AgentStatus
    {
        public string generatedAtUtc = string.Empty;
        public string unityVersion = string.Empty;
        public string projectPath = string.Empty;
        public bool isCompiling;
        public bool hasCompilerErrors;
        public int compilerErrorCount;
        public int compilerWarningCount;
        public int consoleErrorCount;
        public int consoleWarningCount;
        public string lastTestSummary = string.Empty;
        public List<CompilerEntry> compilerDiagnostics = new List<CompilerEntry>();
        public List<ConsoleEntry> recentConsoleErrors = new List<ConsoleEntry>();
    }

    /// <summary>
    /// Editor-only bridge that lets a coding agent read Unity state without a GUI.
    /// Writes Logs/agent-status.json (compile result, compiler diagnostics, console errors).
    /// Test runs are handled by <see cref="AgentTestBridge"/>.
    /// See Docs/06 (AI collaboration) for the full MCP upgrade path.
    /// </summary>
    [InitializeOnLoad]
    public static class AgentConsoleBridge
    {
        internal const string StatusFileName = "agent-status.json";

        private const int MaxConsoleEntries = 200;
        private const int MaxCompilerEntries = 200;
        private const double FlushIntervalSeconds = 1.0;

        private static readonly List<ConsoleEntry> s_console = new List<ConsoleEntry>();
        private static readonly List<CompilerEntry> s_compiler = new List<CompilerEntry>();

        private static bool s_dirty = true;
        private static double s_nextFlush;
        private static string s_lastTestSummary = string.Empty;

        static AgentConsoleBridge()
        {
            Application.logMessageReceived += OnLogMessage;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting += Flush;
        }

        // ---------------------------------------------------------------- paths --

        internal static string ProjectRoot
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Application.dataPath;
            }
        }

        internal static string LogDirectory
        {
            get { return Path.Combine(ProjectRoot, "Logs"); }
        }

        internal static void WriteJson(string fileName, object payload)
        {
            string directory = LogDirectory;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(payload, true));
        }

        internal static void SetTestSummary(string summary)
        {
            s_lastTestSummary = summary;
            s_dirty = true;
        }

        // ------------------------------------------------------------- capture ---

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log) return;

            s_console.Add(new ConsoleEntry
            {
                time = DateTime.UtcNow.ToString("o"),
                type = type.ToString(),
                message = condition,
                stack = FirstLine(stackTrace)
            });

            Trim(s_console, MaxConsoleEntries);
            s_dirty = true;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (messages == null) return;

            string assembly = Path.GetFileNameWithoutExtension(assemblyPath);
            foreach (CompilerMessage message in messages)
            {
                if (message.type != CompilerMessageType.Error && message.type != CompilerMessageType.Warning)
                    continue;

                s_compiler.Add(new CompilerEntry
                {
                    assembly = assembly,
                    severity = message.type.ToString().ToLowerInvariant(),
                    file = message.file,
                    line = message.line,
                    message = message.message
                });
            }

            Trim(s_compiler, MaxCompilerEntries);
            s_dirty = true;
        }

        private static void OnEditorUpdate()
        {
            if (!s_dirty) return;
            if (EditorApplication.timeSinceStartup < s_nextFlush) return;

            s_nextFlush = EditorApplication.timeSinceStartup + FlushIntervalSeconds;
            Flush();
        }

        // --------------------------------------------------------------- flush ---

        private static void Flush()
        {
            try
            {
                AgentStatus status = new AgentStatus
                {
                    generatedAtUtc = DateTime.UtcNow.ToString("o"),
                    unityVersion = Application.unityVersion,
                    projectPath = ProjectRoot,
                    isCompiling = EditorApplication.isCompiling,
                    lastTestSummary = s_lastTestSummary
                };

                foreach (CompilerEntry entry in s_compiler)
                {
                    if (entry.severity == "error") status.compilerErrorCount++;
                    else status.compilerWarningCount++;
                }

                status.hasCompilerErrors = status.compilerErrorCount > 0;
                status.compilerDiagnostics.AddRange(s_compiler);

                foreach (ConsoleEntry entry in s_console)
                {
                    if (entry.type == "Warning") status.consoleWarningCount++;
                    else status.consoleErrorCount++;
                }

                status.recentConsoleErrors.AddRange(s_console);

                WriteJson(StatusFileName, status);
                s_dirty = false;
            }
            catch (Exception ex)
            {
                // Never let the bridge break the editor.
                Console.WriteLine("[AgentConsoleBridge] flush failed: " + ex.Message);
                s_dirty = false;
            }
        }

        // ----------------------------------------------------------- menu items --

        [MenuItem("Tools/Card/Agent/Dump Status Now")]
        private static void MenuDumpStatus()
        {
            Flush();
            Console.WriteLine("[AgentConsoleBridge] status written to Logs/" + StatusFileName);
        }

        [MenuItem("Tools/Card/Agent/Clear Captured Logs")]
        private static void MenuClear()
        {
            s_console.Clear();
            s_compiler.Clear();
            s_lastTestSummary = string.Empty;
            s_dirty = true;
            Console.WriteLine("[AgentConsoleBridge] captured logs cleared");
        }

        // -------------------------------------------------------------- helpers --

        private static void Trim<T>(List<T> list, int max)
        {
            int overflow = list.Count - max;
            if (overflow > 0) list.RemoveRange(0, overflow);
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int index = text.IndexOf('\n');
            return index < 0 ? text : text.Substring(0, index).TrimEnd('\r');
        }
    }
}
