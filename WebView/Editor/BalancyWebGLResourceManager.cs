#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

namespace Balancy.WebView.Editor
{
    /// <summary>
    /// Manages WebGL resources for Balancy WebView
    /// Ensures TypeScript bundle is copied to StreamingAssets before build
    /// </summary>
    public class BalancyWebGLResourceManager : IPreprocessBuildWithReport
    {
        // Build callback order (lower = earlier)
        public int callbackOrder => 0;

        // Destination paths (StreamingAssets)
        private const string DEST_DIR = "Assets/StreamingAssets/Balancy";
        private const string WEBVIEW_BUNDLE_DEST = DEST_DIR + "/balancy-webview.umd.js";
        private const string WEBVIEW_SOURCEMAP_DEST = DEST_DIR + "/balancy-webview.umd.js.map";
        private const string BRIDGE_DEST = DEST_DIR + "/balancy-webview-bridge.js";
        private const string INIT_SCRIPT_DEST = DEST_DIR + "/balancy-webgl-init.js";
        private const string JSZIP_DEST = DEST_DIR + "/jszip.min.js";

        /// <summary>
        /// Resolves the root path of the Balancy package, whether it's embedded in Assets or installed via UPM.
        /// </summary>
        private static string GetPackagePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BalancyWebGLResourceManager).Assembly);
            if (packageInfo != null)
                return packageInfo.resolvedPath;

            // Fallback for embedded/local development
            return Path.Combine(Application.dataPath, "Balancy");
        }

        /// <summary>
        /// Called before Unity build starts
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            // Only process for WebGL builds
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            Debug.Log("[Balancy] Pre-build: Checking WebGL resources...");

            string packagePath = GetPackagePath();
            string sourceDir = Path.Combine(packagePath, "WebView", "Resources", "WebGL");
            string webviewBundleSource = Path.Combine(sourceDir, "balancy-webview.umd.js");

            // Check if resources need to be copied
            bool needsCopy = !File.Exists(WEBVIEW_BUNDLE_DEST) ||
                           !File.Exists(BRIDGE_DEST) ||
                           !File.Exists(INIT_SCRIPT_DEST) ||
                           !File.Exists(JSZIP_DEST);

            if (needsCopy)
            {
                Debug.Log("[Balancy] WebGL resources missing in StreamingAssets, copying now...");

                if (!CopyWebGLResources(packagePath))
                {
                    throw new BuildFailedException("[Balancy] Failed to copy WebGL resources to StreamingAssets!");
                }

                AssetDatabase.Refresh();
            }
            else
            {
                // Check if source is newer than destination
                if (File.Exists(webviewBundleSource))
                {
                    var sourceTime = File.GetLastWriteTime(webviewBundleSource);
                    var destTime = File.GetLastWriteTime(WEBVIEW_BUNDLE_DEST);

                    if (sourceTime > destTime)
                    {
                        Debug.Log("[Balancy] Source files are newer, updating StreamingAssets...");
                        CopyWebGLResources(packagePath);
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        Debug.Log("[Balancy] WebGL resources are up to date");
                    }
                }
                else
                {
                    Debug.Log("[Balancy] WebGL resources are up to date");
                }
            }
        }

        /// <summary>
        /// Copy all WebGL resources to StreamingAssets
        /// </summary>
        private static bool CopyWebGLResources(string packagePath)
        {
            try
            {
                string sourceDir = Path.Combine(packagePath, "WebView", "Resources", "WebGL");
                string webviewBundleSource = Path.Combine(sourceDir, "balancy-webview.umd.js");
                string webviewSourcemapSource = Path.Combine(sourceDir, "balancy-webview.umd.js.map");
                string bridgeSource = Path.Combine(packagePath, "WebView", "Resources", "balancy-webview-bridge.txt");
                string initScriptSource = Path.Combine(sourceDir, "balancy-webgl-init.js");
                string jszipSource = Path.Combine(sourceDir, "jszip.min.js.txt");

                // Ensure destination directory exists
                if (!Directory.Exists(DEST_DIR))
                {
                    Directory.CreateDirectory(DEST_DIR);
                    Debug.Log($"[Balancy] Created directory: {DEST_DIR}");
                }

                // Copy WebView bundle
                if (File.Exists(webviewBundleSource))
                {
                    File.Copy(webviewBundleSource, WEBVIEW_BUNDLE_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(webviewBundleSource)}");
                }
                else
                {
                    Debug.LogError($"[Balancy] Source file not found: {webviewBundleSource}");
                    Debug.LogError("[Balancy] Please build TypeScript WebView first!");
                    return false;
                }

                // Copy source map if exists
                if (File.Exists(webviewSourcemapSource))
                {
                    File.Copy(webviewSourcemapSource, WEBVIEW_SOURCEMAP_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(webviewSourcemapSource)}");
                }

                // Copy bridge script (convert .txt to .js)
                if (File.Exists(bridgeSource))
                {
                    File.Copy(bridgeSource, BRIDGE_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(bridgeSource)} -> balancy-webview-bridge.js");
                }
                else
                {
                    Debug.LogError($"[Balancy] Bridge source not found: {bridgeSource}");
                    return false;
                }

                // Copy init script
                if (File.Exists(initScriptSource))
                {
                    File.Copy(initScriptSource, INIT_SCRIPT_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(initScriptSource)}");
                }
                else
                {
                    Debug.LogError($"[Balancy] Init script not found: {initScriptSource}");
                    return false;
                }

                // Copy JSZip library
                if (File.Exists(jszipSource))
                {
                    File.Copy(jszipSource, JSZIP_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(jszipSource)}");
                }
                else
                {
                    Debug.LogError($"[Balancy] JSZip library not found: {jszipSource}");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Balancy] Exception while copying resources: {ex.Message}");
                return false;
            }
        }
    }
}
#endif
