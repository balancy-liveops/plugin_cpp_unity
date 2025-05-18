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
            for (int i =_callbacks.Count - 1; i>=0;i--)
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

        public static void FinalizedHardPurchase(Actions.PurchaseResult result, Balancy.Actions.BalancyProductInfo productInfo, Actions.PurchaseInfo purchaseInfo,
            Action<bool, bool> validationCallback)
        {
            Debug.LogError("HardPurchase result: " + result);
            Debug.LogError("HardPurchase Receipt: " + purchaseInfo.Receipt);
            Debug.LogError("HardPurchase Error: " + purchaseInfo.ErrorMessage);
#if UNITY_EDITOR
            var receipt = "{\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"" +
                          purchaseInfo.TransactionId + "\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"" +
                          purchaseInfo.ProductId + "\\\\\\\"}\\\",\\\"signature\\\":\\\"bypass\\\"}\"}";
#else
            var receipt = purchaseInfo.Receipt;
#endif
            var paymentInfo = new Core.PaymentInfo
            {
                OrderId = purchaseInfo.TransactionId,
                Receipt = receipt,
                ProductId = purchaseInfo.ProductId,
                Currency = purchaseInfo.CurrencyCode,
                Price = (float)purchaseInfo.Price
            };

            if (productInfo != null)
            {
                var callback = GetCallbackData(productInfo);

                if (result == Actions.PurchaseResult.Success)
                {
                    void InvokeCallbacks(Balancy.Core.Responses.PurchaseProductResponseData responseData)
                    {
                        Debug.LogError("Response: " + responseData.Success);
                        Debug.LogError("ErrorCode: " + responseData.ErrorCode);
                        Debug.LogError("ErrorMessage: " + responseData.ErrorMessage);
                        Debug.LogError("product: " + responseData.ProductId);

                        validationCallback?.Invoke(responseData.Success, responseData.RemoveFromPending);
                        callback?.Callback?.Invoke(responseData.Success, responseData.ErrorMessage);
                        
                        if (responseData.Success)
                            productInfo.ReportThePurchase(paymentInfo);
                    }
                    
                    switch (productInfo.Type)
                    {
                        case Actions.BalancyProductInfo.PurchaseType.StoreItem:
                        {
                            var storeItem = productInfo.GetStoreItem();
                            HardPurchaseStoreItem(storeItem, paymentInfo, InvokeCallbacks, true);
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
                            } else {
                                HardPurchaseGameOffer(offerInfo, paymentInfo, InvokeCallbacks, true);
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
                            } else {
                                var storeItem = productInfo.GetStoreItem();
                                HardPurchaseGameOfferGroup(offerGroupInfo, storeItem, paymentInfo,
                                    InvokeCallbacks, true);
                            }
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    callback?.Callback?.Invoke(false, purchaseInfo.ErrorMessage);
                }
            }
            else
            {
                Debug.LogError("productInfo is null -> can't validate it");
            }
        }

        public static void InitPurchase(OfferInfo offerInfo, Action<bool, string> callback)
        {
            if (offerInfo?.GameOffer == null)
            {
                callback?.Invoke(false, Constants.Errors.GameOfferNull);
                return;
            }
        }

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
                if (!SoftPurchaseStoreItem(storeItem))
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
                }
            }
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.API.ResponseCallback))]
        private static Balancy.LibraryMethods.API.ResponseCallback ProtectedFromGCCallback<T>(Balancy.Core.ResponseCallback<T> callback) where T : Balancy.Core.Responses.ResponseData
        {
            System.Runtime.InteropServices.GCHandle? gch = null;
            Balancy.LibraryMethods.API.ResponseCallback innerCallback = (responseDataPtr) => {
                var responseData = Marshal.PtrToStructure<T>(responseDataPtr);
                if (gch.HasValue)
                    gch.Value.Free();
                try
                {
                    callback(responseData);
                } catch (Exception e)
                {
                    UnityEngine.Debug.LogError("Exception in callback: " + e);
                }
            };
            
            gch = GCHandle.Alloc(innerCallback);
            return innerCallback;
        }
        
        public static string[] GetProductsIdAndType()
        {
            IntPtr ptr = LibraryMethods.General.balancyGetProductsIdAndType(out var size);
            return JsonBasedObject.ReadStringArrayValues(ptr, size);
        }
    }
}