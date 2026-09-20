using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using Balancy.WebView;
using UnityEngine;

namespace Balancy
{
    public class RenderViewsManager
    {
        private const int DEFAULT_OWNER_DEPTH = 10;
#if UNITY_EDITOR
        // private const bool UseEmbeddedWebView = true;
        private const bool UseEmbeddedWebView = false;
#else
        private const bool UseEmbeddedWebView = false;
#endif

        internal static Func<string, bool> _onMessageReceived;

        private static BalancyWebView _webView;
        private static bool _prepareRequested, _dataAvailable, _scriptsLoaded;
        private static Action _pendingPrepared;
        private static Action<string> _pendingPrepareFailed;
        public sealed class ScriptsBundleInfo
        {
            public string Path;
            public string Version;
        }

        // Kept separate from transport so readiness/failure paths can be tested without native code.
        // Combined bundles stay on disk; only legacy projects materialize a multi-megabyte C# string.
        internal static Func<ScriptsBundleInfo> ReadScriptsBundleInfo = () => {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The browser build keeps SDK files in Emscripten virtual filesystem and
            // passes script source into its iframe. A native path is not a browser URL,
            // so preserve that transport until WebGL exposes a blob URL.
            return null;
#else
            var pathPtr = LibraryMethods.General.balancyDataObjectGetCombinedScriptsPath();
            var versionPtr = LibraryMethods.General.balancyDataObjectGetCombinedScriptsVersion();
            string path = pathPtr == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(pathPtr) ?? "";
            string version = versionPtr == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(versionPtr) ?? "";
            return string.IsNullOrEmpty(path) || string.IsNullOrEmpty(version)
                ? null : new ScriptsBundleInfo { Path = path, Version = version };
#endif
        };
        internal static Func<string> ReadScripts = () => {
            var ptr = LibraryMethods.General.balancyDataObjectCompileAllScripts();
            if (ptr == IntPtr.Zero) throw new InvalidOperationException("Script bundle is unavailable");
            return Marshal.PtrToStringAnsi(ptr) ?? "";
        };

        internal static void Init()
        {
            LibraryMethods.General.balancySetDataRequestedCallback(DataRequested);
            LibraryMethods.General.balancyViewAllowOptimization(true);
            LibraryMethods.General.balancySetViewNotificationsCallback(OnNotificationReceived);
            PrepareCallbacks();

            BalancyWebView.Instance.OnMessage = OnMessageReceived;
            _webView = BalancyWebView.Instance;
            _webView.OnLoadCompleted += HandleLoadCompleted;
            _webView.OnClosed += HandleWebViewClosed;
            _webView.OnViewReleased += HandleViewReleased;

            _webView.SetTransparentBackground(true);
            _webView.SetFullScreen(true);
            //_webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
            //_webView.SetDebugLogging(true);

            SetViewDelays(0.03f, 0.08f);
            TryPrepareRequestedWebView();
        }

        internal static void CleanUp()
        {
            try
            {
                LibraryMethods.General.balancySetDataRequestedCallback(null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            try
            {
                LibraryMethods.General.balancySetViewNotificationsCallback(null);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            CleanUpManagedState();
        }

        private static void CleanUpManagedState()
        {
            Balancy.Callbacks.OnOfferDeactivated -= HandleOfferDeactivated;
            Balancy.Callbacks.OnOfferGroupDeactivated -= HandleOfferGroupDeactivated;
            Balancy.Callbacks.OnEventDeactivated -= HandleEventDeactivated;
            Balancy.Callbacks.OnLocalizationChanged -= HandleLocalizationChanged;
            Balancy.Callbacks.OnDataUpdated -= HandleContentUpdated;

            if (_webView != null)
            {
                _webView.OnMessage -= OnMessageReceived;
                _webView.OnLoadCompleted -= HandleLoadCompleted;
                _webView.OnClosed -= HandleWebViewClosed;
                _webView.OnViewReleased -= HandleViewReleased;
                if (_webView.IsPersistentModeEnabled()) _webView.CloseWebView();
            }

            _prepareRequested = _dataAvailable = _scriptsLoaded = false;
            _pendingPrepared = null; _pendingPrepareFailed = null;
            ViewOwners.Clear();
            _onMessageReceived = null;
            m_LastOpenedOwnerPtr = IntPtr.Zero;
            _webView = null;
        }

        /// <summary>Opt-in timing summaries in Unity logs. Enable before Prepare/Open.</summary>
        public static void SetPerformanceLogging(bool enabled)
        {
            BalancyWebView.PerformanceLoggingEnabled = enabled;
            if (_webView != null && _webView.IsPersistentModeEnabled())
                _webView.SendMessageToWebView("{\"type\":\"setPerformanceLogging\",\"enabled\":" + (enabled ? "true" : "false") + "}");
        }

        /// <summary>Read the prepared script bundle (or legacy compile result) from the native core.</summary>
        public static void RefreshScripts() => TryRefreshScripts();

        private static bool TryRefreshScripts()
        {
            var started = BalancyWebView.PerformanceNow();
            try
            {
                if (_webView == null) return false;
                var bundle = ReadScriptsBundleInfo();
                if (bundle != null)
                {
                    _webView.SetScriptsFile(bundle.Path, bundle.Version);
                    Debug.Log($"[RenderViewsManager] Scripts bundle ready: version={bundle.Version} path={bundle.Path}");
                    BalancyWebView.PerformanceLog("resolveScriptsBundle", started, null,
                        "mode=file version=" + bundle.Version + " pathChars=" + bundle.Path.Length);
                }
                else
                {
                    string scriptsCode = ReadScripts();
                    Debug.Log($"[RenderViewsManager] Legacy scripts compiled: {scriptsCode.Length} characters");
                    _webView.SetScriptsCode(scriptsCode);
                    BalancyWebView.PerformanceLog("readScriptsBundle", started, null,
                        "mode=legacy scriptChars=" + scriptsCode.Length);
                }
                _scriptsLoaded = true;
                return true;
            }
            catch (Exception e)
            {
                // Preserve the last usable snapshot; the next data update or explicit
                // Prepare retries. Never destroy an active view on a failed read.
                Debug.LogError($"[RenderViewsManager] Failed to compile scripts, keeping previous bundle: {e.Message}");
                if (!_scriptsLoaded && _webView != null && _webView.IsPersistentModeEnabled())
                    _webView.CloseWebView();
                var failed = _pendingPrepareFailed;
                _pendingPrepared = null; _pendingPrepareFailed = null;
                failed?.Invoke(e.Message);
                return false;
            }
        }

        private static IntPtr m_LastOpenedOwnerPtr = IntPtr.Zero;
        private static readonly Dictionary<string, IntPtr> ViewOwners = new Dictionary<string, IntPtr>();
        private static void HandleViewReleased(string id) => ViewOwners.Remove(id);
        private static void HandleLocalizationChanged(string code) => _webView?.InvalidateCache(true);
        internal static void HandleContentUpdated(Callbacks.DataUpdatedStatus status)
        {
            HandleContentUpdatedAndWait(status, null);
        }

        internal static void HandleContentUpdatedAndWait(Callbacks.DataUpdatedStatus status, Action onReady)
        {
            // If the client opted into persistent mode before the first local snapshot,
            // finish the one-time WebView engine startup before publishing OnDataUpdated.
            // Android may pause Unity while creating its first WebView; publishing first
            // would move that visible pause into gameplay.
            bool waitForInitialPersistentView = !status.IsCloudSynced && _prepareRequested &&
                _webView != null;
            bool completed = false;
            Action completeOnce = () =>
            {
                if (completed) return;
                completed = true;
                onReady?.Invoke();
            };
            if (waitForInitialPersistentView)
            {
                _pendingPrepared += completeOnce;
                _pendingPrepareFailed += _ => completeOnce();
            }

            _dataAvailable = true;
            if (status.IsCMSUpdated) _webView?.InvalidateCache();
            if (_prepareRequested)
            {
                // Data-object scripts can change even when the dictionaries-only CMS flag
                // is false. Resolve the cheap path/version descriptor on every update;
                // SetScriptsFile restarts the shell only when that version actually changed.
                if (!TryRefreshScripts()) return;
                TryPrepareRequestedWebView();
            }
            else _scriptsLoaded = false; // Classic/direct URL opens read lazily after updates.

            if (!waitForInitialPersistentView) completeOnce();
        }

        private static void TryPrepareRequestedWebView()
        {
            if (!_prepareRequested || _webView == null) return;
            if (!_dataAvailable)
            {
                // Creating the first native WebView is expensive on Android. Start an
                // empty shell as soon as the client opts in, while StreamingAssets are
                // still being prepared. SetScriptsCode requests a shell replacement
                // when the local snapshot arrives; PersistentViewState keeps the public
                // preparation callback pending until that script-backed shell is ready.
                if (!_webView.IsPersistentModeEnabled())
                    _webView.PrepareWebView(null, error =>
                        Debug.LogWarning("[RenderViewsManager] WebView prewarm failed; retrying with data: " + error));
                return;
            }
            if (!_scriptsLoaded && !TryRefreshScripts()) return;
            var ready = _pendingPrepared; var failed = _pendingPrepareFailed;
            _pendingPrepared = null; _pendingPrepareFailed = null;
            _webView.PrepareWebView(ready, failed);
        }
        // JsonUtility traverses the serialized type graph, even for shallow JSON.
        // Batch members must not contain another batch array.
        [Serializable] private class BridgeRequestItem { public string type, id, viewId; }
        [Serializable] private class BridgeRequest : BridgeRequestItem { public BridgeRequestItem[] requests; }
        // Native retains this function pointer until its asynchronous response arrives.
        // Keep a static, AOT-compatible callback; never pass a capturing lambda here.
        private static readonly LibraryMethods.General.WebviewRequestCallback CoreResponseCallback = OnMessageResponseReceived;

        private static BridgeRequestItem[] ParseBridgeRequests(string requestData)
        {
            var request = JsonUtility.FromJson<BridgeRequest>(requestData);
            if (request == null) throw new FormatException("Invalid request");
            var requests = request.type == "batch" ? request.requests : new BridgeRequestItem[] { request };
            if (requests == null || requests.Length == 0) throw new FormatException("Invalid request batch");
            foreach (var item in requests)
                // Unity can deserialize a null array member as an empty inline object.
                if (item == null || item.type == "batch" || (request.type == "batch" && string.IsNullOrEmpty(item.id)))
                    throw new FormatException("Invalid request batch member");
            return requests;
        }
        [Serializable] private class BridgeError { public string type = "response"; public string id, error; }
        private static string RequestError(string requestData, string error)
        {
            try {
                var request = JsonUtility.FromJson<BridgeRequest>(requestData);
                if (request.type == "batch" && request.requests != null) {
                    var responses = new List<string>();
                    foreach (var item in request.requests) responses.Add(JsonUtility.ToJson(new BridgeError { id = item?.id, error = error }));
                    return "{\"type\":\"batch-response\",\"responses\":[" + string.Join(",", responses) + "]}";
                }
                return JsonUtility.ToJson(new BridgeError { id = request.id, error = error });
            } catch { return JsonUtility.ToJson(new BridgeError { error = error }); }
        }

        private static void PrepareCallbacks()
        {
            Balancy.Callbacks.OnOfferDeactivated -= HandleOfferDeactivated;
            Balancy.Callbacks.OnOfferDeactivated += HandleOfferDeactivated;
            
            Balancy.Callbacks.OnOfferGroupDeactivated -= HandleOfferGroupDeactivated;
            Balancy.Callbacks.OnOfferGroupDeactivated += HandleOfferGroupDeactivated;
            
            Balancy.Callbacks.OnEventDeactivated -= HandleEventDeactivated;
            Balancy.Callbacks.OnEventDeactivated += HandleEventDeactivated;
            Balancy.Callbacks.OnLocalizationChanged -= HandleLocalizationChanged;
            Balancy.Callbacks.OnLocalizationChanged += HandleLocalizationChanged;
            Balancy.Callbacks.OnDataUpdated -= HandleContentUpdated;
            // Controller invokes HandleContentUpdated before public subscribers.
        }

        private static void HandleEventDeactivated(EventInfo eventInfo)
        {
            if (eventInfo.GameEvent?.ManualRemove ?? false)
                return;
            
            CheckForClosing(eventInfo);
        }

        private static void HandleOfferGroupDeactivated(OfferGroupInfo offerGroupInfo)
        {
            CheckForClosing(offerGroupInfo);
        }

        private static void HandleOfferDeactivated(OfferInfo offerInfo, bool wasPurchased)
        {
            CheckForClosing(offerInfo);
        }

        private static void CheckForClosing(JsonBasedObject deactivatedOwner)
        {
            if (m_LastOpenedOwnerPtr != IntPtr.Zero &&
                m_LastOpenedOwnerPtr == deactivatedOwner.GetRawPointer())
            {
                m_LastOpenedOwnerPtr = IntPtr.Zero;
                CloseView();
            }
        }

        internal static void OnProfileUpdated()
        {
            ViewOwners.Clear();
            _webView?.InvalidateCache();
            // Profile was recreated — all smart object pointers (offers, events, etc.)
            // are now invalid. Close the view (it may be showing stale data) and null
            // the cached owner pointer so we don't send a dangling pointer to C++.
            m_LastOpenedOwnerPtr = IntPtr.Zero;
            CloseView();
        }
        
        private static void HandleWebViewClosed()
        {
            m_LastOpenedOwnerPtr = IntPtr.Zero;
        }

        private static void HandleLoadCompleted(bool obj)
        {
            
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.WebviewRequestCallback))]
        private static void OnNotificationReceived(string notification)
        {
            try
            {
                _webView?.SendMessageToWebView(notification);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
        
        internal static void SendMessageToView(string message)
        {
            _webView?.SendMessageToWebView(message);
        }

        private static bool UsePersistentWebViewForLocalViews()
        {
            return _webView != null && _webView.IsPersistentModeEnabled();
        }

        private static string BuildAdditionalInfo(JsonBasedObject owner)
        {
            long launchTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string additionalInfo = $"{{\"launchTime\":{launchTime}}}";

            if (owner is IOwnerWithTimer ownerWithTimer)
            {
                int secondsLeft = ownerWithTimer.GetSecondsLeftBeforeDeactivation();
                if (secondsLeft > 0)
                    additionalInfo = $"{{\"launchTime\":{launchTime},\"secondsLeft\":{secondsLeft}}}";
            }

            if (BalancyWebView.PerformanceLoggingEnabled) additionalInfo = additionalInfo.Substring(0, additionalInfo.Length - 1) + ",\"performanceLogging\":true}";
            return additionalInfo;
        }

        private static string NormalizeLocalPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;

            return BalancyWebView.TryGetLocalFilePath(filePath, out var physicalPath)
                ? physicalPath
                : filePath;
        }

        internal enum LocalViewStorage
        {
            PhysicalFile,
            Cache,
            Resources
        }

        internal readonly struct LocalViewLocation
        {
            internal readonly LocalViewStorage Storage;
            internal readonly string Path;

            internal LocalViewLocation(LocalViewStorage storage, string path)
            {
                Storage = storage;
                Path = path;
            }
        }

        internal static LocalViewLocation ResolveLocalViewLocation(string filePath, string persistentDataPath,
            string streamingAssetsPath, RuntimePlatform platform)
        {
            string normalized = NormalizeLocalPath(filePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized))
                return new LocalViewLocation(LocalViewStorage.PhysicalFile, normalized);

            string persistentRoot = (persistentDataPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            string modelsRoot = persistentRoot + "/Balancy/Models/";
            bool usesVirtualFileStorage = platform == RuntimePlatform.Android ||
                platform == RuntimePlatform.WebGLPlayer;
            if (usesVirtualFileStorage && !string.IsNullOrEmpty(persistentRoot) &&
                normalized.StartsWith(modelsRoot, StringComparison.Ordinal))
                return new LocalViewLocation(LocalViewStorage.Cache, normalized.Substring(modelsRoot.Length));

            // Unity WebGL's native helper uses Application.persistentDataPath itself
            // as the IndexedDB root (without the desktop /Balancy/Models suffix).
            string persistentPrefix = persistentRoot + "/";
            if (platform == RuntimePlatform.WebGLPlayer && !string.IsNullOrEmpty(persistentRoot) &&
                normalized.StartsWith(persistentPrefix, StringComparison.Ordinal))
                return new LocalViewLocation(LocalViewStorage.Cache, normalized.Substring(persistentPrefix.Length));

            const string androidAssets = "/android_asset/Balancy/";
            int androidAssetsIndex = normalized.IndexOf(androidAssets, StringComparison.Ordinal);
            if (androidAssetsIndex >= 0)
                return new LocalViewLocation(LocalViewStorage.Resources,
                    normalized.Substring(androidAssetsIndex + androidAssets.Length));

            string streamingRoot = (streamingAssetsPath ?? string.Empty).Replace('\\', '/').TrimEnd('/') + "/Balancy/";
            if (usesVirtualFileStorage && !string.IsNullOrEmpty(streamingAssetsPath) &&
                normalized.StartsWith(streamingRoot, StringComparison.Ordinal))
                return new LocalViewLocation(LocalViewStorage.Resources, normalized.Substring(streamingRoot.Length));

            const string browserStreamingAssets = "/StreamingAssets/Balancy/";
            int streamingIndex = normalized.IndexOf(browserStreamingAssets, StringComparison.Ordinal);
            if (platform == RuntimePlatform.WebGLPlayer && streamingIndex >= 0)
                return new LocalViewLocation(LocalViewStorage.Resources,
                    normalized.Substring(streamingIndex + browserStreamingAssets.Length));

            // Older WebGL callbacks can contain an IDB prefix we do not know in
            // managed code. Preserve the complete <game>_Cache segment instead
            // of cutting at "Cache/" and losing the game identifier.
            int cacheMarker = normalized.IndexOf("_Cache/", StringComparison.Ordinal);
            if (platform == RuntimePlatform.WebGLPlayer && cacheMarker >= 0)
            {
                int segmentStart = normalized.LastIndexOf('/', cacheMarker);
                return new LocalViewLocation(LocalViewStorage.Cache,
                    normalized.Substring(segmentStart >= 0 ? segmentStart + 1 : 0));
            }

            return new LocalViewLocation(LocalViewStorage.PhysicalFile, normalized);
        }

        private static bool TryLoadLocalViewText(string filePath, out string content, out LocalViewLocation location)
        {
            location = ResolveLocalViewLocation(filePath, Application.persistentDataPath,
                Application.streamingAssetsPath, Application.platform);
#if (UNITY_WEBGL || UNITY_ANDROID) && !UNITY_EDITOR
            if (location.Storage != LocalViewStorage.PhysicalFile)
            {
                IntPtr contentPtr = LibraryMethods.General.balancyLoadFileContent(
                    location.Path, location.Storage == LocalViewStorage.Resources ? 1 : 0);
                content = Marshal.PtrToStringAnsi(contentPtr);
                return !string.IsNullOrEmpty(content);
            }
#endif
            if (!File.Exists(location.Path))
            {
                content = null;
                return false;
            }
            content = File.ReadAllText(location.Path);
            return !string.IsNullOrEmpty(content);
        }

        private static string ReplaceViewFileName(string relativePath, string fileName)
        {
            int separator = relativePath.LastIndexOf('/');
            return separator >= 0 ? relativePath.Substring(0, separator + 1) + fileName : fileName;
        }

        public static void PrepareWebView(Action onReady = null, Action<string> onFailed = null)
        {
            Debug.Log("[RenderViewsManager] PrepareWebView requested");
            _prepareRequested = true;
            _pendingPrepared += onReady; _pendingPrepareFailed += onFailed;
            TryPrepareRequestedWebView();
        }

        public static void ShowWebView()
        {
            _webView?.ShowWebView();
        }

        public static void HideWebView()
        {
            _webView?.HideWebView();
        }

        public static void OpenLocalView(string filePath, JsonBasedObject owner, Action onShown = null, Action<ViewOpenError> onFailed = null)
        {
            var openStarted = BalancyWebView.PerformanceNow();
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("File path is null or empty");
                onFailed?.Invoke(ViewOpenError.ViewNotFound);
                return;
            }

            if (_prepareRequested)
            {
                TryPrepareRequestedWebView();
                if (!UsePersistentWebViewForLocalViews())
                {
                    onFailed?.Invoke(ViewOpenError.LoadFailed);
                    return;
                }
            }
            else RefreshScripts();

            Debug.Log($"[RenderViewsManager] OpenLocalView requested. Persistent={UsePersistentWebViewForLocalViews()} Path={filePath}");

            if (UsePersistentWebViewForLocalViews())
            {
                if (!_webView.CanShowPersistentView())
                {
                    onFailed?.Invoke(ViewOpenError.AlreadyOpened);
                    return;
                }
                try
                {
                    var htmlStarted = BalancyWebView.PerformanceNow();
                    if (!TryLoadLocalViewText(filePath, out string htmlContent, out _))
                    {
                        onFailed?.Invoke(ViewOpenError.FileNotFound);
                        return;
                    }
#if UNITY_WEBGL && !UNITY_EDITOR
                    string baseUrl = null; // Browser resources use the WebGL cache/blob URL mapping.
#else
                    string baseUrl = BalancyWebView.ToWebViewUrl(NormalizeLocalPath(filePath));
#endif
                    BalancyWebView.PerformanceLog("readViewHtml", htmlStarted, null, "htmlChars=" + htmlContent.Length);
                    string ownerJson = owner?.ToJsonString(DEFAULT_OWNER_DEPTH, false) ?? "";
                    if (_webView.ShowView(htmlContent, ownerJson, BuildAdditionalInfo(owner), () => {
                            BalancyWebView.PerformanceLog("openLocalToReady", openStarted, _webView.CurrentViewId);
                            onShown?.Invoke();
                        },
                        error => onFailed?.Invoke(ViewOpenError.LoadFailed), baseUrl))
                    {
                        m_LastOpenedOwnerPtr = owner?.GetRawPointer() ?? IntPtr.Zero;
                        ViewOwners[_webView.CurrentViewId] = m_LastOpenedOwnerPtr;
                        BalancyWebView.PerformanceLog("openLocalAccepted", openStarted, _webView.CurrentViewId, "file=" + Path.GetFileName(filePath));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[RenderViewsManager] Failed to load persistent HTML: " + e.Message);
                    onFailed?.Invoke(ViewOpenError.LoadFailed);
                }
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Load HTML content from cache instead of using file:// URLs
            if (OpenLocalViewWebGL(filePath, owner, onFailed))
                onShown?.Invoke();
#else
            string fileUrl = BalancyWebView.ToWebViewUrl(filePath);
            if (OpenView(fileUrl, owner, onFailed))
                onShown?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool OpenLocalViewWebGL(string filePath, JsonBasedObject owner, Action<ViewOpenError> onFailed)
        {
            Debug.Log($"[RenderViewsManager] Loading HTML content from cache: {filePath}");

            if (!TryLoadLocalViewText(filePath, out string htmlContent, out var location))
            {
                Debug.LogError($"[RenderViewsManager] Failed to load HTML content: {filePath}");
                onFailed?.Invoke(ViewOpenError.LoadFailed);
                return false;
            }

            Debug.Log($"[RenderViewsManager] Loaded HTML content: {htmlContent.Length} bytes");

            // Load manifest.json if it exists
            string manifestPath = ReplaceViewFileName(location.Path, "manifest.json");
            IntPtr manifestPtr = LibraryMethods.General.balancyLoadFileContent(
                manifestPath, location.Storage == LocalViewStorage.Resources ? 1 : 0);
            string manifestContent = Marshal.PtrToStringAnsi(manifestPtr);

            // Open WebView with HTML content
            return OpenHtmlView(htmlContent, manifestContent, owner, onFailed);
        }

        private static bool OpenHtmlView(string htmlContent, string manifestContent, JsonBasedObject owner, Action<ViewOpenError> onFailed)
        {
            if (_webView.IsWebViewOpen())
            {
                Debug.LogError("View is already opened");
                onFailed?.Invoke(ViewOpenError.AlreadyOpened);
                return false;
            }

            m_LastOpenedOwnerPtr = owner?.GetRawPointer() ?? IntPtr.Zero;
            string ownerJson = owner?.ToJsonString(DEFAULT_OWNER_DEPTH, false) ?? "";

            // Parse manifest
            string manifestJson = "{}";
            if (!string.IsNullOrEmpty(manifestContent))
            {
                manifestJson = manifestContent;
            }

            // Calculate additional info
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string additionalInfo = $"{{\"launchTime\":{time}}}";

            if (owner is IOwnerWithTimer ownerWithTimer)
            {
                int secondsLeft = ownerWithTimer.GetSecondsLeftBeforeDeactivation();
                if (secondsLeft > 0)
                    additionalInfo = $"{{\"launchTime\":{time},\"secondsLeft\":{secondsLeft}}}";
            }

            Debug.Log($"[RenderViewsManager] Opening HTML view with content length: {htmlContent.Length}");

            // Use existing OpenWebView infrastructure
            bool success = _webView.OpenWebViewHtml(htmlContent, ownerJson, additionalInfo, manifestJson);

            if (!success)
            {
                Debug.LogError("[RenderViewsManager] Failed to open HTML view");
                onFailed?.Invoke(ViewOpenError.LoadFailed);
            }

            return success;
        }
#endif

        /// <summary>Set presentation delay and fade duration in seconds. Both default to zero.
        /// Call after SDK initialization; settings also apply to a prepared hidden WebView.</summary>
        public static void SetViewDelays(float showDelay, float transparencyAnimationDuration)
        {
            if (_webView)
            {
                _webView.SetShowDelay(showDelay);
                _webView.SetAnimationDuration(transparencyAnimationDuration);
            }
        }

        public static void SetEmergencyExitEnabled(bool enabled)
        {
            if (_webView)
            {
                _webView.SetEmergencyExitEnabled(enabled);
            }
        }
        
        public static bool OpenView(string url, JsonBasedObject owner = null, Action<ViewOpenError> onFailed = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("URL is null or empty");
                onFailed?.Invoke(ViewOpenError.ViewNotFound);
                return false;
            }

            if (_webView == null) { onFailed?.Invoke(ViewOpenError.LoadFailed); return false; }
            if (!_scriptsLoaded) RefreshScripts();

            if (_webView.IsWebViewOpen())
            {
                Debug.LogError("View is already opened");
                onFailed?.Invoke(ViewOpenError.AlreadyOpened);
                return false;
            }

            if (_webView.IsPersistentModeEnabled())
            {
                if (!_webView.CanShowPersistentView()) { onFailed?.Invoke(ViewOpenError.AlreadyOpened); return false; }
                _webView.CloseWebView();
            }

            var urlToLoad = url;// + "?timestamp=" + Guid.NewGuid().ToString();

            m_LastOpenedOwnerPtr = owner?.GetRawPointer() ?? IntPtr.Zero;
            // Guard against a null owner (e.g. opening a standalone view): a null ownerJson gets
            // marshalled to the native OpenWebView as a null char* and crashes (SIGSEGV). The
            // persistent branch already does this; classic mode was missing it.
            string ownerJson = owner?.ToJsonString(DEFAULT_OWNER_DEPTH, false) ?? "";

            string additionalInfo = BuildAdditionalInfo(owner);
            
            bool success = false;
            if (UseEmbeddedWebView)
            {
#if UNITY_EDITOR_OSX
                success = BalancyWebViewEmbedded.Instance.InitializeEmbeddedWebView(urlToLoad, ownerJson, additionalInfo);
#elif UNITY_EDITOR
                CreateErrorMessage();
#endif
            }
            else
            {
                // Use game view size for popup mode to match embedded mode behavior
                // Debug.LogWarning("[RenderViewsManager] OpenView " + urlToLoad + " in popup mode.");
                // Debug.LogWarning("[RenderViewsManager] ownerJson " + ownerJson);
                success = _webView.OpenWebView(urlToLoad, ownerJson, additionalInfo);
                // Debug.LogWarning("[RenderViewsManager] success " + success);
            }
            
#if UNITY_EDITOR
#if UNITY_EDITOR_OSX
            BalancyEditorViewHint.ShowUIMessage("The view was opened as a popup window. On a mobile device it will be opened as an embedded web view.", "OK", CloseView);
#else
            BalancyEditorViewHint.ShowUIMessage("The view isn't supported on this OS. On a mobile device it will be opened as an embedded web view.", "OK", CloseView);
#endif
#endif
            
            if (success)
                Debug.Log("Opening View: " + urlToLoad);
            else
            {
                Debug.Log("Failed to open View");
                onFailed?.Invoke(ViewOpenError.LoadFailed);
            }

            return success;
        }

#if UNITY_EDITOR
        private static void CreateErrorMessage()
        {
            var path = "UI/NoViewMessage.prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Packages/co.balancy.unity/" + path);

            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Balancy/" + path);

            if (prefab != null)
                GameObject.Instantiate(prefab);
            else
                Debug.LogError("Failed to load View prefab!");
        }
#endif

        private static void OnMessageReceived(string msg)
        {
            if (_onMessageReceived != null)
            {
                bool proceed = _onMessageReceived(msg);
                if (!proceed)
                {
                    Debug.Log("Message handling was cancelled by external handler: " + msg);
                    // Send response back so the WebView bridge doesn't hang waiting
                    _webView.SendMessageToWebView(RequestError(msg, "Message rejected by application"));
                    return;
                }
            }

            RunRequestInTheCorePlugin(msg);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.WebviewRequestCallback))]
        private static void OnMessageResponseReceived(string response)
        {
            try
            {
                _webView?.SendMessageToWebView(response);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
        
        enum RequestAction {
            None = 0,
            GetProfile = 1,
            SetProfile = 2,
            GetLocalization = 10,
            GetImageUrl = 11,
            GetInfo = 12,
            CanBuyGroupOffer = 13,
            
            WatchRewardedAd = 40,

            // Server actions — forwarded by the core to the authoritative server (room).
            CallServerAction = 50,

            BuyOffer = 101,
            BuyGroupOffer = 102,
            BuyShopSlot = 103,
            BattlePassClaim = 104,

            CloseWindow = 200,
            BalancyIsReady = 201,
            SetEmergencyExitEnabled = 203,

            AuthWithNameAndPassword = 403,
            AuthWithEmailAndPassword = 404,

            // Provider auth actions (409-410 forwarded here for native OAuth, 411-412 handled in C++ core)
            AuthWithProvider = 409,
            LinkWithProvider = 410,
            UnlinkProvider = 411,
            ContinueAsGuest = 412,

            CustomMessage = 1000,
        }
        
        enum InfoType {
            None = 0,
            OfferPrice = 1,
            OfferGroupPrice = 2,
            CustomPrice = 9,
            Custom = 10
        }
        
        const string DEFAULT_ANSWER = "{\"status\":\"ok\"}";
        const string FAILED_ANSWER = "{\"status\":\"failed\"}";
        
        [System.Serializable]
        class CommandBuyOffer
        {
            public string instanceId;
        }
        
        [System.Serializable]
        class CommandBuyOfferGroup : CommandBuyOffer
        {
            public int index;
        }
        
        [System.Serializable]
        class CommandBuyShopSlot
        {
            public string slotId;
        }
        
        [System.Serializable]
        class CommandGetInfo
        {
            public int type;
            public string instanceId;
            public int index;
            public string productId;
            public string cistom;
        }

        [System.Serializable]
        class CommandSetEmergencyExit
        {
            public bool enabled;
        }

        [System.Serializable]
        class CommandAuthName
        {
            public string name;
            public string password;
        }

        [System.Serializable]
        class CommandAuthEmail
        {
            public string email;
            public string password;
        }

        [System.Serializable]
        class CommandProvider
        {
            public string provider;
            public bool forceLink;
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.DataRequestedCallback))]
        private static void DataRequested(string sender, int command, string paramsJson, int requestId)
        {
            try
            {
                switch ((RequestAction)command)
                {
                    case RequestAction.BalancyIsReady:
                    {
                        LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                        return;
                    }
                    case RequestAction.BuyOffer:
                    {
                        CommandBuyOffer commandInfo = JsonUtility.FromJson<CommandBuyOffer>(paramsJson);
                        if (commandInfo == null || string.IsNullOrEmpty(commandInfo.instanceId))
                        {
                            Debug.LogError("Invalid command parameters for IBuyOffer");
                            break;
                        }

                        var offerInfo = Profiles.System?.SmartInfo.FindOfferInfo(commandInfo.instanceId);
                        if (offerInfo != null)
                        {
                            Balancy.API.InitPurchaseOffer(offerInfo, (success, error) =>
                            {
                                if (success)
                                {
                                    Debug.Log("Offer purchased successfully: " + commandInfo.instanceId);
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                                }
                                else
                                {
                                    Debug.LogError("Failed to purchase offer: " + commandInfo.instanceId +
                                                   ", Error: " + error);
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                                }
                            });
                        }
                        else
                        {
                            Debug.LogError("OfferInfo not found for instanceId: " + commandInfo.instanceId);
                            break;
                        }

                        return;
                    }

                    case RequestAction.BuyGroupOffer:
                    {
                        CommandBuyOfferGroup commandInfo = JsonUtility.FromJson<CommandBuyOfferGroup>(paramsJson);
                        if (commandInfo == null || string.IsNullOrEmpty(commandInfo.instanceId))
                        {
                            Debug.LogError("Invalid command parameters for IBuyGroupOffer");
                            break;
                        }

                        var offerInfo = Profiles.System?.SmartInfo.FindOfferGroupInfo(commandInfo.instanceId);
                        if (offerInfo?.GameOfferGroup?.StoreItems == null ||
                            offerInfo.GameOfferGroup.StoreItems.Length <= commandInfo.index)
                        {
                            Debug.LogError("Store item index is invalid or not set for group offer: " +
                                           commandInfo.instanceId);
                            break;
                        }

                        var storeItem = offerInfo?.GameOfferGroup?.StoreItems[commandInfo.index];

                        if (storeItem == null || !offerInfo.CanPurchase(storeItem))
                        {
                            Debug.LogError("StoreItem is not available for purchase: " + commandInfo.instanceId);
                            break;
                        }

                        Balancy.API.InitPurchaseOffer(offerInfo, storeItem, (success, error) =>
                        {
                            if (success)
                            {
                                Debug.Log("Group offer purchased successfully: " + commandInfo.instanceId);
                                LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                            }
                            else
                            {
                                Debug.LogError("Failed to purchase group offer: " + commandInfo.instanceId +
                                               ", Error: " + error);
                                LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                            }
                        });
                        return;
                    }

                    case RequestAction.BuyShopSlot:
                    {
                        CommandBuyShopSlot commandInfo = JsonUtility.FromJson<CommandBuyShopSlot>(paramsJson);
                        if (commandInfo == null || string.IsNullOrEmpty(commandInfo.slotId))
                        {
                            Debug.LogError("Invalid command parameters for IBuyShopSlot");
                            break;
                        }

                        var shopSlot = Profiles.System?.ShopsInfo.FindShopSlot(commandInfo.slotId);
                        if (shopSlot != null)
                        {
                            Balancy.API.InitPurchaseShop(shopSlot, (success, error) =>
                            {
                                if (success)
                                {
                                    Debug.Log("Shop slot purchased successfully: " + commandInfo.slotId);
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                                }
                                else
                                {
                                    Debug.LogError("Failed to purchase shop slot: " + commandInfo.slotId +
                                                   ", Error: " + error);
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                                }
                            });
                        }
                        else
                        {
                            Debug.LogError("ShopSlot not found for instanceId: " + commandInfo.slotId);
                            break;
                        }

                        return;
                    }

                    case RequestAction.GetInfo:
                    {
                        CommandGetInfo commandInfo = JsonUtility.FromJson<CommandGetInfo>(paramsJson);
                        if (commandInfo == null || commandInfo.type == 0)
                        {
                            Debug.LogError("Invalid command parameters for GetInfo");
                            break;
                        }

                        switch ((InfoType)commandInfo.type)
                        {
                            case InfoType.OfferGroupPrice:
                            {
                                var offerInfo =
                                    Profiles.System?.SmartInfo.FindOfferGroupInfo(commandInfo.instanceId);
                                if (offerInfo?.GameOfferGroup?.StoreItems == null ||
                                    offerInfo.GameOfferGroup.StoreItems.Length <= commandInfo.index)
                                {
                                    Debug.LogError(
                                        "Store item index is invalid or not set for group offer: " +
                                        commandInfo.instanceId);
                                    break;
                                }

                                var storeItem = offerInfo?.GameOfferGroup?.StoreItems[commandInfo.index];
                                Balancy.Actions.Purchasing.GetHardPurchaseInfoCallback()(
                                    storeItem?.Price?.Product?.ProductId, (info) =>
                                    {
                                        LibraryMethods.General.balancyDataRequestedResponse(requestId,
                                            JsonUtility.ToJson(info));
                                    });
                                return;
                            }
                            case InfoType.CustomPrice:
                            {
                                Balancy.Actions.Purchasing.GetHardPurchaseInfoCallback()(commandInfo.productId,
                                    (info) =>
                                    {
                                        LibraryMethods.General.balancyDataRequestedResponse(requestId,
                                            JsonUtility.ToJson(info));
                                    });
                                return;
                            }
                        }

                        LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                        return;
                    }
                    case RequestAction.WatchRewardedAd:
                    {
                        Balancy.Actions.Ads.GetAdWatchCallback()?.Invoke((success) =>
                        {
                            LibraryMethods.General.balancyDataRequestedResponse(requestId,
                                "{\"success\":" + (success ? 1 : 0) + "}");
                        });

                        return;
                    }

                    case RequestAction.SetEmergencyExitEnabled:
                    {
                        CommandSetEmergencyExit emergencyCmd = JsonUtility.FromJson<CommandSetEmergencyExit>(paramsJson);
                        if (emergencyCmd != null)
                        {
                            _webView.SetEmergencyExitEnabled(emergencyCmd.enabled);
                        }
                        LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                        return;
                    }
                    case RequestAction.AuthWithNameAndPassword:
                    {
                        CommandAuthName authNameCmd = JsonUtility.FromJson<CommandAuthName>(paramsJson);
                        if (authNameCmd == null || string.IsNullOrEmpty(authNameCmd.name))
                        {
                            LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                            return;
                        }

                        API.Auth.WithNameAndPassword(authNameCmd.name, authNameCmd.password, data =>
                        {
                            if (data.Success)
                            {
                                API.Auth.GetInfo(info =>
                                {
                                    var json = $"{{\"success\":true,\"userId\":\"{EscapeJson(info.UserId ?? data.UserId)}\",\"networks\":{(info.Success ? info.NetworksJson : "[]")}}}";
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, json);
                                });
                            }
                            else
                            {
                                LibraryMethods.General.balancyDataRequestedResponse(requestId,
                                    $"{{\"success\":false,\"errorMessage\":\"{EscapeJson(data.ErrorMessage ?? "")}\"}}");
                            }
                        });
                        return;
                    }

                    case RequestAction.AuthWithEmailAndPassword:
                    {
                        CommandAuthEmail authEmailCmd = JsonUtility.FromJson<CommandAuthEmail>(paramsJson);
                        if (authEmailCmd == null || string.IsNullOrEmpty(authEmailCmd.email))
                        {
                            LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                            return;
                        }

                        API.Auth.WithEmailAndPassword(authEmailCmd.email, authEmailCmd.password, data =>
                        {
                            if (data.Success)
                            {
                                API.Auth.GetInfo(info =>
                                {
                                    var json = $"{{\"success\":true,\"userId\":\"{EscapeJson(info.UserId ?? data.UserId)}\",\"networks\":{(info.Success ? info.NetworksJson : "[]")}}}";
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, json);
                                });
                            }
                            else
                            {
                                LibraryMethods.General.balancyDataRequestedResponse(requestId,
                                    $"{{\"success\":false,\"errorMessage\":\"{EscapeJson(data.ErrorMessage ?? "")}\"}}");
                            }
                        });
                        return;
                    }

                    case RequestAction.AuthWithProvider:
                    {
                        CommandProvider providerCmd = JsonUtility.FromJson<CommandProvider>(paramsJson);
                        if (providerCmd == null || string.IsNullOrEmpty(providerCmd.provider))
                        {
                            LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                            return;
                        }

                        // Provider auth requires native OAuth — the game must obtain userId+token
                        // from the platform SDK (Sign in with Apple, Google Sign-In, Facebook Login),
                        // then call API.Auth.WithApple/Google/Facebook with those credentials.
                        // This case dispatches based on provider name.
                        void OnAuthSuccess(Balancy.Core.Responses.AuthResponseData data)
                        {
                            if (data.Success)
                            {
                                API.Auth.GetInfo(info =>
                                {
                                    var json = $"{{\"success\":true,\"userId\":\"{EscapeJson(info.UserId ?? data.UserId)}\",\"networks\":{(info.Success ? info.NetworksJson : "[]")}}}";
                                    LibraryMethods.General.balancyDataRequestedResponse(requestId, json);
                                });
                            }
                            else
                            {
                                LibraryMethods.General.balancyDataRequestedResponse(requestId,
                                    $"{{\"success\":false,\"errorMessage\":\"{EscapeJson(data.ErrorMessage ?? "")}\"}}");
                            }
                        }

                        // Note: In a real integration, the game developer would intercept this
                        // via a custom DataRequested callback or override, launch the native
                        // OAuth flow, obtain userId+token, and call the appropriate API method.
                        // For now, return an error since we don't have the native token.
                        LibraryMethods.General.balancyDataRequestedResponse(requestId,
                            $"{{\"success\":false,\"errorMessage\":\"Provider auth for '{EscapeJson(providerCmd.provider)}' requires native OAuth — implement a custom handler\"}}");
                        return;
                    }

                    case RequestAction.LinkWithProvider:
                    {
                        CommandProvider providerCmd = JsonUtility.FromJson<CommandProvider>(paramsJson);
                        if (providerCmd == null || string.IsNullOrEmpty(providerCmd.provider))
                        {
                            LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                            return;
                        }

                        // Same as AuthWithProvider — native OAuth token needed.
                        LibraryMethods.General.balancyDataRequestedResponse(requestId,
                            $"{{\"success\":false,\"errorMessage\":\"Provider link for '{EscapeJson(providerCmd.provider)}' requires native OAuth — implement a custom handler\"}}");
                        return;
                    }

                    case RequestAction.CloseWindow:
                    {
                        CloseView();
                        LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RenderViewsManager] Error in DataRequested (command={command}): {e.Message}\n{e.StackTrace}");
                LibraryMethods.General.balancyDataRequestedResponse(requestId, FAILED_ANSWER);
                return;
            }

            LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
        }

        public static void CloseView()
        {
            Debug.Log($"[RenderViewsManager] CloseView requested. Persistent={_webView != null && _webView.IsPersistentModeEnabled()} Embedded={UseEmbeddedWebView}");
            if (_webView != null && _webView.IsPersistentModeEnabled())
            {
                _webView.CloseView();
            }
            else if (UseEmbeddedWebView)
            {
#if UNITY_EDITOR
                BalancyWebViewEmbedded.Instance.CloseEmbeddedWebView();
#endif
            }
            else
                _webView.CloseWebView();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static void RunRequestInTheCorePlugin(string requestData)
        {
            try
            {
                var requests = ParseBridgeRequests(requestData);
                string viewId = requests.Length > 0 ? requests[0].viewId : null;
                IntPtr owner = m_LastOpenedOwnerPtr;
                if (!string.IsNullOrEmpty(viewId) && !ViewOwners.TryGetValue(viewId, out owner))
                {
                    CoreResponseCallback(RequestError(requestData, "View is no longer active")); return;
                }
                foreach (var item in requests)
                    if (item.viewId != viewId) { CoreResponseCallback(RequestError(requestData, "Mixed view contexts")); return; }
                // Null owner is valid for explicit-context APIs, localization and resources.
                LibraryMethods.General.balancyWebViewRequest(owner, requestData, CoreResponseCallback);
            }
            catch (Exception error) { CoreResponseCallback(RequestError(requestData, error.Message)); }
        }
    }
}
