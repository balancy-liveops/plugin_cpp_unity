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

        // Source paths (within package)
        private const string SOURCE_DIR = "Assets/Balancy/WebView/Resources/WebGL";
        private const string WEBVIEW_BUNDLE_SOURCE = SOURCE_DIR + "/balancy-webview.umd.js";
        private const string WEBVIEW_SOURCEMAP_SOURCE = SOURCE_DIR + "/balancy-webview.umd.js.map";
        private const string BRIDGE_SOURCE = "Assets/Balancy/WebView/Resources/balancy-webview-bridge.txt";
        private const string INIT_SCRIPT_SOURCE = SOURCE_DIR + "/balancy-webgl-init.js";
        private const string JSZIP_SOURCE = SOURCE_DIR + "/jszip.min.js.txt";

        // Destination paths (StreamingAssets)
        private const string DEST_DIR = "Assets/StreamingAssets/Balancy";
        private const string WEBVIEW_BUNDLE_DEST = DEST_DIR + "/balancy-webview.umd.js";
        private const string WEBVIEW_SOURCEMAP_DEST = DEST_DIR + "/balancy-webview.umd.js.map";
        private const string BRIDGE_DEST = DEST_DIR + "/balancy-webview-bridge.js";
        private const string INIT_SCRIPT_DEST = DEST_DIR + "/balancy-webgl-init.js";
        private const string JSZIP_DEST = DEST_DIR + "/jszip.min.js";

        /// <summary>
        /// Menu item to manually copy WebGL resources
        /// </summary>
        [MenuItem("Balancy/WebView/Copy WebGL Resources to StreamingAssets")]
        public static void CopyResourcesToStreamingAssets()
        {
            Debug.Log("[Balancy] Copying WebGL resources to StreamingAssets...");

            bool success = CopyWebGLResources();

            if (success)
            {
                AssetDatabase.Refresh();
                Debug.Log("[Balancy] ✅ WebGL resources copied successfully!");
            }
            else
            {
                Debug.LogError("[Balancy] ❌ Failed to copy WebGL resources!");
            }
        }

        /// <summary>
        /// Menu item to validate WebGL resources
        /// </summary>
        [MenuItem("Balancy/WebView/Validate WebGL Resources")]
        public static void ValidateWebGLResources()
        {
            Debug.Log("[Balancy] Validating WebGL resources...");

            bool allValid = true;

            // Check source files
            if (!File.Exists(WEBVIEW_BUNDLE_SOURCE))
            {
                Debug.LogError($"[Balancy] ❌ Missing source: {WEBVIEW_BUNDLE_SOURCE}");
                Debug.LogError("[Balancy] Please run the TypeScript build: npm run build:webview");
                allValid = false;
            }
            else
            {
                Debug.Log($"[Balancy] ✅ Found: {WEBVIEW_BUNDLE_SOURCE}");
            }

            if (!File.Exists(BRIDGE_SOURCE))
            {
                Debug.LogError($"[Balancy] ❌ Missing source: {BRIDGE_SOURCE}");
                allValid = false;
            }
            else
            {
                Debug.Log($"[Balancy] ✅ Found: {BRIDGE_SOURCE}");
            }

            if (!File.Exists(INIT_SCRIPT_SOURCE))
            {
                Debug.LogError($"[Balancy] ❌ Missing source: {INIT_SCRIPT_SOURCE}");
                allValid = false;
            }
            else
            {
                Debug.Log($"[Balancy] ✅ Found: {INIT_SCRIPT_SOURCE}");
            }

            if (!File.Exists(JSZIP_SOURCE))
            {
                Debug.LogError($"[Balancy] ❌ Missing source: {JSZIP_SOURCE}");
                allValid = false;
            }
            else
            {
                Debug.Log($"[Balancy] ✅ Found: {JSZIP_SOURCE}");
            }

            // Check destination files
            if (!File.Exists(WEBVIEW_BUNDLE_DEST))
            {
                Debug.LogWarning($"[Balancy] ⚠️ Missing in StreamingAssets: {WEBVIEW_BUNDLE_DEST}");
                Debug.LogWarning("[Balancy] Run: Balancy > WebView > Copy WebGL Resources");
                allValid = false;
            }
            else
            {
                Debug.Log($"[Balancy] ✅ Found: {WEBVIEW_BUNDLE_DEST}");
            }

            if (allValid)
            {
                Debug.Log("[Balancy] ✅ All WebGL resources are valid!");
            }
            else
            {
                Debug.LogWarning("[Balancy] ⚠️ Some WebGL resources are missing. Fix before building.");
            }
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

            // Check if resources need to be copied
            bool needsCopy = !File.Exists(WEBVIEW_BUNDLE_DEST) ||
                           !File.Exists(BRIDGE_DEST) ||
                           !File.Exists(INIT_SCRIPT_DEST) ||
                           !File.Exists(JSZIP_DEST);

            if (needsCopy)
            {
                Debug.Log("[Balancy] WebGL resources missing in StreamingAssets, copying now...");

                if (!CopyWebGLResources())
                {
                    throw new BuildFailedException("[Balancy] Failed to copy WebGL resources to StreamingAssets!");
                }

                AssetDatabase.Refresh();
            }
            else
            {
                // Check if source is newer than destination
                var sourceTime = File.GetLastWriteTime(WEBVIEW_BUNDLE_SOURCE);
                var destTime = File.GetLastWriteTime(WEBVIEW_BUNDLE_DEST);

                if (sourceTime > destTime)
                {
                    Debug.Log("[Balancy] Source files are newer, updating StreamingAssets...");
                    CopyWebGLResources();
                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.Log("[Balancy] ✅ WebGL resources are up to date");
                }
            }
        }

        /// <summary>
        /// Copy all WebGL resources to StreamingAssets
        /// </summary>
        private static bool CopyWebGLResources()
        {
            try
            {
                // Ensure destination directory exists
                if (!Directory.Exists(DEST_DIR))
                {
                    Directory.CreateDirectory(DEST_DIR);
                    Debug.Log($"[Balancy] Created directory: {DEST_DIR}");
                }

                // Copy WebView bundle
                if (File.Exists(WEBVIEW_BUNDLE_SOURCE))
                {
                    File.Copy(WEBVIEW_BUNDLE_SOURCE, WEBVIEW_BUNDLE_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(WEBVIEW_BUNDLE_SOURCE)}");
                }
                else
                {
                    Debug.LogError($"[Balancy] Source file not found: {WEBVIEW_BUNDLE_SOURCE}");
                    Debug.LogError("[Balancy] Please build TypeScript WebView first!");
                    return false;
                }

                // Copy source map if exists
                if (File.Exists(WEBVIEW_SOURCEMAP_SOURCE))
                {
                    File.Copy(WEBVIEW_SOURCEMAP_SOURCE, WEBVIEW_SOURCEMAP_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(WEBVIEW_SOURCEMAP_SOURCE)}");
                }

                // Copy bridge script (convert .txt to .js)
                if (File.Exists(BRIDGE_SOURCE))
                {
                    File.Copy(BRIDGE_SOURCE, BRIDGE_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(BRIDGE_SOURCE)} → balancy-webview-bridge.js");
                }
                else
                {
                    Debug.LogError($"[Balancy] Bridge source not found: {BRIDGE_SOURCE}");
                    return false;
                }

                // Copy init script
                if (File.Exists(INIT_SCRIPT_SOURCE))
                {
                    File.Copy(INIT_SCRIPT_SOURCE, INIT_SCRIPT_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(INIT_SCRIPT_SOURCE)}");
                }
                else
                {
                    Debug.LogError($"[Balancy] Init script not found: {INIT_SCRIPT_SOURCE}");
                    return false;
                }

                // Copy JSZip library
                if (File.Exists(JSZIP_SOURCE))
                {
                    File.Copy(JSZIP_SOURCE, JSZIP_DEST, true);
                    Debug.Log($"[Balancy] Copied: {Path.GetFileName(JSZIP_SOURCE)}");
                }
                else
                {
                    Debug.LogError($"[Balancy] JSZip library not found: {JSZIP_SOURCE}");
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

        /// <summary>
        /// Menu item to clean StreamingAssets
        /// </summary>
        [MenuItem("Balancy/WebView/Clean WebGL Resources from StreamingAssets")]
        public static void CleanStreamingAssets()
        {
            if (Directory.Exists(DEST_DIR))
            {
                Directory.Delete(DEST_DIR, true);
                AssetDatabase.Refresh();
                Debug.Log("[Balancy] ✅ Cleaned WebGL resources from StreamingAssets");
            }
            else
            {
                Debug.Log("[Balancy] StreamingAssets/Balancy directory doesn't exist");
            }
        }
    }
}
#endif
