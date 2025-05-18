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

        // Additional information about the purchase
        public class PurchaseInfo
        {
            // Receipt data for validation
            public string Receipt { get; set; }

            // Product ID from the store
            public string ProductId { get; set; }

            public string TransactionId { get; set; }

            // Error message if purchase failed
            public string ErrorMessage { get; set; }

            // Currency code (e.g., USD, EUR)
            public string CurrencyCode { get; set; }

            // Price of the item in the specified currency
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

        // Delegate for the hard purchase callback
        public delegate void HardPurchaseCallback(BalancyProductInfo productInfo);

        public static class Purchasing
        {
            // Default implementation that just logs a warning
            private static readonly HardPurchaseCallback DefaultHardPurchaseCallback = (productInfo) =>
            {
                UnityEngine.Debug.LogWarning(
                    "No hard purchase implementation provided. Either implement your own using " +
                    "PurchaseCallbacks.SetHardPurchaseCallback or install the Balancy Purchasing package."); //TODO add here the id
            };

            // Current implementation - defaults to the warning
            private static HardPurchaseCallback _hardPurchaseCallback = DefaultHardPurchaseCallback;

            // Method for developers to set their own implementation
            public static void SetHardPurchaseCallback(HardPurchaseCallback callback)
            {
                _hardPurchaseCallback = callback ?? DefaultHardPurchaseCallback;
            }

            // Reset to default implementation (useful for testing)
            public static void ResetHardPurchaseCallback()
            {
                _hardPurchaseCallback = DefaultHardPurchaseCallback;
            }

            // Internal method to get the current implementation
            internal static HardPurchaseCallback GetHardPurchaseCallback()
            {
                return _hardPurchaseCallback;
            }
        }
    }
}
