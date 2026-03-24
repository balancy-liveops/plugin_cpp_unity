using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using Balancy.Models.SmartObjects.Analytics;

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
#if UNITY_WEBGL && !UNITY_EDITOR
                    // WebGL/Wasm workaround: Marshal.PtrToStructure crashes with
                    // "function signature mismatch" due to IL2CPP using long (i64) for
                    // PtrToStructure's first parameter while Wasm32 uses i32 pointers.
                    // Manually read fields from the packed struct instead.
                    var responseData = ManualMarshal<T>(responseDataPtr);
#else
                    var responseData = Marshal.PtrToStructure<T>(responseDataPtr);
#endif
                    _callback?.Invoke(responseData);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Exception in TypedCallbackWrapper: " + e);
                }
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Manual field-by-field marshaling for WebGL/Wasm where Marshal.PtrToStructure
        /// crashes due to function signature mismatch in IL2CPP's Wasm implementation.
        /// Reads Pack=1 sequential layout structs: [byte success][int errorCode][LPStr errorMessage][...derived fields]
        /// </summary>
        private static T ManualMarshal<T>(IntPtr ptr) where T : Balancy.Core.Responses.ResponseData
        {
            int offset = 0;

            // Base ResponseData fields (Pack=1):
            // byte success (1 byte)
            byte success = Marshal.ReadByte(ptr, offset);
            offset += 1;

            // int ErrorCode (4 bytes)
            int errorCode = Marshal.ReadInt32(ptr, offset);
            offset += 4;

            // const char* ErrorMessage (pointer-sized: 4 bytes on Wasm32)
            IntPtr errorMessagePtr = Marshal.ReadIntPtr(ptr, offset);
            string errorMessage = errorMessagePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorMessagePtr) : null;
            offset += IntPtr.Size;

            if (typeof(T) == typeof(Balancy.Core.Responses.AuthResponseData))
            {
                // const char* UserId (pointer-sized)
                IntPtr userIdPtr = Marshal.ReadIntPtr(ptr, offset);
                string userId = userIdPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(userIdPtr) : null;

                var result = new Balancy.Core.Responses.AuthResponseData();
                result.Success = success == 1;
                result.ErrorCode = errorCode;
                result.ErrorMessage = errorMessage;
                result.UserId = userId;
                return result as T;
            }
            else if (typeof(T) == typeof(Balancy.Core.Responses.LinkResponseData))
            {
                IntPtr userIdPtr = Marshal.ReadIntPtr(ptr, offset);
                string userId = userIdPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(userIdPtr) : null;

                var result = new Balancy.Core.Responses.LinkResponseData();
                result.Success = success == 1;
                result.ErrorCode = errorCode;
                result.ErrorMessage = errorMessage;
                result.UserId = userId;
                return result as T;
            }
            else if (typeof(T) == typeof(Balancy.Core.Responses.PurchaseProductResponseData))
            {
                // const char* ProductId (pointer-sized)
                IntPtr productIdPtr = Marshal.ReadIntPtr(ptr, offset);
                string productId = productIdPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(productIdPtr) : null;
                offset += IntPtr.Size;

                // int removeFromPending (4 bytes) - private field, use reflection
                int removeFromPending = Marshal.ReadInt32(ptr, offset);

                var result = new Balancy.Core.Responses.PurchaseProductResponseData();
                result.Success = success == 1;
                result.ErrorCode = errorCode;
                result.ErrorMessage = errorMessage;
                result.ProductId = productId;
                var field = typeof(Balancy.Core.Responses.PurchaseProductResponseData)
                    .GetField("removeFromPending", BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(result, removeFromPending);
                return result as T;
            }
            else
            {
                // Fallback for unknown types - create base ResponseData
                var result = System.Activator.CreateInstance<T>();
                result.Success = success == 1;
                result.ErrorCode = errorCode;
                result.ErrorMessage = errorMessage;
                return result;
            }
        }
#endif
        
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
#if UNITY_WEBGL && !UNITY_EDITOR
        private static Core.Responses.Product ConstructProductWebGL(IntPtr dataPtr, int index)
        {
            // InteropProductData struct layout (Pack=1):
            // IntPtr base_id, int type, IntPtr item_id, IntPtr name, IntPtr description,
            // IntPtr localized_name, IntPtr localized_description, float price
            int elemSize = IntPtr.Size * 7 + 4 + 4; // 6 pointers + 1 int(type) + 1 float(price)
            int pOff = 0;
            IntPtr itemPtr = IntPtr.Add(dataPtr, index * elemSize);
            IntPtr baseIdPtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            int pType = Marshal.ReadInt32(itemPtr, pOff); pOff += 4;
            IntPtr itemIdPtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            IntPtr namePtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            IntPtr descPtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            IntPtr locNamePtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            IntPtr locDescPtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            IntPtr iconPtr = Marshal.ReadIntPtr(itemPtr, pOff); pOff += IntPtr.Size;
            // float is 4 bytes - read as int bits and convert
            int priceBits = Marshal.ReadInt32(itemPtr, pOff);
            float priceVal = BitConverter.ToSingle(BitConverter.GetBytes(priceBits), 0);

            return new Core.Responses.Product
            {
                base_id = baseIdPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(baseIdPtr) : null,
                type = (byte)pType,
                item_id = itemIdPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(itemIdPtr) : null,
                name = namePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(namePtr) : null,
                description = descPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(descPtr) : null,
                localized_name = locNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(locNamePtr) : null,
                localized_description = locDescPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(locDescPtr) : null,
                icon = iconPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(iconPtr) : null,
                price = priceVal
                
            };
        }
#else
        private static Core.Responses.Product ConstructProductWebGL(IntPtr dataPtr, int index)
        {
            int elemSize = Marshal.SizeOf<Core.Responses.InteropProductData>();
            IntPtr itemPtr = IntPtr.Add(dataPtr, index * elemSize);
            var interop = Marshal.PtrToStructure<Core.Responses.InteropProductData>(itemPtr);

            return new Core.Responses.Product
            {
                base_id = Marshal.PtrToStringAnsi(interop.base_id),
                type = (byte)interop.type,
                item_id = Marshal.PtrToStringAnsi(interop.item_id),
                name = Marshal.PtrToStringAnsi(interop.name),
                description = Marshal.PtrToStringAnsi(interop.description),
                localized_name = Marshal.PtrToStringAnsi(interop.localized_name),
                localized_description = Marshal.PtrToStringAnsi(interop.localized_description),
                icon =  Marshal.PtrToStringAnsi(interop.icon),
                price = interop.price,
            };
        }
#endif
        
        private class TypedCallbackProductResponseDataWrapper : CallbackWrapperBase
        {
            private readonly Balancy.Core.ResponseCallback<Core.Responses.ProductResponseData> _callback;

            public TypedCallbackProductResponseDataWrapper(Balancy.Core.ResponseCallback<Core.Responses.ProductResponseData> callback)
            {
                _callback = callback;
            }

            public override void InvokeCallback(IntPtr responseDataPtr)
            {
                try
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    // WebGL/Wasm workaround: manually read fields to avoid Marshal.PtrToStructure crash
                    int offset = 0;
                    byte success = Marshal.ReadByte(responseDataPtr, offset); offset += 1;
                    int errorCode = Marshal.ReadInt32(responseDataPtr, offset); offset += 4;
                    IntPtr errorMsgPtr = Marshal.ReadIntPtr(responseDataPtr, offset); offset += IntPtr.Size;
                    string errorMessage = errorMsgPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorMsgPtr) : null;
                    // InteropProductsResponseData derived fields:
                    IntPtr dataPtr = Marshal.ReadIntPtr(responseDataPtr, offset); offset += IntPtr.Size;
#else
                    var response = Marshal.PtrToStructure<Core.Responses.InteropProductResponseData>(responseDataPtr);
                    IntPtr dataPtr = response.data;
                    byte success = (byte)(response.Success ? 1 : 0);
                    int errorCode = response.ErrorCode;
                    string errorMessage = response.ErrorMessage;
#endif
                    Core.Responses.Product product = ConstructProductWebGL(dataPtr, 0);

                    var res = new Core.Responses.ProductResponseData
                    {
                        Product = product,
                        Success = success == 1,
                        ErrorCode = errorCode,
                        ErrorMessage = errorMessage,
                    };

                    _callback?.Invoke(res);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Exception in TypedCallbackProductResponseDataWrapper: " + e);
                }
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
#if UNITY_WEBGL && !UNITY_EDITOR
                    // WebGL/Wasm workaround: manually read fields to avoid Marshal.PtrToStructure crash
                    int offset = 0;
                    byte success = Marshal.ReadByte(responseDataPtr, offset); offset += 1;
                    int errorCode = Marshal.ReadInt32(responseDataPtr, offset); offset += 4;
                    IntPtr errorMsgPtr = Marshal.ReadIntPtr(responseDataPtr, offset); offset += IntPtr.Size;
                    string errorMessage = errorMsgPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorMsgPtr) : null;
                    // InteropProductsResponseData derived fields:
                    IntPtr dataPtr = Marshal.ReadIntPtr(responseDataPtr, offset); offset += IntPtr.Size;
                    int count = Marshal.ReadInt32(responseDataPtr, offset);
#else
                    var response = Marshal.PtrToStructure<Core.Responses.InteropProductsResponseData>(responseDataPtr);
                    var count = response.size;
                    IntPtr dataPtr = response.data;
                    byte success = (byte)(response.Success ? 1 : 0);
                    int errorCode = response.ErrorCode;
                    string errorMessage = response.ErrorMessage;
#endif

                    var products = new List<Core.Responses.Product>(count);
#if UNITY_WEBGL && !UNITY_EDITOR
                    for (int i = 0; i < count; i++)
                    {
                        var product = ConstructProductWebGL(dataPtr, i);
                        products.Add(product);
                    }
#else
                    for (int i = 0; i < count; i++)
                    {
                        var product = ConstructProductWebGL(dataPtr, i);

                        products.Add(product);
                    }
#endif
                    var res = new Core.Responses.ProductsResponseData
                    {
                        Products = products,
                        Success = success == 1,
                        ErrorCode = errorCode,
                        ErrorMessage = errorMessage,
                    };

                    _callback?.Invoke(res);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Exception in TypedCallbackProductsResponseDataWrapper: " + e);
                }
            }
        }
        
        private static CallbackResult ProtectedFromGCCallback<T>(Balancy.Core.ResponseCallback<T> callback, Func<Balancy.Core.ResponseCallback<T>, CallbackWrapperBase> customWrapperCreator = null)  where T : Balancy.Core.Responses.ResponseData
        {
            int callbackId;
            lock (_callbackLock)
            {
                callbackId = ++_callbackIdCounter;
            }
            
            var wrapper = customWrapperCreator != null ? customWrapperCreator(callback) : new TypedCallbackWrapper<T>(callback);
            
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
        
        public static bool SoftPurchaseShopSlot(Balancy.Data.SmartObjects.ShopSlot shopSlot)
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
        
        public static void HardPurchaseShopSlot(Balancy.Data.SmartObjects.ShopSlot shopSlot, Balancy.Core.PaymentInfo paymentInfo,
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.PurchaseProductResponseData> callback, bool requireValidation)
        {
            var callbackResult = ProtectedFromGCCallback(callback);
            Balancy.LibraryMethods.API.balancyHardPurchaseShopSlot(shopSlot?.GetRawPointer() ?? IntPtr.Zero, paymentInfo,
                callbackResult.CallbackId, callbackResult.StaticCallback, requireValidation);
        }

        public static void GetProduct(string productId, Balancy.Core.ResponseCallback<Balancy.Core.Responses.ProductResponseData> callback)
        {
            var callbackResult = ProtectedFromGCCallback(callback, responseCallback => new TypedCallbackProductResponseDataWrapper(responseCallback));

            LibraryMethods.API.balancyGetProduct(productId, callbackResult.CallbackId, callbackResult.StaticCallback);
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
        
        public static bool StartAbTestManually(ABTest abTest, ABTestVariant variant)
        {
            return Balancy.LibraryMethods.General.balancyStartAbTestManually(abTest?.GetRawPointer() ?? IntPtr.Zero, variant?.GetRawPointer() ?? IntPtr.Zero);
        }

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
            public static void LevelStarted() {
                Balancy.LibraryMethods.API.balancyGenenal_LevelStarted();
            }
            
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

            public static int GetTotalItemsCount(Balancy.Models.SmartObjects.Item item) {
                return Balancy.LibraryMethods.General.balancyInventory_GetTotalItemsCount(item?.GetRawPointer() ?? IntPtr.Zero);
            }
        }

        public static class Tasks
        {
            public static void ActivateTask(Balancy.Models.LiveOps.Tasks.BaseTask task, GameEvent gameEvent = null) {
                Balancy.LibraryMethods.API.balancyTasks_ActivateTask(
                    task?.GetRawPointer() ?? IntPtr.Zero,
                    gameEvent?.GetRawPointer() ?? IntPtr.Zero);
            }

            public static void DeactivateTask(Balancy.Models.LiveOps.Tasks.BaseTask task) {
                Balancy.LibraryMethods.API.balancyTasks_DeactivateTask(task?.GetRawPointer() ?? IntPtr.Zero);
            }

            public static bool ClaimReward(Balancy.Models.LiveOps.Tasks.BaseTask task) {
                return Balancy.LibraryMethods.API.balancyTasks_ClaimReward(task?.GetRawPointer() ?? IntPtr.Zero);
            }

            public static void RestoreFailedTask(Balancy.Models.LiveOps.Tasks.BaseTask task) {
                Balancy.LibraryMethods.API.balancyTasks_RestoreFailedTask(task?.GetRawPointer() ?? IntPtr.Zero);
            }
        }
        
        public static class VisualScripting
        {
            /// <summary>
            /// Run a visual script by its CMS ID.
            /// </summary>
            /// <param name="scriptId">The script's ID in the CMS.</param>
            /// <param name="launcherId">Optional identifier for who launched the script (e.g., a GameEvent UnnyId). Accessible inside the script via GetLauncherId().</param>
            /// <param name="inputJson">Optional JSON string with input parameters keyed by port name. Example: {"GameEvent":"unnyId123","score":100}</param>
            /// <returns>The instance ID of the running script, or empty string on failure.</returns>
            public static string RunScriptById(string scriptId, string launcherId = null, string inputJson = null, Action<string, string> onComplete = null) {
                var instanceId = Marshal.PtrToStringAnsi(
                    Balancy.LibraryMethods.API.balancyScripts_RunById(scriptId, launcherId ?? "", inputJson ?? ""));
                if (onComplete != null && !string.IsNullOrEmpty(instanceId))
                    ScriptCompletionManager.Register(instanceId, onComplete);
                return instanceId;
            }

            /// <summary>
            /// Run a visual script by its name.
            /// </summary>
            /// <param name="scriptName">The script's name in the CMS.</param>
            /// <param name="launcherId">Optional identifier for who launched the script.</param>
            /// <param name="inputJson">Optional JSON string with input parameters keyed by port name.</param>
            /// <param name="onComplete">Optional callback invoked when the script finishes. Receives (exitPortName, outputsJson).</param>
            /// <returns>The instance ID of the running script, or empty string on failure.</returns>
            public static string RunScriptByName(string scriptName, string launcherId = null, string inputJson = null, Action<string, string> onComplete = null) {
                var instanceId = Marshal.PtrToStringAnsi(
                    Balancy.LibraryMethods.API.balancyScripts_RunByName(scriptName, launcherId ?? "", inputJson ?? ""));
                if (onComplete != null && !string.IsNullOrEmpty(instanceId))
                    ScriptCompletionManager.Register(instanceId, onComplete);
                return instanceId;
            }

            /// <summary>
            /// Stop a running visual script by its instance ID.
            /// </summary>
            /// <param name="instanceId">The instance ID returned from RunScriptById/RunScriptByName.</param>
            /// <returns>True if the script was found and stopped.</returns>
            public static bool StopScript(string instanceId) {
                return Balancy.LibraryMethods.API.balancyScripts_Stop(instanceId);
            }
        }

        //This method doesn't work in production
        public static void SetTimeCheatingOffset(int seconds) => LibraryMethods.Extra.balancySetTimeOffset(seconds);
        public static int GetTimeCheatingOffset() => LibraryMethods.Extra.balancyGetTimeOffset();

        public static void NotifyAppPause(int secondsElapsed) => LibraryMethods.Extra.balancyNotifyAppPause(secondsElapsed);
        public static void NotifyAppResume() => LibraryMethods.Extra.balancyNotifyAppResume();
    }
}