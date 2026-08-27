using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Balancy.Dictionaries
{
    public class DataObjectsManager
    {
        private static string CACHE_PATH;
        private static string RESOURCES_PATH;

        enum Status
        {
            None = 0,
            Loading = 1,
            Loaded = 2
        }
        
        [UnityEngine.Scripting.Preserve, StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        internal class SharedObjectInfo
        {
            public int PixelsPerUnit;
            public int OffsetTop;
            public int OffsetBottom;
            public int OffsetRight;
            public int OffsetLeft;
            
            [MarshalAs(UnmanagedType.LPStr)] public string UnnyId;
            [MarshalAs(UnmanagedType.LPStr)] public string LocationPath;
        }

        internal static void Init(string cachePath, string resourcesPath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // For WebGL, cachePath already includes /idbfs/ prefix
            // Don't use Path.Combine as it doesn't work correctly with /idbfs/
            // The path from C++ LocationPath is relative, so we just need the base cache directory
            CACHE_PATH = cachePath + "/";
#else
            CACHE_PATH = Path.Combine(cachePath, "Balancy/Models/");
#endif
            RESOURCES_PATH = resourcesPath;
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();
        }

        private static UnityMainThreadDispatcher _mainThreadInstance;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Synchronous version - returns cached blob URL or null
        [DllImport("__Internal")]
        private static extern IntPtr _balancyReadFileAsBlobUrl(string path);

        // Async version - preloads file from IndexedDB and caches blob URL
        private delegate void PreloadCallback(IntPtr userData, IntPtr blobUrlPtr);

        [DllImport("__Internal")]
        private static extern void _balancyPreloadFileAsBlobUrl(string directory, string fileName,
            PreloadCallback callback, IntPtr userData);

        private static string ReadFileAsBlobUrl(string path)
        {
            IntPtr ptr = _balancyReadFileAsBlobUrl(path);
            if (ptr == IntPtr.Zero)
                return null;

            string blobUrl = Marshal.PtrToStringAnsi(ptr);
            Marshal.FreeHGlobal(ptr);
            return blobUrl;
        }

        // Callback context for async preload
        private class PreloadContext
        {
            public bool Complete;
            public string BlobUrl;
        }

        // Static dictionary to track preload contexts by ID
        private static Dictionary<int, PreloadContext> _preloadContexts = new Dictionary<int, PreloadContext>();
        private static int _nextContextId = 1;

        // Static callback for async preload (required for IL2CPP)
        [AOT.MonoPInvokeCallback(typeof(PreloadCallback))]
        private static void OnFilePreloaded(IntPtr userDataPtr, IntPtr blobUrlPtr)
        {
            int contextId = userDataPtr.ToInt32();

            if (_preloadContexts.TryGetValue(contextId, out var context))
            {
                if (blobUrlPtr != IntPtr.Zero)
                {
                    context.BlobUrl = Marshal.PtrToStringAnsi(blobUrlPtr);
                }
                context.Complete = true;
            }
        }
#endif 

        private abstract class OneObjectBase
        {
            protected SharedObjectInfo _objectInfo = null;
            public Status Status = Status.None;
            
            protected string PathInStorage => CACHE_PATH + _objectInfo?.LocationPath;
            protected string PathInResources => RESOURCES_PATH + _objectInfo?.LocationPath;

            public void ProcessLoadedObject(SharedObjectInfo objectInfo)
            {
                _objectInfo = objectInfo;
                if (objectInfo != null)
                {
                    OnObjectLoaded();
                }
                else
                {
                    OnObjectLoadFailed();
                }
            }

            protected abstract void OnObjectLoaded();
            protected abstract void OnObjectLoadFailed();
        }

        private class OneObjectSprite : OneObjectBase
        {
            private class CallbackInfo
            {
                public Action<UnityEngine.Sprite> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            public Sprite Sprite;

            protected override void OnObjectLoaded()
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // For WebGL: Use UnityWebRequest to load texture asynchronously
                _mainThreadInstance.StartCoroutine(LoadTextureWebGL());
#elif UNITY_ANDROID && !UNITY_EDITOR
                // For Android: Check cache first (sync), then fall back to StreamingAssets via UnityWebRequest
                _mainThreadInstance.StartCoroutine(LoadTextureAndroid());
#else
                // For native platforms: Use synchronous file loading
                LoadTextureNative();
#endif
            }

            private void LoadTextureNative()
            {
                Texture2D texture = TryToLoadTextureFromResources();

                if (texture == null)
                {
                    var path = PathInStorage;
                    if (!File.Exists(path))
                    {
                        path = PathInResources;
                        if (!File.Exists(path))
                        {
                            Debug.Log("NO FILE PATH " + path);
                            SetSprite(null);
                            return;
                        }
                    }
                    byte[] bytes = File.ReadAllBytes(path);
                    texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.LoadImage(bytes);
                }

                CreateSpriteFromTexture(texture);
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            private IEnumerator LoadTextureAndroid()
            {
                // Try Unity Resources first
                Texture2D texture = TryToLoadTextureFromResources();
                if (texture != null)
                {
                    CreateSpriteFromTexture(texture);
                    yield break;
                }

                // Try cache directory (sync file access works on Android for persistentDataPath)
                var cachePath = PathInStorage;
                if (File.Exists(cachePath))
                {
                    byte[] bytes = File.ReadAllBytes(cachePath);
                    texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.LoadImage(bytes);
                    CreateSpriteFromTexture(texture);
                    yield break;
                }

                // Fall back to StreamingAssets via UnityWebRequest
                var resourcesUrl = PathInResources;
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(resourcesUrl))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        texture = DownloadHandlerTexture.GetContent(request);
                        CreateSpriteFromTexture(texture);
                    }
                    else
                    {
                        Debug.LogError($"[Balancy] Android: Failed to load texture from {resourcesUrl}: {request.error}");
                        SetSprite(null);
                    }
                }
            }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            private IEnumerator LoadTextureWebGL()
            {
                //Debug.Log("**==>> [WebGL] Loading texture from: " + _objectInfo.LocationPath);

                // Try loading from Resources first
                Texture2D texture = TryToLoadTextureFromResources();
                if (texture != null)
                {
                    //Debug.Log("**==>> [WebGL] Texture loaded from Resources");
                    CreateSpriteFromTexture(texture);
                    yield break;
                }

                // PathInStorage format: /idbfs/{hash}/{fileName}
                // We need to split into directory and fileName for IndexedDB
                string fullPath = PathInStorage;
                //Debug.Log("**==>> [WebGL] Full path: " + fullPath);

                // Split path: directory is everything up to last '/', fileName is the rest
                int lastSlash = fullPath.LastIndexOf('/');
                if (lastSlash < 0)
                {
                    Debug.LogError("**==>> [WebGL] Invalid path format: " + fullPath);
                    SetSprite(null);
                    yield break;
                }

                string directory = fullPath.Substring(0, lastSlash);
                string fileName = fullPath.Substring(lastSlash + 1);

                //Debug.Log("**==>> [WebGL] Directory: " + directory);
                //Debug.Log("**==>> [WebGL] FileName: " + fileName);

                // First check if already cached
                string blobUrl = DataObjectsManager.ReadFileAsBlobUrl(fullPath);
                if (!string.IsNullOrEmpty(blobUrl))
                {
                    //Debug.Log("**==>> [WebGL] Using cached blob URL: " + blobUrl);
                }
                else
                {
                    // Need to preload from IndexedDB first
                    //Debug.Log("**==>> [WebGL] Preloading file from IndexedDB...");

                    // Create context with unique ID
                    int contextId = DataObjectsManager._nextContextId++;
                    var context = new DataObjectsManager.PreloadContext
                    {
                        Complete = false,
                        BlobUrl = null
                    };
                    DataObjectsManager._preloadContexts[contextId] = context;

                    // Call async preload with static callback
                    _balancyPreloadFileAsBlobUrl(directory, fileName,
                        DataObjectsManager.OnFilePreloaded, new IntPtr(contextId));

                    // Wait for preload to complete
                    float timeout = 5f;
                    float elapsed = 0f;
                    while (!context.Complete && elapsed < timeout)
                    {
                        yield return null;
                        elapsed += Time.deltaTime;
                    }

                    if (!context.Complete)
                    {
                        Debug.LogError("**==>> [WebGL] Preload timeout after " + timeout + " seconds");
                        DataObjectsManager._preloadContexts.Remove(contextId);
                        SetSprite(null);
                        yield break;
                    }

                    blobUrl = context.BlobUrl;
                    DataObjectsManager._preloadContexts.Remove(contextId);

                    if (!string.IsNullOrEmpty(blobUrl))
                    {
                        //Debug.Log("**==>> [WebGL] Preload complete, blob URL: " + blobUrl);
                    }
                    else
                    {
                        Debug.LogError("**==>> [WebGL] Preload failed - file not found in IndexedDB");
                    }
                }

                if (string.IsNullOrEmpty(blobUrl))
                {
                    Debug.LogError("**==>> [WebGL] Failed to get blob URL for: " + fullPath);
                    SetSprite(null);
                    yield break;
                }

                //Debug.Log("**==>> [WebGL] Loading texture from blob URL: " + blobUrl);

                // Use UnityWebRequest to load texture from blob URL
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(blobUrl))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        texture = DownloadHandlerTexture.GetContent(request);
                        //Debug.Log("**==>> [WebGL] Texture loaded successfully, size: " + texture.width + "x" + texture.height);
                        CreateSpriteFromTexture(texture);
                    }
                    else
                    {
                        Debug.LogError("**==>> [WebGL] Failed to load texture from blob URL: " + request.error);
                        SetSprite(null);
                    }
                }
            }
#endif

            private void CreateSpriteFromTexture(Texture2D texture)
            {
                if (texture == null)
                {
                    SetSprite(null);
                    return;
                }

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), _objectInfo.PixelsPerUnit, 0,
                    SpriteMeshType.FullRect,
                    new Vector4(_objectInfo.OffsetLeft, _objectInfo.OffsetBottom, _objectInfo.OffsetRight, _objectInfo.OffsetTop));
                SetSprite(sprite);
            }

            protected override void OnObjectLoadFailed()
            {
                SetSprite(null);
            }

            private Texture2D TryToLoadTextureFromResources()
            {
                var resourcesPath = _objectInfo.LocationPath.Replace('/', '-');
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resourcesPath);
                return Resources.Load<Texture2D>(fileNameWithoutExtension);
            }
            
            private void SetSprite(Sprite sprite)
            {
                Sprite = sprite;
                Status = Sprite != null ? Status.Loaded : Status.None;

                foreach (var info in _callbacks)
                {
                    if (info.LoadHandler.GetStatus() == AsyncLoadHandler.Status.Loading)
                    {
                        info.LoadHandler.Finish();
                        info.Callback?.Invoke(Sprite);
                    }
                }
                
                _callbacks.Clear();
            }

            public void AddCallback(AsyncLoadHandler handler, Action<UnityEngine.Sprite> callback)
            {
                var info = new CallbackInfo
                {
                    LoadHandler = handler,
                    Callback = callback
                };
                _callbacks.Add(info);
            }
        }

        private class OneObjectPath : OneObjectBase
        {
            private class CallbackInfo
            {
                public Action<string> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            public string FilePath;

            protected override void OnObjectLoaded()
            {
                if (File.Exists(PathInStorage))
                    SetPath(PathInStorage);
                else
                {
                    if (File.Exists(PathInResources))
                        SetPath(PathInResources);
                    else
                        SetPath(null);
                }
            }

            protected override void OnObjectLoadFailed()
            {
                SetPath(null);
            }

            private void SetPath(string path)
            {
                FilePath = path;
                Status = !string.IsNullOrEmpty(FilePath) ? Status.Loaded : Status.None;

                foreach (var info in _callbacks)
                {
                    if (info.LoadHandler.GetStatus() == AsyncLoadHandler.Status.Loading)
                    {
                        info.LoadHandler.Finish();
                        info.Callback?.Invoke(FilePath);
                    }
                }
                
                _callbacks.Clear();
            }

            public void AddCallback(AsyncLoadHandler handler, Action<string> callback)
            {
                var info = new CallbackInfo
                {
                    LoadHandler = handler,
                    Callback = callback
                };
                _callbacks.Add(info);
            }
        }
        
        private class OneObjectView
        {
            private class CallbackInfo
            {
                public Action<string> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            public bool Loaded = false;
            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            private string Path;

            public string PathInStorage => Path;

            public void SetPath(string path)
            {
                Path = path;
                Loaded = true;

                foreach (var info in _callbacks)
                {
                    if (info.LoadHandler.GetStatus() == AsyncLoadHandler.Status.Loading)
                    {
                        info.LoadHandler.Finish();
                        info.Callback?.Invoke(PathInStorage);
                    }
                }
                
                _callbacks.Clear();
            }

            public void AddCallback(AsyncLoadHandler handler, Action<string> callback)
            {
                var info = new CallbackInfo
                {
                    LoadHandler = handler,
                    Callback = callback
                };
                _callbacks.Add(info);
            }
        }

        private static readonly Dictionary<string, OneObjectBase> AllObjects = new Dictionary<string, OneObjectBase>();
        private static readonly Dictionary<string, OneObjectView> AllViews = new Dictionary<string, OneObjectView>();
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Models.DataObjectWasCachedCallback))]
        private static void DataObjectLoaded(string id, IntPtr ptr)
        {
            try
            {
                if (ptr == IntPtr.Zero)
                {
                    if (AllObjects.TryGetValue(id, out var oneObject))
                    {
                        _mainThreadInstance.Enqueue(() => { oneObject.ProcessLoadedObject(null); });
                    } else
                        Debug.Log("No request object found " + id);
                }
                else
                {
                    if (AllObjects.TryGetValue(id, out var oneObject))
                    {
                        // Unified approach: ptr is always a pointer to a JSON string (all platforms)
                        string jsonData = Marshal.PtrToStringAnsi(ptr);

                        // Parse JSON to SharedObjectInfo
                        var sharedObject = JsonUtility.FromJson<SharedObjectInfo>(jsonData);
                        _mainThreadInstance.Enqueue(() => { oneObject.ProcessLoadedObject(sharedObject); });
                    }
                    else
                        Debug.Log("No request object found " + id);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        
        public static AsyncLoadHandler GetSprite(string id, Action<UnityEngine.Sprite> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllObjects.TryGetValue(id, out var oneObject))
            {
                var oneObjectSprite = new OneObjectSprite();
                oneObjectSprite.AddCallback(handler, callback);
                AllObjects.Add(id, oneObjectSprite);
                oneObject = oneObjectSprite;
            }
            else
            {
                if (oneObject is OneObjectSprite sprite)
                {
                    if (sprite.Status == Status.Loaded)
                    {
                        handler.Finish();
                        callback?.Invoke(sprite.Sprite);
                    }
                    else
                    {
                        sprite.AddCallback(handler, callback);
                    }
                }
                else
                {
                    Debug.LogError($"Object {id} is not a sprite type");
                    handler.Finish();
                    callback?.Invoke(null);
                    return handler;
                }
            }

            if (oneObject.Status == Status.None && !handler.IsFinished())
            {
                oneObject.Status = Status.Loading;
                LibraryMethods.Models.balancyDataObjectLoad(id, DataObjectLoaded);
            }

            return handler;
        }

        public static AsyncLoadHandler GetObject(string id, Action<string> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllObjects.TryGetValue(id, out var oneObject))
            {
                var oneObjectPath = new OneObjectPath();
                oneObjectPath.AddCallback(handler, callback);
                AllObjects.Add(id, oneObjectPath);
                oneObject = oneObjectPath;
            }
            else
            {
                if (oneObject is OneObjectPath pathObject)
                {
                    if (pathObject.Status == Status.Loaded)
                    {
                        handler.Finish();
                        callback?.Invoke(pathObject.FilePath);
                    }
                    else
                    {
                        pathObject.AddCallback(handler, callback);
                    }
                }
                else
                {
                    Debug.LogError($"Object {id} is not a path type");
                    handler.Finish();
                    callback?.Invoke(null);
                    return handler;
                }
            }

            if (oneObject.Status == Status.None && !handler.IsFinished())
            {
                oneObject.Status = Status.Loading;
                LibraryMethods.Models.balancyDataObjectLoad(id, DataObjectLoaded);
            }

            return handler;
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Models.DataObjectWasCachedCallback))]
        private static void DataObjectViewLoaded(string id, string path)
        {
            if (AllViews.TryGetValue(id, out var oneObjectView))
            {
                if (string.IsNullOrEmpty(path))
                    AllViews.Remove(id);
                _mainThreadInstance.Enqueue(() => { oneObjectView.SetPath(path); });
            }
            else
                Debug.Log("No request object view found " + id);
        }
        
        public static AsyncLoadHandler GetObjectView(string id, Action<string> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllViews.TryGetValue(id, out var oneObjectView))
            {
                oneObjectView = new OneObjectView();
                oneObjectView.AddCallback(handler, callback);
                AllViews.Add(id, oneObjectView);
                
                LibraryMethods.Models.balancyDataObjectViewPreload(id, DataObjectViewLoaded);
            }
            else
            {
                if (oneObjectView.Loaded)
                {
                    handler.Finish();
                    callback?.Invoke(oneObjectView.PathInStorage);
                }
                else
                {
                    oneObjectView.AddCallback(handler, callback);
                }
            }

            return handler;
        }

        /// <summary>
        /// Drops the memoized paths of already-resolved views. Call this after a CMS /
        /// dictionary update: GetObjectView caches resolved views in AllViews and, on a
        /// repeat open, returns the cached path WITHOUT re-running the preload — so a
        /// re-versioned script would never be re-downloaded and the recompile-before-open
        /// would compile from a disk still missing it, reproducing the original crash.
        /// Removing the completed entries forces the next open to preload again (which
        /// re-downloads changed scripts). In-flight (not-yet-Loaded) entries are left intact
        /// so their pending opens still complete (DataObjectViewLoaded looks them up by id).
        /// </summary>
        internal static void InvalidateLoadedViews()
        {
            var staleIds = new List<string>();
            foreach (var kvp in AllViews)
            {
                if (kvp.Value.Loaded)
                    staleIds.Add(kvp.Key);
            }
            foreach (var id in staleIds)
                AllViews.Remove(id);
        }

        internal static void ClearFromMemory(string id)
        {
            if (AllObjects.TryGetValue(id, out var oneObject))
            {
                if (oneObject is OneObjectSprite sprite && sprite.Sprite != null)
                {
                    Object.Destroy(sprite.Sprite.texture);
                    Object.Destroy(sprite.Sprite);
                }
                AllObjects.Remove(id);
            }
        }

        internal static void CleanUp()
        {
            foreach (var kvp in AllObjects)
            {
                if (kvp.Value is OneObjectSprite sprite && sprite.Sprite != null)
                {
                    Object.Destroy(sprite.Sprite.texture);
                    Object.Destroy(sprite.Sprite);
                }
            }
            AllObjects.Clear();
            AllViews.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
            _preloadContexts.Clear();
#endif
            lock (_preloadLock)
            {
                _pendingPreloadCallbacks.Clear();
                _preloadWaitRegistered = false;
            }
        }
        
        internal static void ClearFromDisk(string id)
        {
            LibraryMethods.Models.balancyDataObjectDeleteFromDisk(id);
            ClearFromMemory(id);
        }

        /// <summary>
        /// Checks whether a data object's file is already cached on disk.
        /// </summary>
        public static bool IsCached(string id)
        {
            return LibraryMethods.Models.balancyDataObjectIsCached(id) != 0;
        }

        /// <summary>
        /// Returns true if any data object downloads are currently in progress.
        /// </summary>
        public static bool IsPreloadingInProgress()
        {
            return LibraryMethods.Models.balancyIsPreloadingInProgress() != 0;
        }

        private static readonly List<Action> _pendingPreloadCallbacks = new List<Action>();
        private static readonly object _preloadLock = new object();
        private static bool _preloadWaitRegistered;

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Models.PreloadCompleteCallback))]
        private static void OnPreloadingComplete()
        {
            Action[] callbacks;
            lock (_preloadLock)
            {
                callbacks = _pendingPreloadCallbacks.ToArray();
                _pendingPreloadCallbacks.Clear();
                _preloadWaitRegistered = false;
            }

            UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
            {
                foreach (var callback in callbacks)
                {
                    try
                    {
                        callback?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            });
        }

        /// <summary>
        /// Invokes the callback when all pending data object downloads complete.
        /// If nothing is pending, the callback fires immediately.
        /// </summary>
        public static void WaitForPreloading(Action onComplete)
        {
            if (!Controller.IsNativeInitialized)
            {
                Debug.LogError("[Balancy] WaitForPreloading ignored: SDK is not initialized");
                return;
            }

            lock (_preloadLock)
            {
                if (onComplete != null)
                    _pendingPreloadCallbacks.Add(onComplete);
                if (_preloadWaitRegistered)
                    return;
                _preloadWaitRegistered = true;
            }
            LibraryMethods.Models.balancyWaitForPreloading(OnPreloadingComplete);
        }
    }
}
