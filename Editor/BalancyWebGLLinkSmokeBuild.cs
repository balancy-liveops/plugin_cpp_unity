#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Balancy.Editor
{
    /// <summary>Builds a WebGL player so native/C# interop is checked by the real Unity linker.</summary>
    public static class BalancyWebGLLinkSmokeBuild
    {
        public static void Build()
        {
            var outputPath = Environment.GetEnvironmentVariable("BALANCY_WEBGL_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("BALANCY_WEBGL_BUILD_PATH must point to the player output directory.");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("The WebGL link smoke test requires at least one enabled scene.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Unity WebGL build failed: {report.summary.result}");
        }
    }
}
#endif
