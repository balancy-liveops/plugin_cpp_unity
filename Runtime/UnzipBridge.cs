using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Balancy
{
    /// <summary>
    /// Bridge between C++ and Unity for unzipping operations.
    /// Uses Unity's built-in System.IO.Compression instead of native minizip.
    /// Works on all Unity platforms: iOS, Android, Windows, Mac, WebGL.
    ///
    /// WebGL Note: Uses JavaScript-side unzipping with JSZip for better IndexedDB integration.
    /// </summary>
    public static class UnzipBridge
    {
        private delegate void OnUnzipRequestDelegate(string id, string zipFilePath);

        private static readonly Dictionary<string, Action<string>> _pendingRequests = new Dictionary<string, Action<string>>();
        private static UnityMainThreadDispatcher _dispatcher;
        private static volatile bool _isStopped = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: DllImport to JavaScript unzip function
        [DllImport("__Internal")]
        private static extern void BalancyUnzipFile(string id, string zipFilePath);
#endif

        /// <summary>
        /// Initialize the unzip bridge and register callbacks with C++
        /// </summary>
        public static void Initialize()
        {
            _isStopped = false;
            _dispatcher = UnityMainThreadDispatcher.Instance();
            LibraryMethods.General.balancySetUnzipCallback(OnUnzipRequest);
            LibraryMethods.General.balancySetExtractZipFromMemoryCallback(OnExtractZipFromMemory);

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[Balancy] UnzipBridge initialized - using JavaScript JSZip for WebGL");

            // Register the callback that JavaScript will call
            RegisterWebGLCallback();
#else
            Debug.Log("[Balancy] UnzipBridge initialized - using Unity's built-in ZIP library");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Register callback for JavaScript to call when unzip completes
        /// </summary>
        private static void RegisterWebGLCallback()
        {
            // Create a global JavaScript function that can be called from .jslib
            Application.ExternalEval(@"
                Module.BalancyUnzipCompleted = function(id, resultPath) {
                    console.log('[Balancy] JavaScript calling Unity: UnzipCompleted', id, resultPath);
                    SendMessage('BalancyBridge', 'OnWebGLUnzipCompleted', id + '|' + resultPath);
                };
            ");
        }
#endif
        
        /// <summary>
        /// Called by C++ when it needs to extract ZIP from memory
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.ExtractZipFromMemoryCallback))]
        private static string OnExtractZipFromMemory(IntPtr zipDataPtr, int dataSize, bool includeHeaders)
        {
            if (zipDataPtr == IntPtr.Zero || dataSize <= 0)
            {
                Debug.LogError("[Balancy] OnExtractZipFromMemory: Invalid ZIP data");
                return string.Empty;
            }
            
            try
            {
                // Marshal the unmanaged byte array to managed byte array
                byte[] zipData = new byte[dataSize];
                Marshal.Copy(zipDataPtr, zipData, 0, dataSize);
                
                // Extract and return the result
                return ExtractZipFromMemory(zipData, includeHeaders);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] OnExtractZipFromMemory failed: {ex.Message}\n{ex.StackTrace}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Called by C++ when it needs to unzip a file
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(OnUnzipRequestDelegate))]
        private static void OnUnzipRequest(string id, string zipFilePath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Use JavaScript-side unzipping
            try
            {
                BalancyUnzipFile(id, zipFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] Failed to call JavaScript unzip: {ex.Message}");
                NotifyUnzipCompleted(id, string.Empty);
            }
#else
            // Other platforms: Use C# unzipping
            if (_dispatcher == null)
            {
                Debug.LogError("[Balancy][UnzipBridge] ERROR: UnzipBridge not initialized! Call Initialize() first.");
                NotifyUnzipCompleted(id, string.Empty);
                return;
            }

            _dispatcher.StartCoroutine(UnzipAsync(id, zipFilePath));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Called by JavaScript when unzip completes (via Module.BalancyUnzipCompleted)
        /// Message format: "id|resultPath"
        /// </summary>
        public static void OnWebGLUnzipCompleted(string message)
        {
            var parts = message.Split('|');
            if (parts.Length != 2)
            {
                Debug.LogError($"[Balancy] Invalid WebGL unzip callback message: {message}");
                return;
            }

            string id = parts[0];
            string resultPath = parts[1];

            Debug.Log($"[Balancy] WebGL unzip completed: id={id}, path={resultPath}");
            NotifyUnzipCompleted(id, resultPath);
        }
#endif
        
        /// <summary>
        /// Unzip the file asynchronously using Unity's System.IO.Compression
        /// </summary>
        private static IEnumerator UnzipAsync(string id, string zipFilePath)
        {
            string extractedFolderPath = string.Empty;
            bool success = false;
            Exception errorException = null;

            // Check if it's an android_asset path (resources from StreamingAssets on Android)
            bool isAndroidAssetPath = zipFilePath.Contains("/android_asset/");

#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, StreamingAssets are inside the APK and must be read via UnityWebRequest
            if (isAndroidAssetPath)
            {
                yield return UnzipFromAndroidStreamingAssets(id, zipFilePath);
                yield break;
            }
#endif

            // Validate file exists
            if (!File.Exists(zipFilePath))
            {
                Debug.LogError($"[Balancy] ZIP file not found: {zipFilePath}");
                NotifyUnzipCompleted(id, string.Empty);
                yield break;
            }

            // Get the folder name from the zip file (remove extension)
            string zipFileName = Path.GetFileNameWithoutExtension(zipFilePath);
            string destinationFolder = Path.GetDirectoryName(zipFilePath);
            extractedFolderPath = Path.Combine(destinationFolder, zipFileName);

            // Delete existing folder if it exists
            if (Directory.Exists(extractedFolderPath))
            {
                try
                {
                    Directory.Delete(extractedFolderPath, true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Balancy] Failed to delete existing folder: {ex.Message}");
                    NotifyUnzipCompleted(id, string.Empty);
                    yield break;
                }
            }

            // Create destination directory
            try
            {
                Directory.CreateDirectory(extractedFolderPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] Failed to create destination folder: {ex.Message}");
                NotifyUnzipCompleted(id, string.Empty);
                yield break;
            }
            
            // Extract the ZIP file - we need to do this without try-catch to allow yield
            ZipArchive archive = null;
            
            // Open archive (no yield here, so try-catch is ok)
            try
            {
                archive = ZipFile.OpenRead(zipFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] Failed to open ZIP file: {ex.Message}");
                NotifyUnzipCompleted(id, string.Empty);
                yield break;
            }
            
            // Extract entries (WITH yield, so NO try-catch allowed here)
            int totalEntries = archive.Entries.Count;
            int processedEntries = 0;
            
            foreach (var entry in archive.Entries)
            {
                // Skip directories (they're created automatically)
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                {
                    processedEntries++;
                    continue;
                }
                
                // Get the full path for the entry
                string destinationPath = Path.Combine(extractedFolderPath, entry.FullName);
                
                // Create subdirectories if needed
                string directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    try
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Balancy] Failed to create directory '{directoryPath}': {ex.Message}");
                        continue;
                    }
                }
                
                // Extract the file
                try
                {
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Balancy] Failed to extract entry '{entry.FullName}': {ex.Message}");
                }
                
                processedEntries++;
                
                // Yield every 10 files to avoid blocking the main thread
                if (processedEntries % 10 == 0)
                {
                    yield return null;
                }
            }
            
            // Cleanup
            if (archive != null)
            {
                archive.Dispose();
            }
            
            success = true;

            // Handle result
            if (success)
            {
                // Add trailing slash to match C++ expectations
                if (!extractedFolderPath.EndsWith("/"))
                {
                    extractedFolderPath += "/";
                }

                NotifyUnzipCompleted(id, extractedFolderPath);
            }
            else
            {
                Debug.LogError($"[Balancy] Failed to unzip {zipFilePath}: {errorException?.Message}");

                // Clean up failed extraction
                if (!string.IsNullOrEmpty(extractedFolderPath) && Directory.Exists(extractedFolderPath))
                {
                    try { Directory.Delete(extractedFolderPath, true); }
                    catch { }
                }

                NotifyUnzipCompleted(id, string.Empty);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// On Android, StreamingAssets are inside the APK (which is a ZIP file).
        /// We must use UnityWebRequest to read the ZIP file bytes, then extract from memory.
        /// </summary>
        private static IEnumerator UnzipFromAndroidStreamingAssets(string id, string zipFilePath)
        {
            // Convert /android_asset/Balancy/... path to URL format that UnityWebRequest understands
            string assetPath = zipFilePath;
            if (zipFilePath.StartsWith("/android_asset/"))
            {
                // /android_asset/Balancy/xxx.zip -> Application.streamingAssetsPath + /xxx.zip
                var relativePath = zipFilePath.Substring("/android_asset/".Length);
                assetPath = Application.streamingAssetsPath + "/" + relativePath;
            }

            // Use UnityWebRequest to read the ZIP file from the APK
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(assetPath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Balancy] Failed to read ZIP from APK: {request.error}, URL: {assetPath}");
                    NotifyUnzipCompleted(id, string.Empty);
                    yield break;
                }

                byte[] zipData = request.downloadHandler.data;

                // Determine extraction destination in persistentDataPath (writable location)
                // C++ getCachePath() uses "persistentDataPath/Balancy/Models/" as the base
                // The relativePath from android_asset starts with "Balancy/..." so we need to
                // extract to "persistentDataPath/Balancy/Models/..." to match C++ expectations
                string relativePath;
                if (zipFilePath.StartsWith("/android_asset/"))
                {
                    // /android_asset/Balancy/xxx_Cache/Files/12345.zip -> Balancy/xxx_Cache/Files/12345.zip
                    relativePath = zipFilePath.Substring("/android_asset/".Length);

                    // Remove the "Balancy/" prefix since we'll add "Balancy/Models/" base path
                    if (relativePath.StartsWith("Balancy/"))
                    {
                        relativePath = relativePath.Substring("Balancy/".Length);
                    }
                }
                else
                {
                    relativePath = Path.GetFileName(zipFilePath);
                }

                // Extract folder path: remove .zip extension
                string zipFileName = Path.GetFileNameWithoutExtension(relativePath);
                string parentDir = Path.GetDirectoryName(relativePath);
                string extractedRelativePath = string.IsNullOrEmpty(parentDir)
                    ? zipFileName
                    : Path.Combine(parentDir, zipFileName);

                // Full destination path in persistentDataPath/Balancy/Models/ (matches C++ getCachePath)
                string destinationFolder = Path.Combine(Application.persistentDataPath, "Balancy", "Models", extractedRelativePath);

                // Delete existing folder if it exists
                bool setupFailed = false;
                string setupError = null;

                if (Directory.Exists(destinationFolder))
                {
                    try
                    {
                        Directory.Delete(destinationFolder, true);
                    }
                    catch (Exception ex)
                    {
                        setupError = $"Failed to delete existing folder: {ex.Message}";
                        setupFailed = true;
                    }
                }

                // Create destination directory
                if (!setupFailed)
                {
                    try
                    {
                        Directory.CreateDirectory(destinationFolder);
                    }
                    catch (Exception ex)
                    {
                        setupError = $"Failed to create destination folder: {ex.Message}";
                        setupFailed = true;
                    }
                }

                if (setupFailed)
                {
                    Debug.LogError($"[Balancy][UnzipBridge] {setupError}");
                    NotifyUnzipCompleted(id, string.Empty);
                    yield break;
                }

                // Extract from memory - no yield inside try-catch (C# limitation)
                bool success = false;
                int processedEntries = 0;
                string extractError = null;

                try
                {
                    using (var memoryStream = new MemoryStream(zipData))
                    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            // Skip directories
                            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                            {
                                processedEntries++;
                                continue;
                            }

                            string destinationPath = Path.Combine(destinationFolder, entry.FullName);
                            string directoryPath = Path.GetDirectoryName(destinationPath);

                            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                            }

                            entry.ExtractToFile(destinationPath, overwrite: true);
                            processedEntries++;
                        }

                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    extractError = $"{ex.Message}\n{ex.StackTrace}";
                    success = false;
                }

                if (!success)
                {
                    Debug.LogError($"[Balancy] Failed to extract ZIP from memory: {extractError}");
                }

                if (success)
                {
                    // Add trailing slash to match C++ expectations
                    if (!destinationFolder.EndsWith("/"))
                    {
                        destinationFolder += "/";
                    }
                    NotifyUnzipCompleted(id, destinationFolder);
                }
                else
                {
                    // Clean up on failure
                    if (Directory.Exists(destinationFolder))
                    {
                        try { Directory.Delete(destinationFolder, true); }
                        catch { }
                    }
                    NotifyUnzipCompleted(id, string.Empty);
                }
            }
        }
#endif

        /// <summary>
        /// Notify C++ that unzipping is complete
        /// </summary>
        private static void NotifyUnzipCompleted(string id, string extractedFolderPath)
        {
            if (_isStopped) return;

            try
            {
                LibraryMethods.General.balancyUnzipCompleted(id, extractedFolderPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] Failed to notify unzip completion: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Extract ZIP archive from memory and return concatenated content of all files
        /// This is used for compressed dictionary files that need to be merged into a single string
        /// </summary>
        public static string ExtractZipFromMemory(byte[] zipData, bool includeHeaders)
        {
            if (zipData == null || zipData.Length == 0)
            {
                Debug.LogError("[Balancy] ExtractZipFromMemory: ZIP data is empty");
                return string.Empty;
            }

            try
            {
                using (var memoryStream = new MemoryStream(zipData))
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    var resultBuilder = new System.Text.StringBuilder();
                    int fileCount = 0;

                    foreach (var entry in archive.Entries)
                    {
                        // Skip directories
                        if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                        {
                            continue;
                        }

                        // Add separator between files (only if multiple files and not first file)
                        if (fileCount > 0 && includeHeaders)
                        {
                            resultBuilder.Append("\n\n");
                        }

                        // Add file header only if requested and multiple files
                        if (includeHeaders && archive.Entries.Count > 1)
                        {
                            resultBuilder.Append($"// File: {entry.FullName}\n");
                        }

                        // Extract and append file content
                        using (var entryStream = entry.Open())
                        using (var reader = new StreamReader(entryStream))
                        {
                            string content = reader.ReadToEnd();
                            resultBuilder.Append(content);
                        }

                        fileCount++;
                    }

                    Debug.Log($"[Balancy] Successfully extracted {fileCount} files from memory ZIP ({zipData.Length} bytes)");
                    return resultBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] Failed to extract ZIP from memory: {ex.Message}\n{ex.StackTrace}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Clean up resources
        /// </summary>
        public static void Cleanup()
        {
            _isStopped = true;
            _pendingRequests.Clear();
        }
    }
}
