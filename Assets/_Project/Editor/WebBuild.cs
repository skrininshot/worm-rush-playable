using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WormRush.EditorTools
{
    /// <summary>
    /// Release Web build for static hosting (Cloudflare Pages). One entry point for the menu and for batch mode:
    ///   Unity -batchmode -nographics -quit -projectPath . -logFile build.log
    ///         -executeMethod WormRush.EditorTools.WebBuild.Run [-buildPath Builds/WebGL]
    /// The output folder is the Pages "build output directory": drag it into the dashboard, or point wrangler at it.
    /// Brotli + "decompression fallback": the .wasm is ~47 MB raw, over the 25 MiB per-file limit of Pages, and a
    /// static host does not send Content-Encoding for Unity's .br files, so the loader unpacks them in JavaScript.
    /// The settings are put back after the build, so ProjectSettings stays untouched.
    /// </summary>
    public static class WebBuild
    {
        const string DefaultOutput = "Builds/WebGL";
        const string BuildsFolder = "Builds";
        const string FallbackScene = "Assets/Scenes/SampleScene.unity";
        const string TemplateName = "PROJECT:WormRush";   // Assets/WebGLTemplates/WormRush
        const string CircleShaderPath = "Assets/_Project/View/Shaders/Circle.shader";
        const string InstancedMaterialPath = "Assets/_Project/Resources/CircleInstanced.mat";
        const long PagesMaxFileBytes = 25L * 1024 * 1024; // Cloudflare Pages: 25 MiB per file
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

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                scenes = new[] { FallbackScene };
            }

            EnsureInstancedMaterial();

            // Stale files from an earlier build (other compression, other names) would end up in the upload.
            if (Directory.Exists(fullOutput))
            {
                Directory.Delete(fullOutput, true);
            }

            var compression = PlayerSettings.WebGL.compressionFormat;
            string template = PlayerSettings.WebGL.template;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.template = TemplateName;

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = fullOutput,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                };
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.template = template;
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[WebBuild] " + summary.result + ": " + summary.totalErrors + " error(s). Output: " + fullOutput);
                return false;
            }

            long total = 0;
            foreach (string file in Directory.GetFiles(fullOutput, "*", SearchOption.AllDirectories).OrderBy(f => f))
            {
                long size = new FileInfo(file).Length;
                total += size;
                string rel = file.Substring(fullOutput.Length).TrimStart(Path.DirectorySeparatorChar, '/');
                string flag = size > PagesMaxFileBytes ? "   <-- OVER the 25 MiB Cloudflare Pages limit" : "";
                Debug.Log("[WebBuild]   " + (size / BytesPerMegabyte).ToString("0.00") + " MB  " + rel + flag);
            }
            Debug.Log("[WebBuild] OK: " + (total / BytesPerMegabyte).ToString("0.00") + " MB in " + fullOutput);
            return true;
        }

        /// <summary>
        /// WorldView makes its material at run time, and a build strips every shader variant that no material asset
        /// uses: the INSTANCING_ON variant of WormRush/Circle goes, and RenderMeshInstanced then draws nothing (the
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
