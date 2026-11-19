using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Banjjak.EditorTools
{
    public static class BuildTools
    {
        public const string OutDir = "Build/Windows";
        public const string WebDir = "Build/WebGL";

        static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        static void Finish(BuildReport report, string what)
        {
            var s = report.summary;
            Debug.Log($"[Banjjak] {what} build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("반짝 워시/Windows 빌드")]
        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OutDir + "/BanjjakWash.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Finish(report, "Windows");
        }

        /// <summary>Browser build for static hosting (GitHub Pages): gzip with decompression fallback, so no server headers are needed.</summary>
        [MenuItem("반짝 워시/웹(WebGL) 빌드")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = false;   // the page asks for "no-store" anyway; see WebPage/index.html
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            if (Directory.Exists(WebDir)) Directory.Delete(WebDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = WebDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            // our own full-window Korean loading page replaces Unity's fixed 960x600 template
            // each build stamps a new version into the file URLs so browsers fetch the new game files right away
            if (report.summary.result == BuildResult.Succeeded && File.Exists("WebPage/index.html"))
                File.WriteAllText(WebDir + "/index.html",
                    File.ReadAllText("WebPage/index.html").Replace("__BUILD_VERSION__", System.DateTime.UtcNow.ToString("yyyyMMddHHmmss")),
                    new System.Text.UTF8Encoding(false));
            Finish(report, "WebGL");
        }
    }
}
