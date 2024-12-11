using System;
using System.Runtime.InteropServices;
using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects;

namespace Balancy
{
    public static class API
    {
        public static bool SoftPurchaseStoreItem(StoreItem storeItem)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseStoreItem(storeItem?.GetRawPointer() ?? IntPtr.Zero);
        }

        public static bool SoftPurchaseGameOffer(OfferInfo offerInfo)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseGameOffer(offerInfo?.GetRawPointer() ?? IntPtr.Zero);
        }

        public static bool SoftPurchaseGameOfferGroup(OfferGroupInfo offerInfo, StoreItem storeItem)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseGameOfferGroup(offerInfo?.GetRawPointer() ?? IntPtr.Zero, storeItem?.GetRawPointer() ?? IntPtr.Zero);
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

        public static void HardPurchaseGameOfferGroup(OfferGroupInfo offerInfo, StoreItem storeItem, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            Balancy.LibraryMethods.API.balancyHardPurchaseGameOfferGroup(offerInfo?.GetRawPointer() ?? IntPtr.Zero, storeItem?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                ProtectedFromGCCallback(callback), requireValidation);
        }

        private static Balancy.LibraryMethods.API.ResponseCallback ProtectedFromGCCallback<T>(Balancy.Core.ResponseCallback<T> callback) where T : Balancy.Core.Responses.ResponseData
        {
            System.Runtime.InteropServices.GCHandle? gch = null;
            Balancy.LibraryMethods.API.ResponseCallback innerCallback = (responseDataPtr) => {
                var responseData = Marshal.PtrToStructure<T>(responseDataPtr);
                if (gch.HasValue)
                    gch.Value.Free();
                callback(responseData);
            };
            
            gch = GCHandle.Alloc(innerCallback);
            return innerCallback;
        }
    }
}