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
#if UNITY_EDITOR
                success = BalancyWebViewEmbedded.Instance.InitializeEmbeddedWebView(urlToLoad, ownerJson);
#endif
            }
            else
            {
                // Use game view size for popup mode to match embedded mode behavior
                success = _webView.OpenWebView(urlToLoad, ownerJson, Screen.width / 2, Screen.height / 2);
            }
            
            if (success)
                Debug.Log("Opening View: " + urlToLoad);
            else
                Debug.Log("Failed to open View");
        }

        private static string OnMessageReceived(string msg)
        {
            Debug.Log("Incomming = " + msg);
            var handled = TryToHandleMessage(msg);
            if (handled)
                return string.Empty;
            
            var output = RunRequestInTheCorePlugin(msg);
            Debug.Log("output = " + output);
            return output;
        }

        private static bool TryToHandleMessage(string msg)
        {
            void BadCommand()
            {
                Debug.LogError("Balancy View Bad Command: " + msg);
            }
            
            if (msg.StartsWith("//:"))
            {
                var prms = msg.Split(":");
                if (prms.Length >= 2)
                {
                    var command = prms[1];
                    switch (command)
                    {
                        case "balancy_close_view":
                            if (UseEmbeddedWebView)
                            {
#if UNITY_EDITOR
                                BalancyWebViewEmbedded.Instance.CloseEmbeddedWebView();
#endif
                            }
                            else
                                _webView.CloseWebView();
                            break;
                        case "balancy_buy_offer":
                        {
                            if (prms.Length >= 3)
                            {
                                var instanceId = prms[2];
                                var offerInfo = Profiles.System?.SmartInfo.FindOfferInfo(instanceId);
                                if (offerInfo != null)
                                {
                                    Balancy.API.InitPurchaseOffer(offerInfo, (success, error) =>
                                    {
                                        if (success)
                                            Debug.Log("Offer purchased successfully: " + instanceId);
                                        else
                                            Debug.LogError("Failed to purchase offer: " + instanceId + ", Error: " + error);
                                    });
                                }
                                else
                                    Debug.LogError("OfferInfo not found for instanceId: " + instanceId);
                            } else
                                BadCommand();
                            break;
                        }
                        case "balancy_buy_group_offer":
                        {
                            if (prms.Length >= 4)
                            {
                                var groupId = prms[2];
                                var storeItemIndexStr = prms[3];
                                if (!int.TryParse(storeItemIndexStr, out int storeItemIndex))
                                {
                                    Debug.LogError("Invalid store item index: " + storeItemIndexStr);
                                    return false;
                                }
                                var offerInfo = Profiles.System?.SmartInfo.FindOfferGroupInfo(groupId);
                                if (offerInfo?.GameOfferGroup?.StoreItems == null || offerInfo.GameOfferGroup.StoreItems.Length <= storeItemIndex)
                                {
                                    Debug.LogError("Store item index is invalid or not set for group offer: " + groupId);
                                    return false;
                                }
                                else
                                {
                                    var storeItem = offerInfo?.GameOfferGroup?.StoreItems[storeItemIndex];
                                    Balancy.API.InitPurchaseOffer(offerInfo, storeItem, (success, error) =>
                                    {
                                        if (success)
                                            Debug.Log("Group offer purchased successfully: " + groupId);
                                        else
                                            Debug.LogError("Failed to purchase group offer: " + groupId +
                                                           ", Error: " + error);
                                    });
                                }
                            } else
                                BadCommand();
                            break;
                        }
                        case "balancy_buy_shop_slot":
                        {
                            if (prms.Length >= 2)
                            {
                                var slotId = prms[2];
                                var shopSlot = Profiles.System?.ShopsInfo.FindShopSlot(slotId);
                                if (shopSlot != null)
                                {
                                    Balancy.API.InitPurchaseShop(shopSlot, (success, error) =>
                                    {
                                        if (success)
                                            Debug.Log("Shop slot purchased successfully: " + slotId);
                                        else
                                            Debug.LogError("Failed to purchase shop slot: " + slotId + ", Error: " + error);
                                    });
                                }
                                else
                                    Debug.LogError("ShopSlot not found for instanceId: " + slotId);
                            } else
                                BadCommand();
                            break;
                        }
                    }
                }
                else
                    BadCommand();

                return true;
            }

            return false;
        }

        private static string RunRequestInTheCorePlugin(string requestData)
        {
            return JsonBasedObject.GetStringFromIntPtr(LibraryMethods.General.balancyWebViewRequest(requestData));
        }
    }
}
