using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.WebGL;
using UnityEngine;

namespace WormRush.EditorTools
{
    /// <summary>
    /// Release Web build for static hosting (Cloudflare Workers/Pages). One entry point for the menu and batch mode:
    ///   Unity -batchmode -nographics -quit -projectPath . -logFile build.log
    ///         -executeMethod WormRush.EditorTools.WebBuild.Run [-buildPath Builds/WebGL]
    ///         [-webExceptions none|explicit|full] [-webSymbols off|embedded]
    /// The last two are for diagnosing a crash in the browser (see <see cref="ExceptionsFromArgs"/>).
    ///
    /// What makes it small (the project is on the built-in render pipeline, no URP package: URP's core forces the
    /// Physics and Terrain modules and a large amount of managed code into the player):
    ///   - Brotli + "decompression fallback": the raw .wasm is over the 25 MiB per-file limit of Cloudflare, and a
    ///     static host does not send Content-Encoding for Unity's .br files, so the loader unpacks them in JS.
    ///   - IL2CPP "Optimize size", managed stripping High, wasm "Disk size with LTO", no exception support.
    /// The build script sets these through PlayerSettings and puts every value back, so ProjectSettings is unchanged.
    /// </summary>
    public static class WebBuild
    {
        const string DefaultOutput = "Builds/WebGL";
        const string BuildsFolder = "Builds";
        const string ScenePath = "Assets/Scenes/SampleScene.unity";   // one camera; the game bootstraps from code
        const string TemplateName = "PROJECT:WormRush";   // Assets/WebGLTemplates/WormRush
        const string CircleShaderPath = "Assets/_Project/View/Shaders/Circle.shader";
        const string InstancedMaterialPath = "Assets/_Project/Resources/CircleInstanced.mat";
        static readonly string[] SettingsFiles =
        {
            "ProjectSettings/ProjectSettings.asset",
        };
        const long PagesMaxFileBytes = 25L * 1024 * 1024; // Cloudflare: 25 MiB per file
        const float BytesPerMegabyte = 1024f * 1024f;

        [MenuItem("Worm Rush/Build Web (Cloudflare Pages)")]
        public static void BuildFromMenu()
        {
            bool ok = Build(DefaultOutput, out string fullPath);
            if (ok)
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Web build", "The build failed. See the Console.", "OK");
            }
        }

        public static void Run()
        {
            bool ok = Build(Arg("-buildPath", DefaultOutput), out _);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string outputPath, out string fullOutput)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            fullOutput = Path.GetFullPath(Path.Combine(projectRoot, outputPath));
            string buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, BuildsFolder)) + Path.DirectorySeparatorChar;
            if (!fullOutput.StartsWith(buildsRoot, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[WebBuild] The output must be a folder inside <project>/" + BuildsFolder + ": " + fullOutput);
                return false;
            }

            EnsureInstancedMaterial();

            // Stale files from an earlier build (other compression, other names) would end up in the upload.
            if (Directory.Exists(fullOutput))
            {
                Directory.Delete(fullOutput, true);
            }

            var named = NamedBuildTarget.WebGL;
            var compression = PlayerSettings.WebGL.compressionFormat;
            string template = PlayerSettings.WebGL.template;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            var exceptions = PlayerSettings.WebGL.exceptionSupport;
            var arithmetic = PlayerSettings.WebGL.wasmArithmeticExceptions;
            var symbols = PlayerSettings.WebGL.debugSymbolMode;
            var stripping = PlayerSettings.GetManagedStrippingLevel(named);
            var codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(named);
            var optimization = UserBuildSettings.codeOptimization;
            bool splash = PlayerSettings.SplashScreen.show;
            bool unityLogo = PlayerSettings.SplashScreen.showUnityLogo;
            Dictionary<string, byte[]> settingsOnDisk = BackUpSettings();
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.template = TemplateName;
                PlayerSettings.WebGL.exceptionSupport = ExceptionsFromArgs();
                PlayerSettings.WebGL.wasmArithmeticExceptions = WebGLWasmArithmeticExceptions.Ignore;
                PlayerSettings.WebGL.debugSymbolMode = Arg("-webSymbols", "off") == "embedded"
                    ? WebGLDebugSymbolMode.Embedded
                    : WebGLDebugSymbolMode.Off;
                PlayerSettings.SetManagedStrippingLevel(named, ManagedStrippingLevel.High);
                PlayerSettings.SetIl2CppCodeGeneration(named, Il2CppCodeGeneration.OptimizeSize);
                UserBuildSettings.codeOptimization = WasmCodeOptimization.DiskSizeLTO;
                // Both: with only `show` off the "Made with Unity" logo texture (2.7 MB raw, about 1 MB packed) is
                // still written into the data file.
                PlayerSettings.SplashScreen.show = false;
                PlayerSettings.SplashScreen.showUnityLogo = false;

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = fullOutput,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.DetailedBuildReport,
                };
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.template = template;
                PlayerSettings.WebGL.exceptionSupport = exceptions;
                PlayerSettings.WebGL.wasmArithmeticExceptions = arithmetic;
                PlayerSettings.WebGL.debugSymbolMode = symbols;
                PlayerSettings.SetManagedStrippingLevel(named, stripping);
                PlayerSettings.SetIl2CppCodeGeneration(named, codeGeneration);
                UserBuildSettings.codeOptimization = optimization;
                PlayerSettings.SplashScreen.show = splash;
                PlayerSettings.SplashScreen.showUnityLogo = unityLogo;
                RestoreSettingsOnDisk(settingsOnDisk);
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[WebBuild] " + summary.result + ": " + summary.totalErrors + " error(s). Output: " + fullOutput);
                return false;
            }

            LogIncludedModules(report);

            long total = 0;
            foreach (string file in Directory.GetFiles(fullOutput, "*", SearchOption.AllDirectories).OrderBy(f => f))
            {
                long size = new FileInfo(file).Length;
                total += size;
                string rel = file.Substring(fullOutput.Length).TrimStart(Path.DirectorySeparatorChar, '/');
                string flag = size > PagesMaxFileBytes ? "   <-- OVER the 25 MiB Cloudflare limit" : "";
                Debug.Log("[WebBuild]   " + (size / BytesPerMegabyte).ToString("0.00") + " MB  " + rel + flag);
            }
            Debug.Log("[WebBuild] OK: " + (total / BytesPerMegabyte).ToString("0.00") + " MB in " + fullOutput);
            return true;
        }

        /// <summary>
        /// Unity writes the settings assets to disk during a build with the build's values, and in batch mode the
        /// restored in-memory values are never flushed. The files are put back byte for byte, so a build leaves
        /// ProjectSettings exactly as it found it.
        /// </summary>
        static Dictionary<string, byte[]> BackUpSettings()
        {
            var saved = new Dictionary<string, byte[]>();
            foreach (string path in SettingsFiles)
            {
                if (File.Exists(path))
                {
                    saved[path] = File.ReadAllBytes(path);
                }
            }
            return saved;
        }

        static void RestoreSettingsOnDisk(Dictionary<string, byte[]> saved)
        {
            AssetDatabase.SaveAssets();
            foreach (KeyValuePair<string, byte[]> file in saved)
            {
                if (!File.ReadAllBytes(file.Key).SequenceEqual(file.Value))
                {
                    File.WriteAllBytes(file.Key, file.Value);
                }
            }
        }

        static void LogIncludedModules(BuildReport report)
        {
            StrippingInfo info = report.strippingInfo;
            if (info == null)
            {
                return;
            }
            foreach (string module in info.includedModules.OrderBy(m => m))
            {
                Debug.Log("[WebBuild] module " + module + "  <-  " + string.Join(", ", info.GetReasonsForIncluding(module).Take(3)));
            }
            // Why a module is there, a few steps back: this is what shows which package or setting keeps it alive.
            foreach (string module in info.includedModules.OrderBy(m => m))
            {
                var seen = new HashSet<string> { module };
                var line = new List<string> { module };
                string current = module;
                for (int depth = 0; depth < 6; depth++)
                {
                    string next = null;
                    foreach (string reason in info.GetReasonsForIncluding(current))
                    {
                        string entity = reason.StartsWith("Required by ") ? reason.Substring("Required by ".Length) : reason;
                        if (seen.Add(entity))
                        {
                            next = entity;
                            break;
                        }
                    }
                    if (next == null) break;
                    line.Add(next);
                    current = next;
                }
                Debug.Log("[WebBuild] chain " + string.Join("  <-  ", line));
            }
        }

        /// <summary>
        /// None by default (the smallest code). A fatal "Uncaught undefined" in the browser hides the managed
        /// exception behind it; build once with -webExceptions full -webSymbols embedded to get its stack.
        /// </summary>
        static WebGLExceptionSupport ExceptionsFromArgs()
        {
            switch (Arg("-webExceptions", "none"))
            {
                case "full": return WebGLExceptionSupport.FullWithStacktrace;
                case "explicit": return WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                default: return WebGLExceptionSupport.None;
            }
        }

        /// <summary>
        /// WorldView makes its material at run time, and a build strips every shader variant that no material asset
        /// uses: the INSTANCING_ON variant of WormRush/Circle goes, and the instanced draw then shows nothing (the
        /// editor never shows it, it compiles variants on demand). A material asset with instancing on, kept in
        /// Resources, keeps the variant. Created through the API, not hand-written YAML.
        /// </summary>
        static void EnsureInstancedMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(InstancedMaterialPath);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(CircleShaderPath);
            if (shader == null)
            {
                Debug.LogError("[WebBuild] Shader not found: " + CircleShaderPath);
                return;
            }
            if (existing != null && existing.shader == shader && existing.enableInstancing)
            {
                return;
            }

            if (existing == null)
            {
                existing = new Material(shader);
                AssetDatabase.CreateAsset(existing, InstancedMaterialPath);
            }
            existing.shader = shader;
            existing.enableInstancing = true;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Debug.Log("[WebBuild] Wrote " + InstancedMaterialPath);
        }

        static string Arg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return fallback;
        }
    }
}
