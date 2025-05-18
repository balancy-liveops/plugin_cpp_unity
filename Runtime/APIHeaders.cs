using System;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using Balancy.Models.SmartObjects;

namespace Balancy
{
    public static partial class API
    {
        private static BalancyStatus _status;
        
        public enum AdType
        {
            None = 0,
            Rewarded,
            Interstitial,
            Custom
        }

        public static BalancyStatus GetStatus()
        {
            var ptr = Balancy.LibraryMethods.General.balancyGetStatus();
            if (_status == null)
                _status = new BalancyStatus();
            _status.SetData(ptr);
            return _status;
        }
        
        public static bool SoftPurchaseStoreItem(StoreItem storeItem)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseStoreItem(storeItem?.GetRawPointer() ?? IntPtr.Zero);
        }

        public static bool SoftPurchaseGameOffer(OfferInfo offerInfo)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseGameOffer(offerInfo?.GetRawPointer() ?? IntPtr.Zero);
        }

        public static bool SoftPurchaseGameOfferGroup(OfferGroupInfo offerGroupInfo, StoreItem storeItem)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseGameOfferGroup(offerGroupInfo?.GetRawPointer() ?? IntPtr.Zero, storeItem?.GetRawPointer() ?? IntPtr.Zero);
        }
        
        public static void HardPurchaseStoreItem(StoreItem storeItem, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            Balancy.LibraryMethods.API.balancyHardPurchaseStoreItem(storeItem?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                ProtectedFromGCCallback(callback), requireValidation);
        }

        public static void HardPurchaseGameOffer(OfferInfo offerInfo, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            Balancy.LibraryMethods.API.balancyHardPurchaseGameOffer(offerInfo?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                ProtectedFromGCCallback(callback), requireValidation);
        }

        public static void HardPurchaseGameOfferGroup(OfferGroupInfo offerGroupInfo, StoreItem storeItem, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            Balancy.LibraryMethods.API.balancyHardPurchaseGameOfferGroup(offerGroupInfo?.GetRawPointer() ?? IntPtr.Zero, storeItem?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                ProtectedFromGCCallback(callback), requireValidation);
        }

        public static void TrackAdRevenue(AdType type, double revenue, string placement) => 
            LibraryMethods.Profile.balancySystemProfileTrackRevenue(type, revenue, placement);

        public static class Localization
        {
            public static string GetLocalizedValue(string key) {
                return JsonBasedObject.GetStringFromIntPtr(Balancy.LibraryMethods.Localization.balancyLocalization_GetLocalizedValue(key));
            }
            
            public static void ChangeLocalization(string code) {
                Balancy.LibraryMethods.Localization.balancyLocalization_ChangeLocalization(code);
            }
            
            public static string GetCurrentLocalizationCode() {
                return JsonBasedObject.GetStringFromIntPtr(Balancy.LibraryMethods.Localization.balancyLocalization_GetCurrentLocalizationCode());
            }
            
            public static string[] GetAllLocalizationCodes() {
                IntPtr ptr = Balancy.LibraryMethods.Localization.balancyLocalization_GetAllLocalizationCodes(out int size);
                return JsonBasedObject.ReadStringArrayValues(ptr, size);
            }
        }
        
        //This method doesn't work in production
        public static void SetTimeCheatingOffset(int seconds) => LibraryMethods.Extra.balancySetTimeOffset(seconds);
        public static int GetTimeCheatingOffset() => LibraryMethods.Extra.balancyGetTimeOffset();
    }
}