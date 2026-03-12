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

        private static void HardPurchase(Actions.BalancyProductInfo productInfo, Action<bool, string> callback)
        {
            _callbacks.Add(new CallbacksData(productInfo, callback));
            Balancy.Actions.Purchasing.GetHardPurchaseCallback()(productInfo);
        }

        private static CallbacksData GetCallbackData(Actions.BalancyProductInfo productInfo)
        {
            for (int i = _callbacks.Count - 1; i >= 0; i--)
            {
                if (_callbacks[i].ProductInfo.Equals(productInfo))
                {
                    var data = _callbacks[i];
                    _callbacks.RemoveAt(i);
                    return data;
                }
            }

            return null;
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
                    Data = JsonUtility.FromJson<Balancy.Core.Responses.CompletePurchaseData>(data.data)
                };
                callback?.Invoke(resData);
            };
                
            var callbackResult = ProtectedFromGCCallback(resCallback);

            LibraryMethods.API.balancyNutakuComplete(userId, orderId, callbackResult.CallbackId, callbackResult.StaticCallback);
        }

        public static void FinalizedHardPurchase(Actions.PurchaseResult result,
            Balancy.Actions.BalancyProductInfo productInfo, Core.PaymentInfo paymentInfo,
            Action<bool, bool> validationCallback, bool requireReceiptValidation = true)
        {
            Debug.Log("HardPurchase result: " + result);
            Debug.Log("HardPurchase Receipt: " + paymentInfo.Receipt);

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
                    void InvokeCallbacks(Balancy.Core.Responses.PurchaseProductResponseData responseData)
                    {
                        Debug.Log(
                            $"Response: {responseData.Success} ErrorCode = {responseData.ErrorCode} Message = {responseData.ErrorMessage} Product = {responseData.ProductId}");

                        validationCallback?.Invoke(responseData.Success, responseData.RemoveFromPending);
                        callback?.Callback?.Invoke(responseData.Success, responseData.ErrorMessage);

                        if (responseData.Success)
                        {
                            paymentInfo.PriceUSD = responseData.PriceUSD;
                            productInfo.ReportThePurchase(paymentInfo);
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
                            if (offerInfo == null)
                            {
                                validationCallback?.Invoke(false, false);
                                callback?.Callback?.Invoke(false, Constants.Errors.OfferInfoNull);
                            }
                            else
                            {
                                HardPurchaseGameOffer(offerInfo, paymentInfo, InvokeCallbacks, requireValidation);
                            }

                            break;
                        }
                        case Actions.BalancyProductInfo.PurchaseType.OfferGroup:
                        {
                            var offerGroupInfo =
                                Balancy.Profiles.System.SmartInfo.FindOfferGroupInfo(productInfo.OfferInstanceId);
                            if (offerGroupInfo == null)
                            {
                                validationCallback?.Invoke(false, false);
                                callback?.Callback?.Invoke(false, Constants.Errors.OfferGroupInfoNull);
                            }
                            else
                            {
                                var storeItem = productInfo.GetStoreItem();
                                HardPurchaseGameOfferGroup(offerGroupInfo, storeItem, paymentInfo,
                                    InvokeCallbacks, requireValidation);
                            }

                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    callback?.Callback?.Invoke(false, "");
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

        public static string[] GetProductsIdAndType()
        {
            IntPtr ptr = LibraryMethods.General.balancyGetProductsIdAndType(out var size);
            return JsonBasedObject.ReadStringArrayValues(ptr, size);
        }
    }
}