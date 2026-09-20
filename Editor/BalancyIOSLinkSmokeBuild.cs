#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Balancy.Editor
{
    /// <summary>
    /// Exports an iOS Simulator or Device Xcode project for the native-link smoke test.
    /// The accompanying shell test runs xcodebuild, because a successful Unity
    /// export alone does not prove that every native symbol can be linked.
    /// </summary>
    public static class BalancyIOSLinkSmokeBuild
    {
        public static void ExportSimulator()
        {
            Export(iOSSdkVersion.SimulatorSDK);
        }

        public static void ExportDevice()
        {
            Export(iOSSdkVersion.DeviceSDK);
        }

        private static void Export(iOSSdkVersion sdkVersion)
        {
            var outputPath = Environment.GetEnvironmentVariable("BALANCY_IOS_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("BALANCY_IOS_BUILD_PATH must point to the Xcode export directory.");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("The iOS link smoke test requires at least one enabled scene.");

            var previousSdk = PlayerSettings.iOS.sdkVersion;
            var previousSimulatorArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = sdkVersion;
                if (sdkVersion == iOSSdkVersion.SimulatorSDK)
                    PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.Universal;

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.iOS,
                    options = BuildOptions.Development
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Unity iOS export failed: {report.summary.result}");
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previousSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousSimulatorArchitecture;
            }
        }
    }
}
#endif
