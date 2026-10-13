using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WormRush.EditorTools
{
    /// <summary>
    /// One command for the size-optimised Web build: applies every size-related player setting through the
    /// API (so nothing depends on hand-edited ProjectSettings or on per-user build settings), builds into
    /// Build/web, prints the size per file kind, fails when the total is over the 5 MB network budget and
    /// writes the measured number into README.md between the build-size markers.
    ///   Unity.exe -batchmode -quit -projectPath . -logFile web-build.log -executeMethod WormRush.EditorTools.WebBuild.Run
    ///   [-buildPath Build/web] [-noReadme]
    /// Needs the WebGL Build Support module installed for this editor (Unity Hub: Installs, Add modules).
    /// </summary>
    public static class WebBuild
    {
        const long BudgetBytes = 5L * 1024 * 1024;
        const double BytesPerMb = 1024.0 * 1024.0;
        const string DefaultOutput = "Build/web";
        const string BuildRoot = "Build";
        const string ReadmeFile = "README.md";
        const string SizeStart = "<!-- build-size:start -->";
        const string SizeEnd = "<!-- build-size:end -->";
        const string FallbackScene = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Worm Rush/Build Web (5 MB budget)")]
        public static void Run()
        {
            string output = ArgValue("-buildPath") ?? DefaultOutput;
            bool writeReadme = !HasArg("-noReadme");
            bool ok = false;
            try
            {
                ok = Build(output, writeReadme);
            }
            catch (Exception e)
            {
                Debug.LogError("WebBuild failed: " + e);
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string output, bool writeReadme)
        {
            ApplySizeSettings();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputFull = Path.GetFullPath(Path.Combine(projectRoot, output));
            CleanOutput(projectRoot, outputFull);

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { FallbackScene };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputFull,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("WebBuild: build " + report.summary.result + ", " + report.summary.totalErrors + " error(s). See the log.");
                return false;
            }

            List<KeyValuePair<string, long>> kinds = MeasureByKind(outputFull);
            long total = kinds.Sum(k => k.Value);
            var table = new StringBuilder("WebBuild size (Brotli, as served):\n");
            foreach (KeyValuePair<string, long> kind in kinds) table.AppendLine("  " + kind.Key.PadRight(10) + Mb(kind.Value));
            table.AppendLine("  " + "TOTAL".PadRight(10) + Mb(total) + "  (budget " + Mb(BudgetBytes) + ")");
            Debug.Log(table.ToString());

            if (total > BudgetBytes)
            {
                Debug.LogError("WebBuild: " + Mb(total) + " is over the " + Mb(BudgetBytes) + " budget. " +
                               "The 'Build Report' block in the editor log lists the biggest assets and the code size.");
                return false;
            }

            if (writeReadme) WriteReadme(projectRoot, kinds, total);
            return true;
        }

        static void ApplySizeSettings()
        {
            NamedBuildTarget web = NamedBuildTarget.WebGL;
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            // The Unity loader unpacks Brotli itself when the server does not send Content-Encoding: br.
            // Cloudflare does not for .br files, and the transfer stays as small as the compressed file.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = false;
            EditorUserBuildSettings.development = false;
            SetCodeOptimization();
        }

        // "IL2CPP Code Optimization" for Web lives in per-user build settings (not in the repo), and the type
        // sits in the WebGL module assembly, so it is set by reflection: a missing module is a warning here
        // and a clear build error afterwards, not a compile error that blocks the whole project.
        static void SetCodeOptimization()
        {
            Type type = Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
            PropertyInfo prop = type == null ? null : type.GetProperty("codeOptimization", BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                Debug.LogWarning("WebBuild: UserBuildSettings.codeOptimization not found (WebGL module missing?); code optimisation left as is.");
                return;
            }
            foreach (string name in new[] { "DiskSizeLTO", "DiskSize" })
            {
                if (!Enum.IsDefined(prop.PropertyType, name)) continue;
                prop.SetValue(null, Enum.Parse(prop.PropertyType, name));
                return;
            }
            Debug.LogWarning("WebBuild: no DiskSize value in " + prop.PropertyType.Name + "; code optimisation left as is.");
        }

        // Stale files from an older build would inflate the measured size; only ever delete inside <project>/Build.
        static void CleanOutput(string projectRoot, string outputFull)
        {
            string buildRoot = Path.GetFullPath(Path.Combine(projectRoot, BuildRoot)) + Path.DirectorySeparatorChar;
            if (!Directory.Exists(outputFull)) return;
            if (outputFull.StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(outputFull, true);
                return;
            }
            Debug.LogWarning("WebBuild: " + outputFull + " is outside " + BuildRoot + "/, not cleaned; old files there will count towards the size.");
        }

        static List<KeyValuePair<string, long>> MeasureByKind(string dir)
        {
            var sizes = new SortedDictionary<string, long>();
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string kind = KindOf(Path.GetFileName(file).ToLowerInvariant());
                long size = new FileInfo(file).Length;
                sizes[kind] = (sizes.TryGetValue(kind, out long sum) ? sum : 0) + size;
            }
            return sizes.OrderByDescending(k => k.Value).ToList();
        }

        static string KindOf(string name)
        {
            if (name.Contains(".wasm")) return "wasm";
            if (name.Contains(".data")) return "data";
            if (name.Contains(".framework.js")) return "framework";
            if (name.Contains(".loader.js")) return "loader";
            if (name.EndsWith(".html", StringComparison.Ordinal)) return "html";
            return "other";
        }

        static void WriteReadme(string projectRoot, List<KeyValuePair<string, long>> kinds, long total)
        {
            string path = Path.Combine(projectRoot, ReadmeFile);
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            int start = text.IndexOf(SizeStart, StringComparison.Ordinal);
            int end = text.IndexOf(SizeEnd, StringComparison.Ordinal);
            if (start < 0 || end < start)
            {
                Debug.LogWarning("WebBuild: build-size markers not found in " + ReadmeFile + "; size not written.");
                return;
            }
            string parts = string.Join(", ", kinds.Select(k => k.Key + " " + Mb(k.Value)));
            string line = "**" + Mb(total) + "** total, Brotli (" + parts + ")";
            text = text.Substring(0, start + SizeStart.Length) + "\n" + line + "\n" + text.Substring(end);
            File.WriteAllText(path, text);
            Debug.Log("WebBuild: wrote the size into " + ReadmeFile);
        }

        static string Mb(long bytes)
        {
            return (bytes / BytesPerMb).ToString("0.00", CultureInfo.InvariantCulture) + " MB";
        }

        static bool HasArg(string name)
        {
            return Environment.GetCommandLineArgs().Contains(name);
        }

        static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
