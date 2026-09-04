using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy
{
    public static partial class API
    {
        private class CallbacksData
        {
            public Actions.BalancyProductInfo ProductInfo;
            public Action<bool, string> Callback;

            public CallbacksData(Actions.BalancyProductInfo productInfo, Action<bool, string> callback)
            {
                ProductInfo = productInfo;
                Callback = callback;
            }
        }

        private static List<CallbacksData> _callbacks = new List<CallbacksData>();
        private static readonly object _purchaseCallbacksLock = new object();

        private static void HardPurchase(Actions.BalancyProductInfo productInfo, Action<bool, string> callback)
        {
            if (productInfo == null)
            {
                InvokePurchaseCallback(callback, false, "Product info is null");
                return;
            }

            var pending = new CallbacksData(productInfo, callback);
            lock (_purchaseCallbacksLock)
                _callbacks.Add(pending);
            try
            {
                Balancy.Actions.Purchasing.GetHardPurchaseCallback()(productInfo);
            }
            catch (Exception exception)
            {
                lock (_purchaseCallbacksLock)
                    _callbacks.Remove(pending);
                Debug.LogException(exception);
                InvokePurchaseCallback(callback, false, exception.Message);
            }
        }

        private static CallbacksData GetCallbackData(Actions.BalancyProductInfo productInfo)
        {
            lock (_purchaseCallbacksLock)
            {
                // Prefer exact identity. If a platform plugin serializes and rebuilds
                // the descriptor, fall back to FIFO value matching.
                for (int i = 0; i < _callbacks.Count; i++)
                {
                    if (ReferenceEquals(_callbacks[i].ProductInfo, productInfo))
                    {
                        var exact = _callbacks[i];
                        _callbacks.RemoveAt(i);
                        return exact;
                    }
                }
                for (int i = 0; i < _callbacks.Count; i++)
                {
                    if (_callbacks[i].ProductInfo?.Equals(productInfo) == true)
                    {
                        var equivalent = _callbacks[i];
                        _callbacks.RemoveAt(i);
                        return equivalent;
                    }
                }
            }

            return null;
        }

        private static void CleanUpPendingPurchaseCallbacks()
        {
            CallbacksData[] pending;
            lock (_purchaseCallbacksLock)
            {
                pending = _callbacks.ToArray();
                _callbacks.Clear();
            }

            foreach (var item in pending)
                InvokePurchaseCallback(item.Callback, false, "SDK stopped");
        }

        private static void InvokePurchaseCallback(Action<bool, string> callback, bool success, string error)
        {
            try { callback?.Invoke(success, error); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void InvokeValidationCallback(Action<bool, bool> callback, bool success, bool removeFromPending)
        {
            try { callback?.Invoke(success, removeFromPending); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static void NutakuCompletePurchase(int userId, string orderId, Balancy.Core.ResponseCallback<Balancy.Core.Responses.CompletePurchaseResponseData> callback)
        {
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.InteropCompletePurchaseResponseData> resCallback = data =>
            {
                var resData = new Balancy.Core.Responses.CompletePurchaseResponseData
                {
                    Success = data.Success,
                    ErrorCode = data.ErrorCode,
                    ErrorMessage = data.ErrorMessage,
                    Data = string.IsNullOrEmpty(data.data)
                        ? null
                        : JsonUtility.FromJson<Balancy.Core.Responses.CompletePurchaseData>(data.data)
                };
                callback?.Invoke(resData);
            };

            var callbackResult = ProtectedFromGCCallback(resCallback);

            LibraryMethods.API.balancyNutakuComplete(userId, orderId, callbackResult.CallbackId, callbackResult.StaticCallback);
        }

        public static void HoolipayPending(string itemId, int platform, Balancy.Core.ResponseCallback<Balancy.Core.Responses.HoolipayPendingResponseData> callback)
        {
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.InteropCompletePurchaseResponseData> resCallback = data =>
            {
                var resData = new Balancy.Core.Responses.HoolipayPendingResponseData
                {
                    Success = data.Success,
                    ErrorCode = data.ErrorCode,
                    ErrorMessage = data.ErrorMessage,
                    Data = string.IsNullOrEmpty(data.data)
                        ? null
                        : JsonUtility.FromJson<Balancy.Core.Responses.HoolipayPendingData>(data.data)
                };
                callback?.Invoke(resData);
            };

            var callbackResult = ProtectedFromGCCallback(resCallback);

            LibraryMethods.API.balancyHoolipayPending(itemId, platform, callbackResult.CallbackId, callbackResult.StaticCallback);
        }

        public static void HoolipayClaim(string orderId, Balancy.Core.ResponseCallback<Balancy.Core.Responses.CompletePurchaseResponseData> callback)
        {
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.InteropCompletePurchaseResponseData> resCallback = data =>
            {
                var resData = new Balancy.Core.Responses.CompletePurchaseResponseData
                {
                    Success = data.Success,
                    ErrorCode = data.ErrorCode,
                    ErrorMessage = data.ErrorMessage,
                    Data = string.IsNullOrEmpty(data.data)
                        ? null
                        : JsonUtility.FromJson<Balancy.Core.Responses.CompletePurchaseData>(data.data)
                };
                callback?.Invoke(resData);
            };

            var callbackResult = ProtectedFromGCCallback(resCallback);

            LibraryMethods.API.balancyHoolipayClaim(orderId, callbackResult.CallbackId, callbackResult.StaticCallback);
        }

        public static void SteamInitPurchase(string steamId, string itemId, string description, Balancy.Core.ResponseCallback<Balancy.Core.Responses.CompletePurchaseResponseData> callback)
        {
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.InteropCompletePurchaseResponseData> resCallback = data =>
            {
                var resData = new Balancy.Core.Responses.CompletePurchaseResponseData
                {
                    Success = data.Success,
                    ErrorCode = data.ErrorCode,
                    ErrorMessage = data.ErrorMessage,
                    Data = string.IsNullOrEmpty(data.data)
                        ? null
                        : JsonUtility.FromJson<Balancy.Core.Responses.CompletePurchaseData>(data.data)
                };
                callback?.Invoke(resData);
            };

            var callbackResult = ProtectedFromGCCallback(resCallback);

            LibraryMethods.API.balancySteamInit(steamId, itemId, description, callbackResult.CallbackId, callbackResult.StaticCallback);
        }

        public static void SteamCompletePurchase(string orderId, Balancy.Core.ResponseCallback<Balancy.Core.Responses.CompletePurchaseResponseData> callback)
        {
            Balancy.Core.ResponseCallback<Balancy.Core.Responses.InteropCompletePurchaseResponseData> resCallback = data =>
            {
                var resData = new Balancy.Core.Responses.CompletePurchaseResponseData
                {
                    Success = data.Success,
                    ErrorCode = data.ErrorCode,
                    ErrorMessage = data.ErrorMessage,
                    Data = string.IsNullOrEmpty(data.data)
                        ? null
                        : JsonUtility.FromJson<Balancy.Core.Responses.CompletePurchaseData>(data.data)
                };
                callback?.Invoke(resData);
            };

            var callbackResult = ProtectedFromGCCallback(resCallback);

            LibraryMethods.API.balancySteamComplete(orderId, callbackResult.CallbackId, callbackResult.StaticCallback);
        }

        public static void FinalizedHardPurchase(Actions.PurchaseResult result,
            Balancy.Actions.BalancyProductInfo productInfo, Core.PaymentInfo paymentInfo,
            Action<bool, bool> validationCallback, bool requireReceiptValidation = true)
        {
            Debug.Log("HardPurchase result: " + result);
            Debug.Log("HardPurchase has receipt: " + !string.IsNullOrEmpty(paymentInfo?.Receipt));

// #if UNITY_EDITOR
//             bool requireValidation = false;
//             // paymentInfo.Receipt = "{\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"" +
//             //                       paymentInfo.OrderId + "\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"" +
//             //                       paymentInfo.ProductId + "\\\\\\\"}\\\",\\\"signature\\\":\\\"bypass\\\"}\"}";
// #else
//             bool requireValidation = true;
// #endif
            bool requireValidation = requireReceiptValidation;
            if (productInfo != null)
            {
                var callback = GetCallbackData(productInfo);

                if (result == Actions.PurchaseResult.Success)
                {
                    if (paymentInfo == null)
                    {
                        InvokePurchaseCallback(callback?.Callback, false, "Payment info is null");
                        return;
                    }

                    void InvokeCallbacks(Balancy.Core.Responses.PurchaseProductResponseData responseData)
                    {
                        Debug.Log(
                            $"Response: {responseData.Success} ErrorCode = {responseData.ErrorCode} Message = {responseData.ErrorMessage} Product = {responseData.ProductId}");

                        InvokeValidationCallback(validationCallback, responseData.Success, responseData.RemoveFromPending);
                        InvokePurchaseCallback(callback?.Callback, responseData.Success, responseData.ErrorMessage);

                        if (responseData.Success)
                        {
                            paymentInfo.PriceUSD = responseData.PriceUSD;
                            try { productInfo.ReportThePurchase(paymentInfo); }
                            catch (Exception exception) { Debug.LogException(exception); }
                        }
                    }

                    switch (productInfo.Type)
                    {
                        case Actions.BalancyProductInfo.PurchaseType.StoreItem:
                        {
                            var storeItem = productInfo.GetStoreItem();
                            HardPurchaseStoreItem(storeItem, paymentInfo, InvokeCallbacks, requireValidation);
                            break;
                        }
                        case Actions.BalancyProductInfo.PurchaseType.ShopSlot:
                        {
                            var shopSlot =
                                Balancy.Profiles.System.ShopsInfo.FindShopSlot(productInfo.OfferUnnyId);
                            if (shopSlot != null)
                                HardPurchaseShopSlot(shopSlot, paymentInfo, InvokeCallbacks, requireValidation);
                            else
                            {
                                var storeItem = productInfo.GetStoreItem();
                                HardPurchaseStoreItem(storeItem, paymentInfo, InvokeCallbacks, requireValidation);
                            }
                            break;
                        }
                        case Actions.BalancyProductInfo.PurchaseType.Offer:
                        {
                            var offerInfo =
                                Balancy.Profiles.System.SmartInfo.FindOfferInfo(productInfo.OfferInstanceId);
                            if (offerInfo != null)
                            {
                                HardPurchaseGameOffer(offerInfo, paymentInfo, InvokeCallbacks, requireValidation);
                            }
                            else
                            {
                                // Offer instance is gone (stale ID from previous session or deactivated mid-purchase).
                                // Fall back to completing the purchase via StoreItem so the user gets what they paid for.
                                var storeItem = productInfo.GetStoreItem();
                                if (storeItem != null)
                                {
                                    Debug.LogWarning($"OfferInfo not found for InstanceId={productInfo.OfferInstanceId}, falling back to StoreItem purchase.");
                                    HardPurchaseStoreItem(storeItem, paymentInfo, InvokeCallbacks, requireValidation);
                                }
                                else
                                {
                                    InvokeValidationCallback(validationCallback, false, false);
                                    InvokePurchaseCallback(callback?.Callback, false, Constants.Errors.OfferInfoNull);
                                }
                            }

                            break;
                        }
                        case Actions.BalancyProductInfo.PurchaseType.OfferGroup:
                        {
                            var offerGroupInfo =
                                Balancy.Profiles.System.SmartInfo.FindOfferGroupInfo(productInfo.OfferInstanceId);
                            if (offerGroupInfo != null)
                            {
                                var storeItem = productInfo.GetStoreItem();
                                HardPurchaseGameOfferGroup(offerGroupInfo, storeItem, paymentInfo,
                                    InvokeCallbacks, requireValidation);
                            }
                            else
                            {
                                // OfferGroup instance is gone — fall back to StoreItem purchase.
                                var storeItem = productInfo.GetStoreItem();
                                if (storeItem != null)
                                {
                                    Debug.LogWarning($"OfferGroupInfo not found for InstanceId={productInfo.OfferInstanceId}, falling back to StoreItem purchase.");
                                    HardPurchaseStoreItem(storeItem, paymentInfo, InvokeCallbacks, requireValidation);
                                }
                                else
                                {
                                    InvokeValidationCallback(validationCallback, false, false);
                                    InvokePurchaseCallback(callback?.Callback, false, Constants.Errors.OfferGroupInfoNull);
                                }
                            }

                            break;
                        }
                        default:
                            InvokeValidationCallback(validationCallback, false, false);
                            InvokePurchaseCallback(callback?.Callback, false, "Unsupported purchase type");
                            break;
                    }
                }
                else
                {
                    InvokePurchaseCallback(callback?.Callback, false, "");
                }
            }
            else
            {
                Debug.LogError("productInfo is null -> can't validate it");
            }
        }

        private static bool CheckSoftPrice(StoreItem storeItem)
        {
            if (storeItem.HaveEnoughResources())
                return true;

            if (storeItem.Price.Type == PriceType.Ads)
                Balancy.Actions.Ads.GetAdWatchCallback()?.Invoke((success) =>
                {
                    if (success)
                        storeItem.AdWasWatched();
                });

            return false;
        }

        public static void PrepareWebView(Action onReady = null)
        {
            RenderViewsManager.PrepareWebView(onReady);
        }

        public static void ShowWebView()
        {
            RenderViewsManager.ShowWebView();
        }

        public static void HideWebView()
        {
            RenderViewsManager.HideWebView();
        }
        
        public static void InitPurchaseShop(Balancy.Data.SmartObjects.ShopSlot shopSlot, Action<bool, string> callback)
        {
            if (shopSlot?.Slot == null)
            {
                callback?.Invoke(false, Constants.Errors.ShopSlotNull);
                return;
            }

            if (shopSlot.Slot.StoreItem == null)
            {
                callback?.Invoke(false, Constants.Errors.StoreItemNull);
                return;
            }

            // Fail before opening a platform purchase or rewarded ad flow. The
            // native core validates the slot again immediately before granting
            // the purchase to protect against stale state and direct callers.
            if (!shopSlot.IsAvailable())
            {
                callback?.Invoke(false, Constants.Errors.ShopSlotNotAvailable);
                return;
            }

            if (shopSlot.Slot.StoreItem.Price.Type == PriceType.Hard &&
                !shopSlot.Slot.StoreItem.Price.IsFree())
            {
                HardPurchase(new Actions.BalancyProductInfo(shopSlot), callback);
            }
            else
            {
                if (!CheckSoftPrice(shopSlot.Slot.StoreItem) || !SoftPurchaseShopSlot(shopSlot))
                {
                    switch (shopSlot.Slot.StoreItem.Price.Type)
                    {
                        case PriceType.Soft:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotEnoughItems);
                            break;
                        case PriceType.Ads:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotAds);
                            break;
                        default:
                            callback?.Invoke(false, Constants.Errors.PurchaseInvalidPriceType);
                            break;
                    }
                } else
                    callback?.Invoke(true, null);
            }
        }

        public static void InitPurchaseOffer(OfferInfo offerInfo, Action<bool, string> callback)
        {
            if (offerInfo?.GameOffer == null)
            {
                callback?.Invoke(false, Constants.Errors.GameOfferNull);
                return;
            }

            if (offerInfo.GameOffer.StoreItem == null)
            {
                callback?.Invoke(false, Constants.Errors.StoreItemNull);
                return;
            }

            if (offerInfo.GameOffer.StoreItem.Price.Type == PriceType.Hard &&
                !offerInfo.GameOffer.StoreItem.Price.IsFree())
            {
                HardPurchase(new Actions.BalancyProductInfo(offerInfo), callback);
            }
            else
            {
                if (!CheckSoftPrice(offerInfo.GameOffer.StoreItem) || !SoftPurchaseGameOffer(offerInfo))
                {
                    switch (offerInfo.GameOffer.StoreItem.Price.Type)
                    {
                        case PriceType.Soft:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotEnoughItems);
                            break;
                        case PriceType.Ads:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotAds);
                            break;
                        default:
                            callback?.Invoke(false, Constants.Errors.PurchaseInvalidPriceType);
                            break;
                    }
                } else
                    callback?.Invoke(true, null);
            }
        }

        public static void InitPurchaseOffer(OfferGroupInfo offerGroupInfo, StoreItem storeItem,
            Action<bool, string> callback)
        {
            if (offerGroupInfo?.GameOfferGroup == null)
            {
                callback?.Invoke(false, Constants.Errors.GameOfferGroupNull);
                return;
            }

            if (storeItem == null)
            {
                callback?.Invoke(false, Constants.Errors.StoreItemNull);
                return;
            }

            if (storeItem.Price.Type == PriceType.Hard && !storeItem.Price.IsFree())
            {
                HardPurchase(new Actions.BalancyProductInfo(offerGroupInfo, storeItem), callback);
            }
            else
            {
                if (!CheckSoftPrice(storeItem) || !SoftPurchaseGameOfferGroup(offerGroupInfo, storeItem))
                {
                    switch (storeItem.Price.Type)
                    {
                        case PriceType.Soft:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotEnoughItems);
                            break;
                        case PriceType.Ads:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotAds);
                            break;
                        default:
                            callback?.Invoke(false, Constants.Errors.PurchaseInvalidPriceType);
                            break;
                    }
                } else
                    callback?.Invoke(true, null);
            }
        }

        [Obsolete("Try not to use it")]
        public static void InitPurchase(StoreItem storeItem, Action<bool, string> callback)
        {
            if (storeItem == null)
            {
                callback?.Invoke(false, Constants.Errors.StoreItemNull);
                return;
            }

            if (storeItem.Price.Type == PriceType.Hard && !storeItem.Price.IsFree())
            {
                HardPurchase(new Actions.BalancyProductInfo(storeItem), callback);
            }
            else
            {
                if (!CheckSoftPrice(storeItem) || !SoftPurchaseStoreItem(storeItem))
                {
                    switch (storeItem.Price.Type)
                    {
                        case PriceType.Soft:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotEnoughItems);
                            break;
                        case PriceType.Ads:
                            callback?.Invoke(false, Constants.Errors.PurchaseNotAds);
                            break;
                        default:
                            callback?.Invoke(false, Constants.Errors.PurchaseInvalidPriceType);
                            break;
                    }
                } else
                    callback?.Invoke(true, null);
            }
        }

        public static void RestorePurchases()
        {
            Balancy.Actions.Purchasing.GetRestorePurchasesCallback()?.Invoke();
        }

        /// <summary>
        /// Per-node visual scripting analytics (vs_node_start / vs_node_finish and
        /// vs_script_start / vs_script_finish).
        ///
        /// Disabled by default: these fire on every executed node — two events per
        /// node — which a script-heavy game produces faster than they can be sent.
        /// Enable it only while debugging script flow.
        /// </summary>
        public static void SetVisualScriptingAnalyticsEnabled(bool enabled)
        {
            LibraryMethods.General.balancySetVisualScriptingAnalyticsEnabled(enabled);
        }

        /// <summary>
        /// Whether per-node visual scripting analytics is currently being collected.
        /// </summary>
        public static bool IsVisualScriptingAnalyticsEnabled()
        {
            return LibraryMethods.General.balancyIsVisualScriptingAnalyticsEnabled();
        }
    }
}
