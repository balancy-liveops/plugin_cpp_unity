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
        private static int _generation;
        private static readonly object ExtractionLock = new object();
        private static readonly List<Task> ExtractionTasks = new List<Task>();
        private static CancellationTokenSource _extractionCancellation = new CancellationTokenSource();

        private static bool IsCurrentSession(int generation) =>
            !_isStopped && generation == Volatile.Read(ref _generation);

        private static void StartSession()
        {
            Cleanup();
            lock (ExtractionLock)
            {
                _extractionCancellation = new CancellationTokenSource();
                Interlocked.Increment(ref _generation);
                _isStopped = false;
            }
        }

        private static Task<ExtractResult> StartExtraction(int generation, Func<CancellationToken, ExtractResult> extract)
        {
            lock (ExtractionLock)
            {
                if (!IsCurrentSession(generation))
                    return Task.FromResult(new ExtractResult { Canceled = true });
                var cancellation = _extractionCancellation.Token;
                // Only filesystem workers are joined on cleanup; none needs a Unity tick.
                ExtractionTasks.RemoveAll(task => task.IsCompleted);
                var worker = Task.Run(() =>
                {
                    try
                    {
                        cancellation.ThrowIfCancellationRequested();
                        return extract(cancellation);
                    }
                    catch (OperationCanceledException) { return new ExtractResult { Canceled = true }; }
                    catch (Exception exception) { return new ExtractResult { Error = exception.ToString() }; }
                });
                ExtractionTasks.Add(worker);
                return worker;
            }
        }

        // Keep the native-registered delegates rooted for the lifetime of the domain.
        private static readonly LibraryMethods.General.ExtractZipFromMemoryCallback _extractZipCallback = OnExtractZipFromMemory;
        private static readonly LibraryMethods.General.ReleaseExtractedZipCallback _releaseExtractedZipCallback = OnReleaseExtractedZip;

#if UNITY_WEBGL && !UNITY_EDITOR
        private static int _webGLRequestIndex;
        // WebGL: DllImport to JavaScript unzip function
        [DllImport("__Internal")]
        private static extern void BalancyUnzipFile(string id, string zipFilePath);
#endif

        /// <summary>
        /// Initialize the unzip bridge and register callbacks with C++
        /// </summary>
        public static void Initialize()
        {
            StartSession();
            _dispatcher = UnityMainThreadDispatcher.Instance();
            LibraryMethods.General.balancySetUnzipCallback(OnUnzipRequest);
            LibraryMethods.General.balancySetExtractZipFromMemoryCallback(_extractZipCallback);
            try
            {
                LibraryMethods.General.balancySetReleaseExtractedZipCallback(_releaseExtractedZipCallback);
            }
            catch (EntryPointNotFoundException)
            {
                // Native plugin predates the release callback: extraction still works, the
                // buffer is not returned. Rebuild the native libraries to close the leak.
                Debug.LogWarning("[Balancy] Native plugin lacks balancySetReleaseExtractedZipCallback; extracted zip buffers will not be released");
            }

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
        private static IntPtr OnExtractZipFromMemory(IntPtr zipDataPtr, int dataSize, bool includeHeaders)
        {
            if (zipDataPtr == IntPtr.Zero || dataSize <= 0)
            {
                Debug.LogError("[Balancy] OnExtractZipFromMemory: Invalid ZIP data");
                return IntPtr.Zero;
            }
            
            try
            {
                // Marshal the unmanaged byte array to managed byte array
                byte[] zipData = new byte[dataSize];
                Marshal.Copy(zipDataPtr, zipData, 0, dataSize);
                
                // Extract and hand native a buffer it returns through OnReleaseExtractedZip.
                return AllocateNativeUtf8(ExtractZipFromMemory(zipData, includeHeaders));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Balancy] OnExtractZipFromMemory failed: {ex.Message}\n{ex.StackTrace}");
                return IntPtr.Zero;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.ReleaseExtractedZipCallback))]
        private static void OnReleaseExtractedZip(IntPtr buffer)
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }

        /// <summary>
        /// Copies text into an unmanaged NUL-terminated UTF-8 buffer owned by the managed
        /// side. Native must return it through OnReleaseExtractedZip; an empty string yields
        /// IntPtr.Zero so native reports "empty" without a release round-trip.
        /// </summary>
        internal static IntPtr AllocateNativeUtf8(string text)
        {
            if (string.IsNullOrEmpty(text))
                return IntPtr.Zero;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Marshal.WriteByte(buffer, bytes.Length, 0);
            return buffer;
        }
        
        /// <summary>
        /// Called by C++ when it needs to unzip a file
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(OnUnzipRequestDelegate))]
        private static void OnUnzipRequest(string id, string zipFilePath)
        {
            int generation = Volatile.Read(ref _generation);
            if (!IsCurrentSession(generation)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            // JS echoes this opaque id; old-session replies cannot match a new request.
            string requestId = generation + ":" + Interlocked.Increment(ref _webGLRequestIndex);
            _pendingRequests[requestId] = path =>
            {
                if (IsCurrentSession(generation)) NotifyUnzipCompleted(id, path, generation);
            };
            try
            {
                BalancyUnzipFile(requestId, zipFilePath);
            }
            catch (Exception ex)
            {
                _pendingRequests.Remove(requestId);
                Debug.LogError($"[Balancy] Failed to call JavaScript unzip: {ex.Message}");
                if (IsCurrentSession(generation)) NotifyUnzipCompleted(id, string.Empty, generation);
            }
#else
            // Other platforms: Use C# unzipping
            if (_dispatcher == null)
            {
                Debug.LogError("[Balancy][UnzipBridge] ERROR: UnzipBridge not initialized! Call Initialize() first.");
                NotifyUnzipCompleted(id, string.Empty, generation);
                return;
            }

            // File downloads complete on a serialized worker. Hop back before
            // touching Coroutine/Unity APIs; extraction itself returns to a worker below.
            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                if (!IsCurrentSession(generation)) return;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // MonoBehaviour coroutines do not have a dependable Edit Mode
                    // player loop. Complete via the EditorApplication.update queue.
                    _ = UnzipInEditor(id, zipFilePath, generation);
                    return;
                }
#endif
                if (_dispatcher != null)
                    _dispatcher.StartCoroutine(UnzipAsync(id, zipFilePath, generation));
                else
                    NotifyUnzipCompleted(id, string.Empty, generation);
            });
#endif
        }

        /// <summary>
        /// Called by JavaScript when unzip completes (via Module.BalancyUnzipCompleted)
        /// Message format: "id|resultPath"
        /// </summary>
#if UNITY_WEBGL && !UNITY_EDITOR
        public static void OnWebGLUnzipCompleted(string message) => CompleteWebGLReply(message);
#endif

        private static void CompleteWebGLReply(string message)
        {
            var parts = message.Split('|');
            if (parts.Length != 2)
            {
                Debug.LogError($"[Balancy] Invalid WebGL unzip callback message: {message}");
                return;
            }

            string id = parts[0];
            string resultPath = parts[1];

            if (!_pendingRequests.TryGetValue(id, out var completion)) return;
            _pendingRequests.Remove(id);
            completion(resultPath);
        }
        
        private sealed class ExtractResult
        {
            internal bool Success;
            internal bool Canceled;
            internal string Folder;
            internal string Error;
        }

        private static readonly SemaphoreSlim UnzipGate = new SemaphoreSlim(1, 1);

        private static ExtractResult ExtractArchive(ZipArchive archive, string destinationFolder, CancellationToken cancellation)
        {
            var result = new ExtractResult { Folder = destinationFolder };
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (Directory.Exists(destinationFolder))
                    Directory.Delete(destinationFolder, true);
                Directory.CreateDirectory(destinationFolder);

                string root = Path.GetFullPath(destinationFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                foreach (var entry in archive.Entries)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                        continue;

                    string destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!destinationPath.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException("ZIP entry escapes destination: " + entry.FullName);

                    string directoryPath = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directoryPath))
                        Directory.CreateDirectory(directoryPath);
                    // ExtractToFile restores the ZIP entry's historical timestamp.
                    // Stream into a newly written file so Unity never observes a
                    // source asset jumping back to its publication date.
                    using (var input = entry.Open())
                    using (var output = File.Create(destinationPath))
                        CopyWithCancellation(input, output, cancellation);
                }
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Canceled = exception is OperationCanceledException;
                result.Error = exception.Message + "\n" + exception.StackTrace;
                try
                {
                    if (Directory.Exists(destinationFolder))
                        Directory.Delete(destinationFolder, true);
                }
                catch { }
            }
            return result;
        }

        private static void CopyWithCancellation(Stream input, Stream output, CancellationToken cancellation)
        {
            var buffer = new byte[64 * 1024];
            for (;;)
            {
                cancellation.ThrowIfCancellationRequested();
                int count = input.Read(buffer, 0, buffer.Length);
                if (count == 0) return;
                cancellation.ThrowIfCancellationRequested();
                output.Write(buffer, 0, count);
            }
        }

        private static ExtractResult ExtractFileOnWorker(string zipFilePath, string destinationFolder, CancellationToken cancellation)
        {
            UnzipGate.Wait(cancellation);
            try
            {
                using (var archive = ZipFile.OpenRead(zipFilePath))
                    return ExtractArchive(archive, destinationFolder, cancellation);
            }
            finally
            {
                UnzipGate.Release();
            }
        }

        private static ExtractResult ExtractBytesOnWorker(byte[] zipData, string destinationFolder, CancellationToken cancellation)
        {
            UnzipGate.Wait(cancellation);
            try
            {
                using (var memoryStream = new MemoryStream(zipData, false))
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                    return ExtractArchive(archive, destinationFolder, cancellation);
            }
            finally
            {
                UnzipGate.Release();
            }
        }

#if UNITY_EDITOR
        private static async Task UnzipInEditor(string id, string zipFilePath, int generation)
        {
            ExtractResult result;
            try
            {
                string destinationFolder = Path.Combine(Path.GetDirectoryName(zipFilePath),
                    Path.GetFileNameWithoutExtension(zipFilePath));
                result = await StartExtraction(generation,
                    cancellation => ExtractFileOnWorker(zipFilePath, destinationFolder, cancellation)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                result = new ExtractResult { Error = exception.ToString() };
            }

            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                if (!IsCurrentSession(generation)) return;
                if (!result.Success)
                    Debug.LogError("[Balancy] Failed to unzip " + id + ": " + result.Error);
                string folder = result.Success ? result.Folder : string.Empty;
                if (!string.IsNullOrEmpty(folder) && !folder.EndsWith("/")) folder += "/";
                NotifyUnzipCompleted(id, folder, generation);
            });
        }
#endif

        private static IEnumerator AwaitExtraction(string id, Task<ExtractResult> task, int generation)
        {
            while (!task.IsCompleted)
            {
                if (!IsCurrentSession(generation)) yield break;
                yield return null;
            }
            if (!IsCurrentSession(generation)) yield break;

            ExtractResult result;
            if (task.IsCanceled)
                result = new ExtractResult { Error = "Extraction task was canceled" };
            else if (task.IsFaulted)
                result = new ExtractResult { Error = task.Exception?.GetBaseException().ToString() ?? "Extraction task failed" };
            else
                result = task.Result;

            if (!result.Success)
                Debug.LogError("[Balancy] Failed to unzip " + id + ": " + result.Error);

            string folder = result.Success ? result.Folder : string.Empty;
            if (!string.IsNullOrEmpty(folder) && !folder.EndsWith("/"))
                folder += "/";
            NotifyUnzipCompleted(id, folder, generation);
        }

        /// <summary>
        /// Read platform data on the Unity thread when required, but perform all
        /// filesystem and decompression work on one bounded worker.
        /// </summary>
        private static IEnumerator UnzipAsync(string id, string zipFilePath, int generation)
        {
            if (!IsCurrentSession(generation)) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (zipFilePath.Contains("/android_asset/"))
            {
                yield return UnzipFromAndroidStreamingAssets(id, zipFilePath, generation);
                yield break;
            }
#endif

            if (!File.Exists(zipFilePath))
            {
                Debug.LogError("[Balancy] ZIP file not found: " + zipFilePath);
                NotifyUnzipCompleted(id, string.Empty, generation);
                yield break;
            }

            string destinationFolder = Path.Combine(
                Path.GetDirectoryName(zipFilePath),
                Path.GetFileNameWithoutExtension(zipFilePath));

            var task = StartExtraction(generation,
                cancellation => ExtractFileOnWorker(zipFilePath, destinationFolder, cancellation));
            yield return AwaitExtraction(id, task, generation);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static IEnumerator UnzipFromAndroidStreamingAssets(string id, string zipFilePath, int generation)
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
                if (!IsCurrentSession(generation)) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[Balancy] Failed to read ZIP from APK: " + request.error + ", URL: " + assetPath);
                    NotifyUnzipCompleted(id, string.Empty, generation);
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

                var task = StartExtraction(generation,
                    cancellation => ExtractBytesOnWorker(zipData, destinationFolder, cancellation));
                yield return AwaitExtraction(id, task, generation);
            }
        }
#endif



        /// <summary>
        /// Notify C++ that unzipping is complete
        /// </summary>
        private static void NotifyUnzipCompleted(string id, string extractedFolderPath, int generation)
        {
            if (!IsCurrentSession(generation)) return;

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
            Task[] workers;
            CancellationTokenSource cancellation;
            lock (ExtractionLock)
            {
                _isStopped = true;
                Interlocked.Increment(ref _generation);
                cancellation = _extractionCancellation;
                _extractionCancellation = null;
                workers = ExtractionTasks.ToArray();
                ExtractionTasks.Clear();
            }
            // Do not release the editor's import guard while any worker can still
            // delete or write an extracted file. Workers never wait on the UI queue.
            try { cancellation?.Cancel(); }
            finally
            {
                try { if (workers.Length != 0) Task.WaitAll(workers); }
                finally
                {
                    cancellation?.Dispose();
                    _pendingRequests.Clear();
                }
            }
        }
    }
}
