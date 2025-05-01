using Balancy.Data.SmartObjects;
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
            // OnNetworkDownloadStarted += info => Debug.Log($" => Balancy.OnNetworkDownloadStarted: {info.Url}, Type: {(info.IsCDNRequest ? "CDN" : "API")}");
            // OnNetworkDownloadFinished += info => Debug.Log($" => Balancy.OnNetworkDownloadFinished: {info.Url}, Time: {info.TimeMs}ms, Size: {info.DownloadedBytes}B, Speed: {info.SpeedKBps:F1}KB/s, Success: {info.Success}");
        }
        
        public static void ClearAll()
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
        }
    }
}