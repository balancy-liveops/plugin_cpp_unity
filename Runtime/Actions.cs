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

        public class PurchaseInfo
        {
            public string Receipt { get; set; }
            public string ProductId { get; set; }
            public string TransactionId { get; set; }
            public string ErrorMessage { get; set; }
            public string CurrencyCode { get; set; }
            public float Price { get; set; }
        }

        [Serializable]
        public class BalancyProductInfo
        {
            public enum PurchaseType
            {
                StoreItem,
                Offer,
                OfferGroup,
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

            public StoreItem GetStoreItem() => CMS.GetModelByUnnyId<StoreItem>(StoreItemUnnyId);
            public GameOffer GetGameOffer() => CMS.GetModelByUnnyId<GameOffer>(OfferUnnyId);
            public GameOfferGroup GetGameOfferGroup() => CMS.GetModelByUnnyId<GameOfferGroup>(OfferUnnyId);

            public void ReportThePurchase(PaymentInfo paymentInfo)
            {
                switch (Type)
                {
                    case PurchaseType.StoreItem:
                        Balancy.Callbacks.OnHardPurchasedStoreItem?.Invoke(paymentInfo, GetStoreItem());
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

        public static class Purchasing
        {
            public delegate void HardPurchaseCallback(BalancyProductInfo productInfo);
            
            private static readonly HardPurchaseCallback DefaultHardPurchaseCallback = (productInfo) =>
            {
                UnityEngine.Debug.LogWarning(
                    "No hard purchase implementation provided. Either implement your own using " +
                    "PurchaseCallbacks.SetHardPurchaseCallback or install the Balancy Purchasing package."); //TODO add here the id
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
        }
    }
}
