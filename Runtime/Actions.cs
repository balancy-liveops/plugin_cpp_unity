using System;
using Balancy.Core;
using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects;

namespace Balancy
{
    public class Actions
    {
        // Result of the purchase process
        public enum PurchaseResult
        {
            Success,
            Failed,
            Cancelled,
            Pending // For purchases that might require additional verification
        }

        [Serializable]
        public class BalancyProductInfo
        {
            public enum PurchaseType
            {
                StoreItem,
                Offer,
                OfferGroup,
                ShopSlot
            }

            public string ProductId;
            public PurchaseType Type;
            public string StoreItemUnnyId;
            public string OfferInstanceId;
            public string OfferUnnyId;

            public BalancyProductInfo(StoreItem storeItem)
            {
                Type = PurchaseType.StoreItem;
                ProductId = storeItem?.Price?.Product?.ProductId;
                StoreItemUnnyId = storeItem?.UnnyId;
            }
            
            public BalancyProductInfo(ShopSlot shopSlot)
            {
                Type = PurchaseType.ShopSlot;
                ProductId = shopSlot?.Slot?.StoreItem?.Price?.Product?.ProductId;
                StoreItemUnnyId = shopSlot?.Slot?.StoreItem?.UnnyId;
                OfferUnnyId = shopSlot?.Slot?.UnnyId;
            }
            
            public BalancyProductInfo(OfferInfo offerInfo)
            {
                Type = PurchaseType.Offer;
                ProductId = offerInfo?.GameOffer?.StoreItem?.Price?.Product?.ProductId;
                StoreItemUnnyId = offerInfo?.GameOffer?.StoreItem?.UnnyId;
                OfferInstanceId = offerInfo?.InstanceId;
                OfferUnnyId = offerInfo?.GameOffer?.UnnyId;
            }
            
            public BalancyProductInfo(OfferGroupInfo offerInfo, StoreItem storeItem)
            {
                Type = PurchaseType.OfferGroup;
                ProductId = storeItem?.Price?.Product?.ProductId;
                StoreItemUnnyId = storeItem?.UnnyId;
                OfferInstanceId = offerInfo?.InstanceId;
                OfferUnnyId = offerInfo?.GameOfferGroup?.UnnyId;
            }
            
            public bool Equals(BalancyProductInfo other)
            {
                if (other == null) return false;
                return ProductId == other.ProductId && Type == other.Type && StoreItemUnnyId == other.StoreItemUnnyId && 
                       OfferInstanceId == other.OfferInstanceId;
            }

            public StoreItem GetStoreItem()
            {
                switch (Type)
                {
                    case PurchaseType.Offer:
                        return GetGameOffer()?.StoreItem;
                    case PurchaseType.ShopSlot:
                        return GetShopSlot()?.StoreItem;
                    default:
                        return CMS.GetModelByUnnyId<StoreItem>(StoreItemUnnyId);
                }
                   
            }
            
            public Balancy.Models.LiveOps.Store.Slot GetShopSlot() => CMS.GetModelByUnnyId<Balancy.Models.LiveOps.Store.Slot>(OfferUnnyId);
            public GameOffer GetGameOffer() => CMS.GetModelByUnnyId<GameOffer>(OfferUnnyId);
            public GameOfferGroup GetGameOfferGroup() => CMS.GetModelByUnnyId<GameOfferGroup>(OfferUnnyId);

            public void ReportThePurchase(PaymentInfo paymentInfo)
            {
                switch (Type)
                {
                    case PurchaseType.StoreItem:
                        Balancy.Callbacks.OnHardPurchasedStoreItem?.Invoke(paymentInfo, GetStoreItem());
                        break;
                    case PurchaseType.ShopSlot:
                        Balancy.Callbacks.OnHardPurchasedShopSlot?.Invoke(paymentInfo, GetShopSlot());
                        break;
                    case PurchaseType.Offer:
                        Balancy.Callbacks.OnHardPurchasedOffer?.Invoke(paymentInfo, GetGameOffer());
                        break;
                    case PurchaseType.OfferGroup:
                        Balancy.Callbacks.OnHardPurchasedOfferGroup?.Invoke(paymentInfo, GetGameOfferGroup(), GetStoreItem());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public static class Ads
        {
            public delegate void OnAdWatchedCallback(bool success);
            
            public delegate void OnWantToWatchAdCallback(OnAdWatchedCallback callback);
            
            private static readonly OnWantToWatchAdCallback DefaultOnAdWatchCallback = (callback) =>
            {
                UnityEngine.Debug.LogWarning(
                    "No Ad Watch implementation provided. Implement your logic using Balancy.Actions.Ads.SetAdWatchCallback");
            };

            private static OnWantToWatchAdCallback _adWatchCallback = DefaultOnAdWatchCallback;

            public static void SetAdWatchCallback(OnWantToWatchAdCallback callback)
            {
                _adWatchCallback = callback ?? DefaultOnAdWatchCallback;
            }

            public static void ResetAdWatchCallback()
            {
                _adWatchCallback = DefaultOnAdWatchCallback;
            }

            internal static OnWantToWatchAdCallback GetAdWatchCallback()
            {
                return _adWatchCallback;
            }
        }

        public static class View
        {
            public static void SetOnMessageReceivedCallback(Func<string, bool> callback)
            {
                RenderViewsManager._onMessageReceived = callback;
            }
            
            public static void SendResponseToView(string requestId, string message)
            {
                var response = "{\"type\":\"response\", \"id\":\"" + requestId + "\", \"result\":" + message + "}";
                RenderViewsManager.SendMessageToView(response);
            }
      
            public static void SendCustomMessageToView(string message)
            {
                var customMessage = "{\"type\":\"custom-message\", \"data\":" + message + "}";
                RenderViewsManager.SendMessageToView(customMessage);
            }
        }

        public static class Purchasing
        {
            public delegate void HardPurchaseCallback(BalancyProductInfo productInfo);
            
            private static readonly HardPurchaseCallback DefaultHardPurchaseCallback = (productInfo) =>
            {
                UnityEngine.Debug.LogWarning(
                    "No hard purchase implementation provided. Either implement your own using " +
                    "Balancy.Actions.Purchasing.SetHardPurchaseCallback or install the Balancy Purchasing package."); //TODO add here the id
            };

            private static HardPurchaseCallback _hardPurchaseCallback = DefaultHardPurchaseCallback;

            public static void SetHardPurchaseCallback(HardPurchaseCallback callback)
            {
                _hardPurchaseCallback = callback ?? DefaultHardPurchaseCallback;
            }

            public static void ResetHardPurchaseCallback()
            {
                _hardPurchaseCallback = DefaultHardPurchaseCallback;
            }

            internal static HardPurchaseCallback GetHardPurchaseCallback()
            {
                return _hardPurchaseCallback;
            }
            
            //Write similar methods to Restore Purchases
            public delegate void RestorePurchasesCallback();
            private static readonly RestorePurchasesCallback DefaultRestorePurchasesCallback = () =>
            {
                UnityEngine.Debug.LogWarning(
                    "No restore purchases implementation provided. Either implement your own using " +
                    "PurchaseCallbacks.SetRestorePurchasesCallback or install the Balancy Purchasing package."); //TODO add here the id
            };
            private static RestorePurchasesCallback _restorePurchasesCallback = DefaultRestorePurchasesCallback;
            
            public static void SetRestorePurchasesCallback(RestorePurchasesCallback callback)
            {
                _restorePurchasesCallback = callback ?? DefaultRestorePurchasesCallback;
            }
            
            public static void ResetRestorePurchasesCallback()
            {
                _restorePurchasesCallback = DefaultRestorePurchasesCallback;
            }
            
            internal static RestorePurchasesCallback GetRestorePurchasesCallback()
            {
                return _restorePurchasesCallback;
            }
            
            
            [Serializable]
            public class HardProductInfo
            {
                public string LocalizedTitle;
                public string LocalizedDescription;
                public string LocalizedPriceString;
                public float LocalizedPrice;
                public string IsoCurrencyCode;
            }
            
            private static readonly Func<string, HardProductInfo> DefaultGetHardPurchaseInfo = (string productId) =>
            {
                UnityEngine.Debug.LogWarning("You need to implement Balancy.Actions.Purchasing.SetGetHardPurchaseInfoCallback or install the Balancy Purchasing package.");
                return new HardProductInfo();
            };

            private static Func<string, HardProductInfo> _getHardPurchaseInfo = DefaultGetHardPurchaseInfo;

            public static void SetGetHardPurchaseInfoCallback(Func<string, HardProductInfo> callback)
            {
                _getHardPurchaseInfo = callback ?? DefaultGetHardPurchaseInfo;
            }

            internal static Func<string, HardProductInfo> GetHardPurchaseInfoCallback()
            {
                return _getHardPurchaseInfo;
            }
        }
    }
}
