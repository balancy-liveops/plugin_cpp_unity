using System.Runtime.InteropServices;
using Balancy.Models.SmartObjects;

namespace Balancy
{
    public static class API
    {
        public static void HardPurchaseStoreItem(StoreItem storeItem, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            Balancy.LibraryMethods.API.balancyHardPurchaseStoreItem(storeItem.GetRawPointer(), paymentInfo,
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