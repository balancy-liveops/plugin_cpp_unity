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
        }
    }
}