using System;
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

            _webView.SetTransparentBackground(true);
            _webView.SetFullScreen(true);
            //_webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
            //_webView.SetDebugLogging(true);

            SetViewDelays(0.2f, 0.3f);
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

            if (_webView != null)
            {
                _webView.OnMessage -= OnMessageReceived;
                _webView.OnLoadCompleted -= HandleLoadCompleted;
                _webView.OnClosed -= HandleWebViewClosed;
            }

            _onMessageReceived = null;
            m_LastOpenedOwnerPtr = IntPtr.Zero;
            _webView = null;
        }

        /// <summary>
        /// Compile all view scripts from the native layer and pass them to BalancyWebView for injection.
        /// Called automatically during Init() and can be called again if scripts need refreshing.
        /// </summary>
        public static void RefreshScripts()
        {
            try
            {
                IntPtr ptr = LibraryMethods.General.balancyDataObjectCompileAllScripts();
                string scriptsCode = Marshal.PtrToStringAnsi(ptr) ?? "";
                Debug.Log($"[RenderViewsManager] Scripts compiled: {scriptsCode.Length} characters");
                _webView.SetScriptsCode(scriptsCode);
            }
            catch (Exception e)
            {
                // Keep the previously-compiled bundle on failure. Wiping it (SetScriptsCode(""))
                // would open the next view with zero components (blank) — a stale-but-complete
                // bundle is strictly better than an empty one, and this now runs before every open.
                Debug.LogError($"[RenderViewsManager] Failed to compile scripts, keeping previous bundle: {e.Message}");
            }
        }

        private static IntPtr m_LastOpenedOwnerPtr = IntPtr.Zero;

        private static void PrepareCallbacks()
        {
            Balancy.Callbacks.OnOfferDeactivated -= HandleOfferDeactivated;
            Balancy.Callbacks.OnOfferDeactivated += HandleOfferDeactivated;
            
            Balancy.Callbacks.OnOfferGroupDeactivated -= HandleOfferGroupDeactivated;
            Balancy.Callbacks.OnOfferGroupDeactivated += HandleOfferGroupDeactivated;
            
            Balancy.Callbacks.OnEventDeactivated -= HandleEventDeactivated;
            Balancy.Callbacks.OnEventDeactivated += HandleEventDeactivated;
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
            if (m_LastOpenedOwnerPtr == IntPtr.Zero)
                return;

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
            if (_webView.IsWebViewOpen())
                _webView.SendMessageToWebView(message);
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

            return additionalInfo;
        }

        private static string NormalizeLocalPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;

            return filePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? filePath.Substring(7)
                : filePath;
        }

        public static void PrepareWebView(Action onReady = null)
        {
            Debug.Log("[RenderViewsManager] PrepareWebView requested");
            _webView?.PrepareWebView(onReady);
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
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("File path is null or empty");
                onFailed?.Invoke(ViewOpenError.ViewNotFound);
                return;
            }

            // Recompile scripts right before opening — the CENTRAL seam every view open funnels
            // through (UnnyObject.OpenView and direct callers alike). Whatever preload put the
            // view's script files on disk has finished by now, so this guarantees the injected
            // bundle is complete. Without it, opening against a stale bundle that predates a
            // late-arriving script throws "Can't find variable: <Class>" (black screen).
            RefreshScripts();

            Debug.Log($"[RenderViewsManager] OpenLocalView requested. Persistent={UsePersistentWebViewForLocalViews()} Path={filePath}");

            if (UsePersistentWebViewForLocalViews())
            {
                string normalizedPath = NormalizeLocalPath(filePath);
                if (!File.Exists(normalizedPath))
                {
                    Debug.LogError($"[RenderViewsManager] Persistent WebView requires a readable local HTML file: {normalizedPath}");
                    onFailed?.Invoke(ViewOpenError.FileNotFound);
                    return;
                }

                if (_webView.IsWebViewOpen())
                {
                    Debug.LogError("View is already opened");
                    onFailed?.Invoke(ViewOpenError.AlreadyOpened);
                    return;
                }

                m_LastOpenedOwnerPtr = owner?.GetRawPointer() ?? IntPtr.Zero;
                string ownerJson = owner?.ToJsonString(DEFAULT_OWNER_DEPTH, false) ?? "";
                string additionalInfo = BuildAdditionalInfo(owner);
                try
                {
                    string htmlContent = File.ReadAllText(normalizedPath);
                    Debug.Log($"[RenderViewsManager] Persistent OpenLocalView loaded HTML. Length={htmlContent.Length} Owner={(owner == null ? "null" : owner.GetType().Name)}");
                    _webView.ShowView(htmlContent, ownerJson, additionalInfo, onShown);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RenderViewsManager] Failed to read local view HTML for persistent WebView: {e.Message}");
                    onFailed?.Invoke(ViewOpenError.LoadFailed);
                }
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Load HTML content from cache instead of using file:// URLs
            if (OpenLocalViewWebGL(filePath, owner, onFailed))
                onShown?.Invoke();
#else
            string fileUrl = "file://" + filePath;
            if (OpenView(fileUrl, owner, onFailed))
                onShown?.Invoke();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool OpenLocalViewWebGL(string filePath, JsonBasedObject owner, Action<ViewOpenError> onFailed)
        {
            Debug.Log($"[RenderViewsManager] Loading HTML content from cache: {filePath}");

            // Extract relative path (remove /idbfs/.../guid_ prefix if present)
            string relativePath = filePath;
            if (relativePath.StartsWith("/idbfs/"))
            {
                // Pattern: /idbfs/<hash>/<guid>_Cache/Files/...
                int cacheIndex = relativePath.IndexOf("Cache/");
                if (cacheIndex > 0)
                {
                    relativePath = relativePath.Substring(cacheIndex);
                }
            }

            Debug.Log($"[RenderViewsManager] Relative path: {relativePath}");

            // Load HTML content from C++ cache
            IntPtr contentPtr = LibraryMethods.General.balancyLoadFileFromCache(relativePath);
            string htmlContent = Marshal.PtrToStringAnsi(contentPtr);

            if (string.IsNullOrEmpty(htmlContent))
            {
                Debug.LogError($"[RenderViewsManager] Failed to load HTML content from cache: {relativePath}");
                onFailed?.Invoke(ViewOpenError.LoadFailed);
                return false;
            }

            Debug.Log($"[RenderViewsManager] Loaded HTML content: {htmlContent.Length} bytes");

            // Load manifest.json if it exists
            string manifestPath = relativePath.Replace("index.html", "manifest.json");
            IntPtr manifestPtr = LibraryMethods.General.balancyLoadFileFromCache(manifestPath);
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

            if (_webView.IsWebViewOpen())
            {
                Debug.LogError("View is already opened");
                onFailed?.Invoke(ViewOpenError.AlreadyOpened);
                return false;
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
                    _webView.SendMessageToWebView("{\"status\":\"ok\"}");
                    return;
                }
            }

            RunRequestInTheCorePlugin(msg, OnMessageResponseReceived);
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

        private static void RunRequestInTheCorePlugin(string requestData, LibraryMethods.General.WebviewRequestCallback callback)
        {
            if (m_LastOpenedOwnerPtr == IntPtr.Zero)
            {
                // Debug.LogWarning("[RenderViewsManager] Cannot process WebView request: owner pointer is null");
                callback("{\"type\":\"response\",\"error\":\"Owner pointer is null\"}");
                return;
            }

            LibraryMethods.General.balancyWebViewRequest(m_LastOpenedOwnerPtr, requestData, callback);
        }
    }
}
