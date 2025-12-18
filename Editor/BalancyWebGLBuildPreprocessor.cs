using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace Balancy.Editor
{
    /// <summary>
    /// Build preprocessor that copies WebGL resources to StreamingAssets before WebGL builds.
    /// This ensures all required files are available in the build output.
    /// </summary>
    public class BalancyWebGLBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Only run for WebGL builds
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            Debug.Log("[Balancy] WebGL Build - Copying resources to StreamingAssets...");

            // Ensure StreamingAssets/Balancy directory exists
            string streamingAssetsBalancy = Path.Combine(Application.streamingAssetsPath, "Balancy");
            if (!Directory.Exists(streamingAssetsBalancy))
            {
                Directory.CreateDirectory(streamingAssetsBalancy);
                Debug.Log($"[Balancy] Created directory: {streamingAssetsBalancy}");
            }

            // Source directory in Resources
            string resourcesPath = Path.Combine(Application.dataPath, "Balancy/WebView/Resources/WebGL");

            if (!Directory.Exists(resourcesPath))
            {
                Debug.LogWarning($"[Balancy] WebGL resources directory not found: {resourcesPath}");
                return;
            }

            // Copy all files from Resources/WebGL to StreamingAssets/Balancy
            CopyWebGLFile(resourcesPath, streamingAssetsBalancy, "balancy-webview.umd.js");
            CopyWebGLFile(resourcesPath, streamingAssetsBalancy, "balancy-webview.umd.js.map");
            CopyWebGLFile(resourcesPath, streamingAssetsBalancy, "balancy-webgl-init.js");

            // Copy jszip (from .txt to .js)
            CopyWebGLFile(resourcesPath, streamingAssetsBalancy, "jszip.min.js.txt", "jszip.min.js");

            // Note: balancy-webview-bridge.js is copied by BalancyWebGLResourceManager.cs
            // Source: Assets/Balancy/WebView/Resources/balancy-webview-bridge.txt
            // This happens automatically during WebGL builds

            Debug.Log("[Balancy] WebGL resources copied to StreamingAssets successfully!");
        }

        /// <summary>
        /// Copy a file from Resources to StreamingAssets
        /// </summary>
        private void CopyWebGLFile(string sourceDir, string destDir, string fileName, string destFileName = null)
        {
            destFileName = destFileName ?? fileName;

            string sourcePath = Path.Combine(sourceDir, fileName);
            string destPath = Path.Combine(destDir, destFileName);

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                Debug.Log($"[Balancy] Copied: {fileName} → {destFileName}");
            }
            else
            {
                Debug.LogWarning($"[Balancy] File not found: {sourcePath}");
            }
        }
    }
}
