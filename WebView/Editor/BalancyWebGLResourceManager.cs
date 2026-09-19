#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System;
using System.Collections.Generic;
using System.IO;

namespace Balancy.WebView.Editor
{
    /// <summary>
    /// Keeps the runtime bridge synchronized for every player build and adds the
    /// remaining browser-only resources for WebGL builds.
    /// </summary>
    public class BalancyWebGLResourceManager : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        private const string DEST_DIR = "Assets/StreamingAssets/Balancy";
        private const string WEBVIEW_BUNDLE_DEST = DEST_DIR + "/balancy-webview.umd.js";
        private const string WEBVIEW_SOURCEMAP_DEST = DEST_DIR + "/balancy-webview.umd.js.map";
        private const string BRIDGE_DEST = DEST_DIR + "/balancy-webview-bridge.js";
        private const string INIT_SCRIPT_DEST = DEST_DIR + "/balancy-webgl-init.js";
        private const string JSZIP_DEST = DEST_DIR + "/jszip.min.js";
        private const string MANIFEST_PATH = DEST_DIR + "/balancy_files_manifest.txt";
        private const string BRIDGE_MANIFEST_ENTRY = "./balancy-webview-bridge.js";

        private static string GetPackagePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BalancyWebGLResourceManager).Assembly);
            if (packageInfo != null)
                return packageInfo.resolvedPath;
            return Path.Combine(Application.dataPath, "Balancy");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            string packagePath = GetPackagePath();
            Directory.CreateDirectory(DEST_DIR);

            // Native shells load this file directly. Always overwrite it so Android/iOS
            // can never package a stale copy left by an earlier WebGL build.
            string bridgeSource = Path.Combine(packagePath, "WebView", "Resources", "balancy-webview-bridge.txt");
            if (!CopyRequired(bridgeSource, BRIDGE_DEST, "Balancy WebView bridge"))
                throw new BuildFailedException("[Balancy] Failed to synchronize balancy-webview-bridge.js");
            EnsureManifestEntry();

            if (report.summary.platform == BuildTarget.WebGL && !CopyWebGLResources(packagePath))
                throw new BuildFailedException("[Balancy] Failed to copy WebGL resources to StreamingAssets");

            AssetDatabase.Refresh();
        }

        private static bool CopyWebGLResources(string packagePath)
        {
            string sourceDir = Path.Combine(packagePath, "WebView", "Resources", "WebGL");
            bool ok = true;
            ok &= CopyRequired(Path.Combine(sourceDir, "balancy-webview.umd.js"), WEBVIEW_BUNDLE_DEST, "WebView bundle");
            CopyOptional(Path.Combine(sourceDir, "balancy-webview.umd.js.map"), WEBVIEW_SOURCEMAP_DEST);
            ok &= CopyRequired(Path.Combine(sourceDir, "balancy-webgl-init.js"), INIT_SCRIPT_DEST, "WebGL init script");
            ok &= CopyRequired(Path.Combine(sourceDir, "jszip.min.js.txt"), JSZIP_DEST, "JSZip");
            return ok;
        }

        private static bool CopyRequired(string source, string destination, string label)
        {
            if (!File.Exists(source))
            {
                Debug.LogError($"[Balancy] {label} source not found: {source}");
                return false;
            }
            File.Copy(source, destination, true);
            Debug.Log($"[Balancy] Synchronized {label}: {Path.GetFileName(destination)} ({new FileInfo(destination).Length} bytes)");
            return true;
        }

        private static void CopyOptional(string source, string destination)
        {
            if (File.Exists(source)) File.Copy(source, destination, true);
        }

        private static void EnsureManifestEntry()
        {
            var lines = File.Exists(MANIFEST_PATH)
                ? new List<string>(File.ReadAllLines(MANIFEST_PATH))
                : new List<string>();
            lines.RemoveAll(line => string.Equals(line.Trim(), BRIDGE_MANIFEST_ENTRY, StringComparison.Ordinal));
            lines.Add(BRIDGE_MANIFEST_ENTRY);
            lines.Sort(StringComparer.Ordinal);
            File.WriteAllLines(MANIFEST_PATH, lines);
        }
    }
}
#endif
