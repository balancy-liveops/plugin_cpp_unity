using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using Balancy.Models.SmartObjects;

namespace Balancy
{
    public static partial class API
    {
        private class CallbackWithId<T> where T : Balancy.Core.Responses.ResponseData
        {
            public int Id { get; set; }
            public Balancy.Core.ResponseCallback<T> Callback { get; set; }
            public GCHandle Handle { get; set; }
        }
        
        private struct CallbackResult
        {
            public int CallbackId;
            public Balancy.LibraryMethods.API.ResponseCallback StaticCallback;
        }
        
        private abstract class CallbackWrapperBase
        {
            public abstract void InvokeCallback(IntPtr responseDataPtr);
        }
        
        private class TypedCallbackWrapper<T> : CallbackWrapperBase where T : Balancy.Core.Responses.ResponseData
        {
            private readonly Balancy.Core.ResponseCallback<T> _callback;
            
            public TypedCallbackWrapper(Balancy.Core.ResponseCallback<T> callback)
            {
                _callback = callback;
            }
            
            public override void InvokeCallback(IntPtr responseDataPtr)
            {
                try
                {
                    var responseData = Marshal.PtrToStructure<T>(responseDataPtr);
                    _callback?.Invoke(responseData);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Exception in TypedCallbackWrapper: " + e);
                }
            }
        }
        
        private static readonly Dictionary<int, CallbackWrapperBase> _callbackStorage = new Dictionary<int, CallbackWrapperBase>();
        private static readonly object _callbackLock = new object();
        private static int _callbackIdCounter = 0;
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.API.ResponseCallback))]
        private static void StaticResponseHandler(int callbackId, IntPtr responseDataPtr)
        {
            CallbackWrapperBase callbackWrapper = null;
            lock (_callbackLock)
            {
                if (_callbackStorage.TryGetValue(callbackId, out callbackWrapper))
                {
                    _callbackStorage.Remove(callbackId);
                }
            }
            
            if (callbackWrapper != null)
            {
                try
                {
                    callbackWrapper.InvokeCallback(responseDataPtr);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Exception in StaticResponseHandler for callback {callbackId}: " + e);
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"Callback with ID {callbackId} not found");
            }
        }

        private class TypedCallbackProductsResponseDataWrapper : CallbackWrapperBase
        {
            private readonly Balancy.Core.ResponseCallback<Core.Responses.ProductsResponseData> _callback;

            public TypedCallbackProductsResponseDataWrapper(Balancy.Core.ResponseCallback<Core.Responses.ProductsResponseData> callback)
            {
                _callback = callback;
            }

            public override void InvokeCallback(IntPtr responseDataPtr)
            {
                try
                {
                    var response = Marshal.PtrToStructure<Core.Responses.InteropProductsResponseData>(responseDataPtr);
                    var count = response.size;
                    IntPtr basePtr = response.data;

                    var products = new List<Core.Responses.Product>(count);
                    int elemSize = Marshal.SizeOf<Core.Responses.InteropProductData>();
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr itemPtr = IntPtr.Add(basePtr, i * elemSize);
                        var interop = Marshal.PtrToStructure<Core.Responses.InteropProductData>(itemPtr);

                        var product = new Core.Responses.Product
                        {
                            base_id = Marshal.PtrToStringAnsi(interop.base_id),
                            type = (byte)interop.type,
                            item_id = Marshal.PtrToStringAnsi(interop.item_id),
                            name = Marshal.PtrToStringAnsi(interop.name),
                            description = Marshal.PtrToStringAnsi(interop.description),
                            localized_name = Marshal.PtrToStringAnsi(interop.localized_name),
                            localized_description = Marshal.PtrToStringAnsi(interop.localized_description),
                            price = interop.price
                        };

                        products.Add(product);
                    }

                    var res = new Core.Responses.ProductsResponseData
                    {
                        Products = products,
                        Success = response.Success,
                        ErrorCode = response.ErrorCode,
                        ErrorMessage = response.ErrorMessage,
                    };

                    _callback?.Invoke(res);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Exception in TypedCallbackProductsResponseDataWrapper: " + e);
                }
            }
        }


        private static CallbackResult ProtectedFromGCCallback<T>(Balancy.Core.ResponseCallback<T> callback, Func<Balancy.Core.ResponseCallback<T>, CallbackWrapperBase> customWrapperCreator) where T : Balancy.Core.Responses.ResponseData
        {
            int callbackId;
            lock (_callbackLock)
            {
                callbackId = ++_callbackIdCounter;
            }
            
            var wrapper = customWrapperCreator(callback);
            
            lock (_callbackLock)
            {
                _callbackStorage[callbackId] = wrapper;
            }
            
            return new CallbackResult
            {
                CallbackId = callbackId,
                StaticCallback = StaticResponseHandler
            };
        }
        
        private static CallbackResult ProtectedFromGCCallback<T>(Balancy.Core.ResponseCallback<T> callback) where T : Balancy.Core.Responses.ResponseData
        {
            int callbackId;
            lock (_callbackLock)
            {
                callbackId = ++_callbackIdCounter;
            }
            
            var wrapper = new TypedCallbackWrapper<T>(callback);
            
            lock (_callbackLock)
            {
                _callbackStorage[callbackId] = wrapper;
            }
            
            return new CallbackResult
            {
                CallbackId = callbackId,
                StaticCallback = StaticResponseHandler
            };
        }
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
        
        [Obsolete("Try not to use it")]
        public static bool SoftPurchaseStoreItem(StoreItem storeItem)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseStoreItem(storeItem?.GetRawPointer() ?? IntPtr.Zero);
        }
        
        public static bool SoftPurchaseShopSlot(ShopSlot shopSlot)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseShopSlot(shopSlot?.GetRawPointer() ?? IntPtr.Zero);
        }

        public static bool SoftPurchaseGameOffer(OfferInfo offerInfo)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseGameOffer(offerInfo?.GetRawPointer() ?? IntPtr.Zero);
        }

        public static bool SoftPurchaseGameOfferGroup(OfferGroupInfo offerGroupInfo, StoreItem storeItem)
        {
            return Balancy.LibraryMethods.API.balancySoftPurchaseGameOfferGroup(offerGroupInfo?.GetRawPointer() ?? IntPtr.Zero, storeItem?.GetRawPointer() ?? IntPtr.Zero);
        }
        
        [Obsolete("Try not to use it")]
        public static void HardPurchaseStoreItem(StoreItem storeItem, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            var callbackResult = ProtectedFromGCCallback(callback);
            Balancy.LibraryMethods.API.balancyHardPurchaseStoreItem(storeItem?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                callbackResult.CallbackId, callbackResult.StaticCallback, requireValidation);
        }
        
        public static void HardPurchaseShopSlot(ShopSlot shopSlot, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            var callbackResult = ProtectedFromGCCallback(callback);
            Balancy.LibraryMethods.API.balancyHardPurchaseShopSlot(shopSlot?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                callbackResult.CallbackId, callbackResult.StaticCallback, requireValidation);
        }

        public static void HardPurchaseGameOffer(OfferInfo offerInfo, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            var callbackResult = ProtectedFromGCCallback(callback);
            Balancy.LibraryMethods.API.balancyHardPurchaseGameOffer(offerInfo?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                callbackResult.CallbackId, callbackResult.StaticCallback, requireValidation);
        }

        public static void HardPurchaseGameOfferGroup(OfferGroupInfo offerGroupInfo, StoreItem storeItem, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            var callbackResult = ProtectedFromGCCallback(callback);
            Balancy.LibraryMethods.API.balancyHardPurchaseGameOfferGroup(offerGroupInfo?.GetRawPointer() ?? IntPtr.Zero, storeItem?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                callbackResult.CallbackId, callbackResult.StaticCallback, requireValidation);
        }

        public static void GetProducts(Balancy.Core.ResponseCallback<Balancy.Core.Responses.ProductsResponseData> callback)
        {
            var callbackResult = ProtectedFromGCCallback(callback, responseCallback => new TypedCallbackProductsResponseDataWrapper(responseCallback));

            LibraryMethods.API.balancyGetProducts(callbackResult.CallbackId, callbackResult.StaticCallback);
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

        public static class Auth
        {
            public static void AsGuest(Balancy.Core.ResponseCallback<Balancy.Core.Responses.AuthResponseData> callback) {
                var callbackResult = ProtectedFromGCCallback(callback);
                Balancy.LibraryMethods.API.balancyAuth_AsGuest(callbackResult.CallbackId, callbackResult.StaticCallback);
            }
            
            public static void WithNameAndPassword(string name, string password, Balancy.Core.ResponseCallback<Balancy.Core.Responses.AuthResponseData> callback) {
                var callbackResult = ProtectedFromGCCallback(callback);
                Balancy.LibraryMethods.API.balancyAuth_NameAndPassword(name, password, callbackResult.CallbackId, callbackResult.StaticCallback);
            }
                        
            public static void WithNutaku(string userId, string token, Balancy.Core.ResponseCallback<Balancy.Core.Responses.AuthResponseData> callback) {
                var callbackResult = ProtectedFromGCCallback(callback);
                Balancy.LibraryMethods.API.balancyAuth_Nutaku(userId, token, callbackResult.CallbackId, callbackResult.StaticCallback);
            }
        }
        
        public static class Link
        {
            public static void WithNameAndPassword(string name, string password, bool forceLink, Balancy.Core.ResponseCallback<Balancy.Core.Responses.LinkResponseData> callback) {
                var callbackResult = ProtectedFromGCCallback(callback);
                Balancy.LibraryMethods.API.balancyLink_NameAndPassword(name, password, forceLink, callbackResult.CallbackId, callbackResult.StaticCallback);
            }
        }
        
        public static class General
        {
            public static void LevelCompleted() {
                Balancy.LibraryMethods.API.balancyGenenal_LevelCompleted();
            }
            
            public static void LevelFailed() {
                Balancy.LibraryMethods.API.balancyGenenal_LevelFailed();
            }
        }
        
        public static class Inventory
        {
            public static int AddItems(Balancy.Models.SmartObjects.Item item, int count) {
                return Balancy.LibraryMethods.General.balancyInventory_AddItems(item?.GetRawPointer() ?? IntPtr.Zero, count);
            }
            
            public static int RemoveItems(Balancy.Models.SmartObjects.Item item, int count) {
                return Balancy.LibraryMethods.General.balancyInventory_RemoveItems(item?.GetRawPointer() ?? IntPtr.Zero, count);
            }
            
            public static int getTotalItemsCount(Balancy.Models.SmartObjects.Item item) {
                return Balancy.LibraryMethods.General.balancyInventory_GetTotalItemsCount(item?.GetRawPointer() ?? IntPtr.Zero);
            }
        }
        
        //This method doesn't work in production
        public static void SetTimeCheatingOffset(int seconds) => LibraryMethods.Extra.balancySetTimeOffset(seconds);
        public static int GetTimeCheatingOffset() => LibraryMethods.Extra.balancyGetTimeOffset();
    }
}