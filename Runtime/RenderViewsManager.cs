using Balancy.Models;
using Balancy.WebView;
using UnityEngine;

namespace Balancy
{
    public class RenderViewsManager
    {
#if UNITY_EDITOR
        // private const bool UseEmbeddedWebView = true;
        private const bool UseEmbeddedWebView = false;
#else
        private const bool UseEmbeddedWebView = false;
#endif
        
        private static BalancyWebView _webView;
        
        internal static void Init()
        {
            LibraryMethods.General.balancySetDataRequestedCallback(DataRequested);

            BalancyWebView.Instance.OnMessage = OnMessageReceived;
            _webView = BalancyWebView.Instance;
            _webView.OnLoadCompleted += HandleLoadCompleted;
            _webView.OnClosed += HandleWebViewClosed;
            
            _webView.SetTransparentBackground(true);
            _webView.SetFullScreen(true);
            //_webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
            //_webView.SetDebugLogging(true);
        }

        private static void HandleWebViewClosed()
        {
            
        }

        private static void HandleLoadCompleted(bool obj)
        {
            
        }

        public static void OpenLocalView(string filePath, JsonBasedObject owner = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("File path is null or empty");
                return;
            }
            string fileUrl = "file://" + filePath;
            OpenView(fileUrl, owner);
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

            string ownerJson = owner?.ToJsonString(false);
            bool success = false;
            if (UseEmbeddedWebView)
            {
#if UNITY_EDITOR_OSX
                success = BalancyWebViewEmbedded.Instance.InitializeEmbeddedWebView(urlToLoad, ownerJson);
#elif UNITY_EDITOR
                CreateErrorMessage();
#endif
            }
            else
            {
                // Use game view size for popup mode to match embedded mode behavior
                success = _webView.OpenWebView(urlToLoad, ownerJson);
            }
            
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

        private static string OnMessageReceived(string msg)
        {
            Debug.Log("Incomming = " + msg);
            
            //hardcode. rewrite the way how native plugins send me the message
            if (msg == "//:balancy_close_view")
            {
                msg= "{\"action\":200, \"params\":{}}";
            }
            
            var output = RunRequestInTheCorePlugin(msg);
            Debug.Log("output = " + output);
            return output;
        }
        
        enum RequestAction {
            None = 0,
            GetProfile = 1,
            SetProfile = 2,
            GetLocalization = 10,
            GetImageUrl = 11,

            IBuyOffer = 101,
            IBuyGroupOffer = 102,
            IBuyShopSlot = 103,
            
            GetInfo = 110,

            ICloseWindow = 200,

            ICustomMessage = 1000,
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
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.InvokeInMainThreadCallback))]
        private static string DataRequested(string sender, int command, string paramsJson)
        {
            switch ((RequestAction)command)
            {
                case RequestAction.IBuyOffer:
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
                    return DEFAULT_ANSWER;
                }

                case RequestAction.IBuyGroupOffer:
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
                    
                    return DEFAULT_ANSWER;
                }

                case RequestAction.IBuyShopSlot:
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
                    
                    return DEFAULT_ANSWER;
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
                            var info = Balancy.Actions.Purchasing.GetHardPurchaseInfoCallback()(storeItem?.Price?.Product?.ProductId);
                            return JsonUtility.ToJson(info);
                        }
                        case InfoType.CustomPrice:
                        {
                            var info = Balancy.Actions.Purchasing.GetHardPurchaseInfoCallback()(commandInfo.productId);
                            return JsonUtility.ToJson(info);
                        }
                    }
                    
                    return DEFAULT_ANSWER;
                }

                case RequestAction.ICloseWindow:
                {
                    if (UseEmbeddedWebView)
                    {
#if UNITY_EDITOR
                        BalancyWebViewEmbedded.Instance.CloseEmbeddedWebView();
#endif
                    }
                    else
                        _webView.CloseWebView();

                    return DEFAULT_ANSWER;
                }
            }

            return null;
        }

        private static string RunRequestInTheCorePlugin(string requestData)
        {
            return JsonBasedObject.GetStringFromIntPtr(LibraryMethods.General.balancyWebViewRequest(requestData));
        }
    }
}
