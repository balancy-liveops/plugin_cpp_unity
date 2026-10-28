#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Balancy.Editor
{
    /// <summary>
    /// Builds a Universal Windows Platform player so the plug-in importer settings (one
    /// libBalancyCore.dll per platform, UWP DLLs under Plugins/WSA) are validated by the real
    /// Unity build pipeline. Unity emits a Visual Studio solution; the IL2CPP compile and the
    /// native P/Invoke resolution happen when that solution is built with msbuild and run.
    /// </summary>
    public static class BalancyWSALinkSmokeBuild
    {
        public static void Build()
        {
            var outputPath = Environment.GetEnvironmentVariable("BALANCY_WSA_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("BALANCY_WSA_BUILD_PATH must point to the generated solution directory.");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("The UWP link smoke test requires at least one enabled scene.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WSAPlayer,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Unity UWP build failed: {report.summary.result}");
        }
    }
}
#endif
