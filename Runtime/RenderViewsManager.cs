using System;
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
            if (m_LastOpenedOwnerPtr == deactivatedOwner.GetRawPointer())
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
            _webView.SendMessageToWebView(notification);
        }
        
        internal static void SendMessageToView(string message)
        {
            if (_webView.IsWebViewOpen())
                _webView.SendMessageToWebView(message);
        }
        
        public static void OpenLocalView(string filePath, JsonBasedObject owner = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("File path is null or empty");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: Load HTML content from cache instead of using file:// URLs
            OpenLocalViewWebGL(filePath, owner);
#else
            string fileUrl = "file://" + filePath;
            OpenView(fileUrl, owner);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void OpenLocalViewWebGL(string filePath, JsonBasedObject owner)
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
                return;
            }

            Debug.Log($"[RenderViewsManager] Loaded HTML content: {htmlContent.Length} bytes");

            // Load manifest.json if it exists
            string manifestPath = relativePath.Replace("index.html", "manifest.json");
            IntPtr manifestPtr = LibraryMethods.General.balancyLoadFileFromCache(manifestPath);
            string manifestContent = Marshal.PtrToStringAnsi(manifestPtr);

            // Open WebView with HTML content
            OpenHtmlView(htmlContent, manifestContent, owner);
        }

        private static void OpenHtmlView(string htmlContent, string manifestContent, JsonBasedObject owner)
        {
            if (_webView.IsWebViewOpen())
            {
                Debug.LogError("View is already opened");
                return;
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
            }
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
        
        public static void OpenView(string url, JsonBasedObject owner = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("URL is null or empty");
                return;
            }
            
            if (_webView.IsWebViewOpen())
            {
                Debug.LogError("View is already opened");
                return;
            }

            var urlToLoad = url;// + "?timestamp=" + Guid.NewGuid().ToString();

            m_LastOpenedOwnerPtr = owner?.GetRawPointer() ?? IntPtr.Zero;
            string ownerJson = owner?.ToJsonString(DEFAULT_OWNER_DEPTH, false);

            long launchTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string additionalInfo = $"{{\"launchTime\":{launchTime}}}";

            if (owner is IOwnerWithTimer ownerWithTimer)
            {
                int secondsLeft = ownerWithTimer.GetSecondsLeftBeforeDeactivation();
                if (secondsLeft > 0)
                    additionalInfo = $"{{\"launchTime\":{launchTime},\"secondsLeft\":{secondsLeft}}}";
            }
            
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
                Debug.Log("Failed to open View");
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
                    return;
                }
            }
            // Debug.Log("Incomming = " + msg);
            
            //hardcode. rewrite the way how native plugins send me the message
            // if (msg == "//:balancy_close_view")
            // {
            //     msg= "{\"action\":200, \"params\":{}}";
            // }
            
            RunRequestInTheCorePlugin(msg, OnMessageResponseReceived);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.WebviewRequestCallback))]
        private static void OnMessageResponseReceived(string response)
        {
            _webView.SendMessageToWebView(response);
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

            BuyOffer = 101,
            BuyGroupOffer = 102,
            BuyShopSlot = 103,
            BattlePassClaim = 104,

            CloseWindow = 200,
            BalancyIsReady = 201,

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
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.DataRequestedCallback))]
        private static void DataRequested(string sender, int command, string paramsJson, int requestId)
        {
            switch ((RequestAction)command)
            {
                case RequestAction.BalancyIsReady:
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[RenderViewsManager] BalancyIsReady received - WebView will be shown by JavaScript");
                    // For WebGL, the show() is called from JavaScript side via the jslib
                    // We just acknowledge the message here
#endif
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
                                Debug.Log("Offer purchased successfully: " + commandInfo.instanceId);
                            else
                                Debug.LogError("Failed to purchase offer: " + commandInfo.instanceId + ", Error: " + error);
                        });
                    }
                    else
                        Debug.LogError("OfferInfo not found for instanceId: " + commandInfo.instanceId);
                    LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
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
                    if (offerInfo?.GameOfferGroup?.StoreItems == null || offerInfo.GameOfferGroup.StoreItems.Length <= commandInfo.index)
                    {
                        Debug.LogError("Store item index is invalid or not set for group offer: " + commandInfo.instanceId);
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
                            Debug.Log("Group offer purchased successfully: " + commandInfo.instanceId);
                        else
                            Debug.LogError("Failed to purchase group offer: " + commandInfo.instanceId +
                                           ", Error: " + error);
                    });
                    
                    LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
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
                                Debug.Log("Shop slot purchased successfully: " + commandInfo.slotId);
                            else
                                Debug.LogError("Failed to purchase shop slot: " + commandInfo.slotId + ", Error: " + error);
                        });
                    }
                    else
                        Debug.LogError("ShopSlot not found for instanceId: " + commandInfo.slotId);
                    
                    LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
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
                            var offerInfo = Profiles.System?.SmartInfo.FindOfferGroupInfo(commandInfo.instanceId);
                            if (offerInfo?.GameOfferGroup?.StoreItems == null || offerInfo.GameOfferGroup.StoreItems.Length <= commandInfo.index)
                            {
                                Debug.LogError("Store item index is invalid or not set for group offer: " + commandInfo.instanceId);
                                break;
                            }

                            var storeItem = offerInfo?.GameOfferGroup?.StoreItems[commandInfo.index];
                            Balancy.Actions.Purchasing.GetHardPurchaseInfoCallback()(storeItem?.Price?.Product?.ProductId, (info) =>
                            {
                                LibraryMethods.General.balancyDataRequestedResponse(requestId, JsonUtility.ToJson(info));
                            });
                            return;
                        }
                        case InfoType.CustomPrice:
                        {
                            Balancy.Actions.Purchasing.GetHardPurchaseInfoCallback()(commandInfo.productId, (info) =>
                            {
                                LibraryMethods.General.balancyDataRequestedResponse(requestId, JsonUtility.ToJson(info));
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
                        LibraryMethods.General.balancyDataRequestedResponse(requestId, "{\"success\":" + (success ? 1 : 0) + "}");
                    });
                    
                    return;
                }

                case RequestAction.CloseWindow:
                {
                    CloseView();
                    LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
                    return;
                }
            }

            LibraryMethods.General.balancyDataRequestedResponse(requestId, DEFAULT_ANSWER);
        }

        public static void CloseView()
        {
            if (UseEmbeddedWebView)
            {
#if UNITY_EDITOR
                BalancyWebViewEmbedded.Instance.CloseEmbeddedWebView();
#endif
            }
            else
                _webView.CloseWebView();
        }

        private static void RunRequestInTheCorePlugin(string requestData, LibraryMethods.General.WebviewRequestCallback callback)
        {
            LibraryMethods.General.balancyWebViewRequest(m_LastOpenedOwnerPtr, requestData, callback);
        }
    }
}
