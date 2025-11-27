using System;
using System.Runtime.InteropServices;

namespace Balancy.Core
{
    internal class Notifications
    {
        public enum NotificationType
        {
            Base = 0,
            DataIsReady = 1,
            AuthFailed = 2,
            CloudProfileFailed = 3,
            UserRefreshed = 4,
            CMSInited = 5, 
            DisconnectAnotherSessionConflict = 6,
            
            OnNewEventActivated = 100,
            OnEventDeactivated = 101,
            OnNewOfferActivated = 102,
            OnOfferDeactivated = 103,
            OnNewOfferGroupActivated = 104,
            OnOfferGroupDeactivated = 105,
            
            OnABTestStarted = 106,
            OnABTestEnded = 107,

            OnSegmentUpdated = 108,
            OnShopUpdated = 109,
            OnDailyBonusUpdated = 110,
            
                // Network events
            OnNetworkDownloadStarted = 111,
            OnNetworkDownloadFinished = 112,
            
            OnShopSlotWasPurchased = 120,
            OnOfferWasPurchased = 121,
            OnOfferGroupWasPurchased = 122,
            
            OnInventoryUpdated = 131,
            
            Unknown
        }

        [StructLayout(LayoutKind.Sequential)]
        public class NotificationBase
        {
            public NotificationType Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class StatusNotificationBase : NotificationBase
        {
        }

        [StructLayout(LayoutKind.Sequential)]
        public class InitNotificationDataIsReady : StatusNotificationBase
        {
            private int flags;

            public bool IsCloudSynced => (flags & (1 << 0)) != 0;
            public bool IsCMSUpdated => (flags & (1 << 1)) != 0;
            public bool IsProfileUpdated => (flags & (1 << 2)) != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class InitNotificationError : StatusNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Message;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class InitNotificationAuthFailed : InitNotificationError
        {
        }

        [StructLayout(LayoutKind.Sequential)]
        public class InitNotificationCloudProfileFailed : InitNotificationError
        {
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class NotificationUserRefreshed : StatusNotificationBase
        {
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class NotificationDisconnectAnotherSessionConflict : StatusNotificationBase
        {
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotificationBase : NotificationBase
        {
            private IntPtr UserData;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewEventActivated : LiveOpsNotificationBase
        {
            public IntPtr EventInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnEventDeactivated : LiveOpsNotificationBase
        {
            public IntPtr EventInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOffer : LiveOpsNotificationBase
        {
            public int wasPurchased;
            public IntPtr OfferInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferActivated : LiveOpsNotification_OnNewOffer
        {
            
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnOfferDeactivated : LiveOpsNotification_OnNewOffer
        {
            public bool WasPurchased => wasPurchased == 1;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferGroup : LiveOpsNotificationBase
        {
            protected int wasPurchased;
            public IntPtr OfferInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferGroupActivated : LiveOpsNotification_OnNewOfferGroup
        {
            
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnOfferGroupDeactivated : LiveOpsNotification_OnNewOfferGroup
        {
            public bool WasPurchased => wasPurchased == 1;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_ABTestStarted : LiveOpsNotificationBase
        {
            public IntPtr ABTestInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_ABTestEnded : LiveOpsNotificationBase
        {
            public IntPtr ABTestInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_SegmentUpdated : LiveOpsNotificationBase
        {
            public IntPtr SegmentInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_ShopUpdated : LiveOpsNotificationBase
        {
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_DailyBonusUpdated : LiveOpsNotificationBase
        {
            public IntPtr DailyBonusInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class NetworkNotificationBase : NotificationBase
        {
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class NetworkNotification_DownloadStarted : NetworkNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Url;
            [MarshalAs(UnmanagedType.LPStr)] public string RelativePath;
            [MarshalAs(UnmanagedType.LPStr)] public string Domain;
            private int isCDNRequest;
            
            public bool IsCDNRequest => isCDNRequest == 1;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class NetworkNotification_DownloadFinished : NetworkNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Url;
            [MarshalAs(UnmanagedType.LPStr)] public string RelativePath;
            [MarshalAs(UnmanagedType.LPStr)] public string Domain;
            [MarshalAs(UnmanagedType.LPStr)] public string ErrorMessage;
            private int isCDNRequest;
            public float TimeMs;
            public float SpeedKBps;
            public long DownloadedBytes;
            private int success;
            public int ErrorCode;
            public int Attempts;
            
            public bool IsCDNRequest => isCDNRequest == 1;
            public bool Success => success == 1;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class PurchaseNotification_ShopSlotWasPurchased : LiveOpsNotificationBase
        {
            public IntPtr ShopSlot;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class PurchaseNotification_OfferWasPurchased : LiveOpsNotificationBase
        {
            public int wasPurchased;
            public IntPtr OfferInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class PurchaseNotification_OfferGroupWasPurchased : LiveOpsNotificationBase
        {
            public int StoreItemIndexInGroupOffer;
            public IntPtr OfferGroupInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_InventoryUpdated : StatusNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Item;
            public int Count;
            public int SlotIndex;
            public int CurrentAmount;
            public IntPtr Inventory;
        }
    }
}
