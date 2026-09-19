using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

            // File downloads complete on a serialized worker. Hop back before
            // touching Coroutine/Unity APIs; extraction itself returns to a worker below.
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                if (!_isStopped && _dispatcher != null)
                    _dispatcher.StartCoroutine(UnzipAsync(id, zipFilePath));
                else
                    NotifyUnzipCompleted(id, string.Empty);
            });
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
        
        private sealed class ExtractResult
        {
            internal bool Success;
            internal string Folder;
            internal string Error;
            internal int Entries;
            internal double WorkerMs;
        }

        private static readonly SemaphoreSlim UnzipGate = new SemaphoreSlim(1, 1);

        private static ExtractResult ExtractArchive(ZipArchive archive, string destinationFolder)
        {
            var result = new ExtractResult { Folder = destinationFolder };
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (Directory.Exists(destinationFolder))
                    Directory.Delete(destinationFolder, true);
                Directory.CreateDirectory(destinationFolder);

                string root = Path.GetFullPath(destinationFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                        continue;

                    string destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!destinationPath.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException("ZIP entry escapes destination: " + entry.FullName);

                    string directoryPath = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directoryPath))
                        Directory.CreateDirectory(directoryPath);
                    entry.ExtractToFile(destinationPath, true);
                    result.Entries++;
                }
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Error = exception.Message + "\n" + exception.StackTrace;
                try
                {
                    if (Directory.Exists(destinationFolder))
                        Directory.Delete(destinationFolder, true);
                }
                catch { }
            }
            finally
            {
                result.WorkerMs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency;
            }
            return result;
        }

        private static ExtractResult ExtractFileOnWorker(string zipFilePath, string destinationFolder)
        {
            UnzipGate.Wait();
            try
            {
                using (var archive = ZipFile.OpenRead(zipFilePath))
                    return ExtractArchive(archive, destinationFolder);
            }
            finally
            {
                UnzipGate.Release();
            }
        }

        private static ExtractResult ExtractBytesOnWorker(byte[] zipData, string destinationFolder)
        {
            UnzipGate.Wait();
            try
            {
                using (var memoryStream = new MemoryStream(zipData, false))
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                    return ExtractArchive(archive, destinationFolder);
            }
            finally
            {
                UnzipGate.Release();
            }
        }

        private static IEnumerator AwaitExtraction(string id, Task<ExtractResult> task, long totalStarted)
        {
            while (!task.IsCompleted)
                yield return null;

            ExtractResult result;
            if (task.IsCanceled)
                result = new ExtractResult { Error = "Extraction task was canceled" };
            else if (task.IsFaulted)
                result = new ExtractResult { Error = task.Exception?.GetBaseException().ToString() ?? "Extraction task failed" };
            else
                result = task.Result;

            FreezeDiagnostics.Log("UNZIP_WORKER END id=" + id + " success=" + result.Success
                + " entries=" + result.Entries + " worker_ms="
                + result.WorkerMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
            FreezeDiagnostics.End("UNZIP_TOTAL END id=" + id, totalStarted, 0);

            if (!result.Success)
                Debug.LogError("[Balancy] Failed to unzip " + id + ": " + result.Error);

            string folder = result.Success ? result.Folder : string.Empty;
            if (!string.IsNullOrEmpty(folder) && !folder.EndsWith("/"))
                folder += "/";
            NotifyUnzipCompleted(id, folder);
        }

        /// <summary>
        /// Read platform data on the Unity thread when required, but perform all
        /// filesystem and decompression work on one bounded worker.
        /// </summary>
        private static IEnumerator UnzipAsync(string id, string zipFilePath)
        {
            long totalStarted = FreezeDiagnostics.Now;
            FreezeDiagnostics.Log("UNZIP_TOTAL BEGIN id=" + id);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (zipFilePath.Contains("/android_asset/"))
            {
                yield return UnzipFromAndroidStreamingAssets(id, zipFilePath, totalStarted);
                yield break;
            }
#endif

            if (!File.Exists(zipFilePath))
            {
                Debug.LogError("[Balancy] ZIP file not found: " + zipFilePath);
                NotifyUnzipCompleted(id, string.Empty);
                yield break;
            }

            string destinationFolder = Path.Combine(
                Path.GetDirectoryName(zipFilePath),
                Path.GetFileNameWithoutExtension(zipFilePath));

            FreezeDiagnostics.Log("UNZIP_WORKER BEGIN id=" + id);
            var task = Task.Run(() => ExtractFileOnWorker(zipFilePath, destinationFolder));
            yield return AwaitExtraction(id, task, totalStarted);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static IEnumerator UnzipFromAndroidStreamingAssets(string id, string zipFilePath, long totalStarted)
        {
            string assetPath = zipFilePath;
            if (zipFilePath.StartsWith("/android_asset/"))
            {
                var relativeAssetPath = zipFilePath.Substring("/android_asset/".Length);
                assetPath = Application.streamingAssetsPath + "/" + relativeAssetPath;
            }

            using (var request = UnityWebRequest.Get(assetPath))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[Balancy] Failed to read ZIP from APK: " + request.error + ", URL: " + assetPath);
                    NotifyUnzipCompleted(id, string.Empty);
                    yield break;
                }

                byte[] zipData = request.downloadHandler.data;
                string relativePath;
                if (zipFilePath.StartsWith("/android_asset/"))
                {
                    relativePath = zipFilePath.Substring("/android_asset/".Length);
                    if (relativePath.StartsWith("Balancy/"))
                        relativePath = relativePath.Substring("Balancy/".Length);
                }
                else
                {
                    relativePath = Path.GetFileName(zipFilePath);
                }

                string zipFileName = Path.GetFileNameWithoutExtension(relativePath);
                string parentDir = Path.GetDirectoryName(relativePath);
                string extractedRelativePath = string.IsNullOrEmpty(parentDir)
                    ? zipFileName
                    : Path.Combine(parentDir, zipFileName);
                string destinationFolder = Path.Combine(
                    Application.persistentDataPath, "Balancy", "Models", extractedRelativePath);

                FreezeDiagnostics.Log("UNZIP_WORKER BEGIN id=" + id + " bytes=" + zipData.Length);
                var task = Task.Run(() => ExtractBytesOnWorker(zipData, destinationFolder));
                yield return AwaitExtraction(id, task, totalStarted);
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
