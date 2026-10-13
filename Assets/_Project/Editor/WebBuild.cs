using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.WebGL;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WormRush.EditorTools
{
    /// <summary>
    /// Release Web build for static hosting (Cloudflare Workers/Pages). One entry point for the menu and batch mode:
    ///   Unity -batchmode -nographics -quit -projectPath . -logFile build.log
    ///         -executeMethod WormRush.EditorTools.WebBuild.Run [-buildPath Builds/WebGL]
    ///         [-webExceptions none|explicit|full] [-webSymbols off|embedded]
    /// The last two are for diagnosing a crash in the browser (see <see cref="ExceptionsFromArgs"/>).
    ///
    /// What makes it small, and why it is done here and not in ProjectSettings: the editor, the tests and the
    /// render check keep running on URP exactly as before; only this build is switched, and every setting is put
    /// back afterwards.
    ///   - Brotli + "decompression fallback": the raw .wasm is over the 25 MiB per-file limit of Cloudflare, and a
    ///     static host does not send Content-Encoding for Unity's .br files, so the loader unpacks them in JS.
    ///   - IL2CPP "Optimize size", managed stripping High, wasm "Disk size with LTO", no exception support.
    ///   - No render pipeline asset: the game draws flat circles and a uGUI canvas and needs nothing from URP, which
    ///     drags its shaders, textures and a large amount of code into the player. Circle.shader has a built-in
    ///     pipeline SubShader for this.
    ///   - A generated one-camera scene instead of SampleScene (its URP components would keep URP's code alive).
    /// </summary>
    public static class WebBuild
    {
        const string DefaultOutput = "Builds/WebGL";
        const string BuildsFolder = "Builds";
        const string TempScenePath = "Assets/_WebBuildScene.unity";
        const string TemplateName = "PROJECT:WormRush";   // Assets/WebGLTemplates/WormRush
        const string CircleShaderPath = "Assets/_Project/View/Shaders/Circle.shader";
        const string InstancedMaterialPath = "Assets/_Project/Resources/CircleInstanced.mat";
        const string MainCameraTag = "MainCamera";
        static readonly string[] SettingsFiles =
        {
            "ProjectSettings/ProjectSettings.asset",
            "ProjectSettings/QualitySettings.asset",
            "ProjectSettings/GraphicsSettings.asset",
        };
        const long PagesMaxFileBytes = 25L * 1024 * 1024; // Cloudflare: 25 MiB per file
        const float BytesPerMegabyte = 1024f * 1024f;

        sealed class PipelineState
        {
            public RenderPipelineAsset Default;
            public RenderPipelineAsset[] PerQualityLevel;
            public int QualityLevel;
        }

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
            Dictionary<string, byte[]> settingsOnDisk = BackUpSettings();
            PipelineState pipeline = null;
            string openScene = SceneManager.GetActiveScene().path;
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
                PlayerSettings.SplashScreen.show = false;   // a Personal licence may still keep it in the build
                WebBuildStrip.Enabled = true;
                pipeline = DropRenderPipeline();
                if (!CreateBuildScene())
                {
                    Debug.LogError("[WebBuild] Cancelled: could not prepare the build scene.");
                    return false;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { TempScenePath },
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
                WebBuildStrip.Enabled = false;
                if (pipeline != null)
                {
                    RestoreRenderPipeline(pipeline);
                }
                ReopenScene(openScene);
                RestoreSettingsOnDisk(settingsOnDisk);
                AssetDatabase.DeleteAsset(TempScenePath);
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
        /// Takes the render pipeline asset off the project (default and every quality level) for the build. The
        /// player then runs on the built-in pipeline and URP's assets are not reachable, so they are not included.
        /// </summary>
        static PipelineState DropRenderPipeline()
        {
            var state = new PipelineState
            {
                Default = GraphicsSettings.defaultRenderPipeline,
                QualityLevel = QualitySettings.GetQualityLevel(),
                PerQualityLevel = new RenderPipelineAsset[QualitySettings.names.Length],
            };
            for (int level = 0; level < state.PerQualityLevel.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                state.PerQualityLevel[level] = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(state.QualityLevel, false);
            GraphicsSettings.defaultRenderPipeline = null;
            return state;
        }

        static void RestoreRenderPipeline(PipelineState state)
        {
            for (int level = 0; level < state.PerQualityLevel.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = state.PerQualityLevel[level];
            }
            QualitySettings.SetQualityLevel(state.QualityLevel, false);
            GraphicsSettings.defaultRenderPipeline = state.Default;
        }

        /// <summary>
        /// The playable builds everything from code (Flow/Bootstrap) and only needs a camera with the MainCamera tag.
        /// The scene is made through the API, saved for the build and deleted again. It replaces the open scene
        /// while the build runs (the editor asks to save first); <see cref="ReopenScene"/> brings it back.
        /// </summary>
        static bool CreateBuildScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = MainCameraTag };
            SceneManager.MoveGameObjectToScene(camera, scene);
            return EditorSceneManager.SaveScene(scene, TempScenePath);
        }

        static void ReopenScene(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
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
                string why = string.Join(", ", info.GetReasonsForIncluding(module).Take(3));
                Debug.Log("[WebBuild] module " + module + "  <-  " + why);
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
