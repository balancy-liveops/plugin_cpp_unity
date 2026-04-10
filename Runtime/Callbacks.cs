using System;
using System.Collections.Generic;
using Balancy.Core;
using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects;
using Balancy.Runtime.Core;
using UnityEngine;

namespace Balancy
{
    public class Callbacks
    {
        public struct DataUpdatedStatus
        {
            public readonly bool IsCloudSynced;
            public readonly bool IsCMSUpdated;
            public readonly bool IsProfileUpdated;
            
            public DataUpdatedStatus(bool isCloudSynced, bool isCmsUpdated, bool isProfileUpdated)
            {
                IsCloudSynced = isCloudSynced;
                IsCMSUpdated = isCmsUpdated;
                IsProfileUpdated = isProfileUpdated;
            }
        }
        
        public struct ErrorStatus
        {
            public readonly string Message;
            
            public ErrorStatus(string message)
            {
                Message = message;
            }
        }
        
        public enum DisconnectReason
        {
            Unknown = 0,
            AnotherSessionConflict = 1,
        }

        public delegate void OnDataUpdatedDelegate(DataUpdatedStatus status);
        public delegate void OnErrorDelegate(ErrorStatus status);
        public delegate void OnEventInfoDelegate(EventInfo eventInfo);
        public delegate void OnOfferInfoDelegate(OfferInfo offerInfo);
        public delegate void OnOfferInfoDelegatePurchased(OfferInfo offerInfo, bool wasPurchased);
        public delegate void OnOfferGroupInfoDelegate(OfferGroupInfo offerGroupInfo);
        public delegate void OnAbTestInfoDelegate(AbTestInfo abTestInfo);
        public delegate void OnSegmentInfoDelegate(SegmentInfo segmentInfo);
        public delegate void OnDailyBonusInfoDelegate(DailyBonusInfo dailyBonusInfo);
        public delegate void OnShopUpdatedDelegate();
        public delegate void OnNetworkDownloadStartedDelegate(NetworkDownloadInfo info);
        public delegate void OnNetworkDownloadFinishedDelegate(NetworkDownloadCompletedInfo info);
        
        public delegate void OnPurchasesRestoredDelegate(List<PurchaseResult> items);
        public delegate void OnHardPurchasedStoreItemDelegate(PaymentInfo paymentInfo, StoreItem storeItem);
        public delegate void OnHardPurchasedShopSlotDelegate(PaymentInfo paymentInfo, Balancy.Models.LiveOps.Store.Slot shopSlot);
        public delegate void OnHardPurchasedOfferDelegate(PaymentInfo paymentInfo, GameOffer gameOffer);
        public delegate void OnHardPurchasedOfferGroupDelegate(PaymentInfo paymentInfo, GameOfferGroup gameOffer, StoreItem storeItem);
        
        public delegate void OnShopSlotWasPurchasedDelegate(Balancy.Data.SmartObjects.ShopSlot shopSlot);
        public delegate void OnOfferWasPurchasedDelegate(OfferInfo offerInfo);
        public delegate void OnOfferGroupWasPurchasedDelegate(OfferGroupInfo offerGroupInfo, StoreItem storeItem);
        public delegate void OnInventoryUpdatedDelegate(Balancy.Data.SmartObjects.Inventory inventory, Balancy.Models.SmartObjects.Item item, int count, int slotIndex, int currentAmount);
        
        public delegate void OnProfileResetDelegate();
        
        public delegate void OnPaymentIsReadyDelegate();

        public delegate void OnDisconnectedDelegate(DisconnectReason reason);
        
        public static OnDataUpdatedDelegate OnDataUpdated = null;
        public static OnErrorDelegate OnAuthFailed = null;
        public static OnErrorDelegate OnCloudProfileFailedToLoad = null;
        public static OnEventInfoDelegate OnNewEventActivated = null;
        public static OnEventInfoDelegate OnEventDeactivated = null;
        public static OnOfferInfoDelegate OnNewOfferActivated = null;
        public static OnOfferInfoDelegatePurchased OnOfferDeactivated = null;
        public static OnOfferGroupInfoDelegate OnNewOfferGroupActivated = null;
        public static OnOfferGroupInfoDelegate OnOfferGroupDeactivated = null;
        public static OnAbTestInfoDelegate OnNewAbTestStarted = null;
        public static OnAbTestInfoDelegate OnAbTestEnded = null;
        public static OnSegmentInfoDelegate OnSegmentInfoUpdated = null;
        public static OnDailyBonusInfoDelegate OnDailyBonusUpdated = null;
        public static OnShopUpdatedDelegate OnShopUpdated = null;
        public static OnNetworkDownloadStartedDelegate OnNetworkDownloadStarted = null;
        public static OnNetworkDownloadFinishedDelegate OnNetworkDownloadFinished = null;
        private static OnPaymentIsReadyDelegate _onPaymentIsReady;
        private static bool _paymentIsReady;

        public static OnPaymentIsReadyDelegate OnPaymentIsReady
        {
            get => _onPaymentIsReady;
            set
            {
                var previous = _onPaymentIsReady;
                _onPaymentIsReady = value;
                if (_paymentIsReady && value != null)
                {
                    var added = (OnPaymentIsReadyDelegate)Delegate.RemoveAll(value, previous);
                    added?.Invoke();
                }
            }
        }

        public static void SetPaymentIsReady()
        {
            _paymentIsReady = true;
            _onPaymentIsReady?.Invoke();
        }
        
        public static OnPurchasesRestoredDelegate OnPurchasesRestored = null;
        public static OnHardPurchasedStoreItemDelegate OnHardPurchasedStoreItem = null;
        public static OnHardPurchasedShopSlotDelegate OnHardPurchasedShopSlot = null;
        public static OnHardPurchasedOfferDelegate OnHardPurchasedOffer = null;
        public static OnHardPurchasedOfferGroupDelegate OnHardPurchasedOfferGroup = null;
        
        public static OnShopSlotWasPurchasedDelegate OnShopSlotWasPurchased = null;
        public static OnOfferWasPurchasedDelegate OnOfferWasPurchased = null;
        public static OnOfferGroupWasPurchasedDelegate OnOfferGroupWasPurchased = null;
        public static OnInventoryUpdatedDelegate OnInventoryUpdated = null;
        
        public static OnProfileResetDelegate OnProfileResetStart = null;
        public static OnProfileResetDelegate OnProfileResetFinish = null;
        
        public static OnProfileResetDelegate OnGameRefreshed = null;
        
        public static OnDisconnectedDelegate OnDisconnected = null;
        
        public struct NetworkDownloadInfo
        {
            public readonly string Url;
            public readonly string RelativePath;
            public readonly string Domain;
            public readonly bool IsCDNRequest;
            
            public NetworkDownloadInfo(string url, string relativePath, string domain, bool isCDNRequest)
            {
                Url = url;
                RelativePath = relativePath;
                Domain = domain;
                IsCDNRequest = isCDNRequest;
            }
        }
        
        public struct NetworkDownloadCompletedInfo
        {
            public readonly string Url;
            public readonly string RelativePath;
            public readonly string Domain;
            public readonly bool IsCDNRequest;
            public readonly float TimeMs;
            public readonly float SpeedKBps;
            public readonly long DownloadedBytes;
            public readonly bool Success;
            public readonly int ErrorCode;
            public readonly string ErrorMessage;
            public readonly int Attempts;
            
            public NetworkDownloadCompletedInfo(string url, string relativePath, string domain, bool isCDNRequest, 
                float timeMs, float speedKBps, long downloadedBytes, bool success, int errorCode, string errorMessage, int attempts)
            {
                Url = url;
                RelativePath = relativePath;
                Domain = domain;
                IsCDNRequest = isCDNRequest;
                TimeMs = timeMs;
                SpeedKBps = speedKBps;
                DownloadedBytes = downloadedBytes;
                Success = success;
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
                Attempts = attempts;
            }
        }

        public static void InitExamplesWithLogs()
        {
            OnDataUpdated += status => Debug.Log(" => Balancy.OnDataUpdated Cloud = " + status.IsCloudSynced + " ;CMS = " + status.IsCMSUpdated + " ;Profiles = " + status.IsProfileUpdated);
            OnAuthFailed += status => Debug.Log(" => Balancy.OnAuthFailed: " + status.Message);
            OnCloudProfileFailedToLoad += status => Debug.Log(" => Balancy.OnCloudProfileFailedToLoad: " + status.Message);
            
            OnNewEventActivated += eventInfo => Debug.Log(" => Balancy.OnNewEventActivated: " + eventInfo?.GameEvent?.Name);
            OnEventDeactivated += eventInfo => Debug.Log(" => Balancy.OnEventDeactivated: " + eventInfo?.GameEvent?.Name);
            OnNewOfferActivated += offerInfo => Debug.Log(" => Balancy.OnNewOfferActivated: " + offerInfo?.GameOffer?.Name);
            OnOfferDeactivated += (offerInfo, wasPurchased) => Debug.Log(" => Balancy.OnOfferDeactivated: " + offerInfo?.GameOffer?.Name + " ; wasPurchased = " + wasPurchased);
            OnNewOfferGroupActivated += offerGroupInfo => Debug.Log(" => Balancy.OnNewOfferGroupActivated: " + offerGroupInfo?.GameOfferGroup?.Name);
            OnOfferGroupDeactivated += offerGroupInfo => Debug.Log(" => Balancy.OnOfferGroupDeactivated: " + offerGroupInfo?.GameOfferGroup?.Name);
            OnNewAbTestStarted += abTestInfo => Debug.Log(" => Balancy.OnNewAbTestStarted: " + abTestInfo?.Test?.Name);
            OnAbTestEnded += abTestInfo => Debug.Log(" => Balancy.OnAbTestEnded: " + abTestInfo?.Test?.Name);
            OnSegmentInfoUpdated += segmentInfo => Debug.Log(" => Balancy.OnSegmentInfoUpdated: " + segmentInfo?.Segment?.Name + " isIn = " + segmentInfo?.IsIn);
            OnDailyBonusUpdated += dailyBonusInfo => Debug.Log(" => Balancy.OnDailyBonusUpdated: " + dailyBonusInfo?.DailyBonus?.Name);
            OnShopUpdated += () => Debug.Log(" => Balancy.OnShopUpdated");
            OnPaymentIsReady += () => Debug.Log(" => Balancy.OnPaymentIsReady");
            
            OnHardPurchasedStoreItem += (paymentInfo, storeItem) => Debug.Log(" => Balancy.OnHardPurchasedStoreItem: " + storeItem?.Name + " UnnyId = " + storeItem?.UnnyId + " price = " + paymentInfo.Price + " priceUSD = " + paymentInfo.PriceUSD);
            OnHardPurchasedShopSlot += (paymentInfo, shopSlot) => Debug.Log(" => Balancy.OnHardPurchasedShopSlot: " + shopSlot?.UnnyId + " price = " + paymentInfo.Price + " priceUSD = " + paymentInfo.PriceUSD);
            OnHardPurchasedOffer += (paymentInfo, gameOffer) => Debug.Log(" => Balancy.OnHardPurchasedOffer: " + gameOffer?.Name + " UnnyId = " + gameOffer?.UnnyId + " price = " + paymentInfo.Price + " priceUSD = " + paymentInfo.PriceUSD);
            OnHardPurchasedOfferGroup += (paymentInfo, gameOfferGroup, storeItem) => Debug.Log(" => Balancy.OnHardPurchasedOfferGroup: " + gameOfferGroup?.Name + " UnnyId = " + gameOfferGroup?.UnnyId + " price = " + paymentInfo.Price + " priceUSD = " + paymentInfo.PriceUSD);
            
            OnShopSlotWasPurchased += shopSlot => Debug.Log(" => Balancy.OnShopSlotWasPurchased: " + shopSlot?.Slot?.UnnyId);
            OnOfferWasPurchased += offerInfo => Debug.Log(" => Balancy.OnOfferWasPurchased: " + offerInfo?.GameOffer?.Name + " UnnyId = " + offerInfo?.GameOffer?.UnnyId);
            OnOfferGroupWasPurchased += (offerGroupInfo, storeItem) => Debug.Log(" => Balancy.OnOfferGroupWasPurchased: " + offerGroupInfo?.GameOfferGroup?.Name + " Store Item = " + storeItem?.Name);
            // OnInventoryUpdated += (inventory, item, count, slotIndex, currentAmount) => Debug.Log(" => Balancy.OnInventoryUpdated: Inventory = " + inventory + ", Item = " + item?.Name + ", Count = " + count + ", SlotIndex = " + slotIndex + ", CurrentAmount = " + currentAmount);
                
            // OnNetworkDownloadStarted += info => Debug.Log($" => Balancy.OnNetworkDownloadStarted: {info.Url}, Type: {(info.IsCDNRequest ? "CDN" : "API")}");
            // OnNetworkDownloadFinished += info => Debug.Log($" => Balancy.OnNetworkDownloadFinished: {info.Url}, Time: {info.TimeMs}ms, Size: {info.DownloadedBytes}B, Speed: {info.SpeedKBps:F1}KB/s, Success: {info.Success}");
            
            OnProfileResetStart += () => Debug.Log(" => Balancy.OnProfileResetStart");
            OnProfileResetFinish += () => Debug.Log(" => Balancy.OnProfileResetFinish");
            
            OnGameRefreshed += () => Debug.Log(" => Balancy.OnGameRefreshed");
            
            OnDisconnected += reason => Debug.Log(" => Balancy.OnDisconnected: " + reason);
        }
        
        internal static void ClearAll()
        {
            OnDataUpdated = null;
            OnAuthFailed = null;
            OnCloudProfileFailedToLoad = null;
            OnNewEventActivated = null;
            OnEventDeactivated = null;
            OnNewOfferActivated = null;
            OnOfferDeactivated = null;
            OnNewOfferGroupActivated = null;
            OnOfferGroupDeactivated = null;
            OnNewAbTestStarted = null;
            OnAbTestEnded = null;
            OnSegmentInfoUpdated = null;
            OnDailyBonusUpdated = null;
            OnShopUpdated = null;
            OnNetworkDownloadStarted = null;
            OnNetworkDownloadFinished = null;
            _onPaymentIsReady = null;
            _paymentIsReady = false;
            
            OnHardPurchasedStoreItem = null;
            OnHardPurchasedShopSlot = null;
            OnHardPurchasedOffer = null;
            OnHardPurchasedOfferGroup = null;
            
            OnShopSlotWasPurchased = null;
            OnOfferWasPurchased = null;
            OnOfferGroupWasPurchased = null;
            OnInventoryUpdated = null;
            
            OnNetworkDownloadStarted = null;
            OnNetworkDownloadFinished = null;

            OnProfileResetStart = null;
            OnProfileResetFinish = null;
            OnGameRefreshed = null;
            OnDisconnected = null;
        }
    }
}