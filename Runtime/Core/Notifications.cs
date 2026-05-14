using System;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

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

            OnLocalizationChanged = 132,

            Unknown
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class NotificationBase
        {
            public NotificationType Type;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class StatusNotificationBase : NotificationBase
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class InitNotificationDataIsReady : StatusNotificationBase
        {
            private int flags;

            public bool IsCloudSynced => (flags & (1 << 0)) != 0;
            public bool IsCMSUpdated => (flags & (1 << 1)) != 0;
            public bool IsProfileUpdated => (flags & (1 << 2)) != 0;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class InitNotificationError : StatusNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Message;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class InitNotificationAuthFailed : InitNotificationError
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class InitNotificationCloudProfileFailed : InitNotificationError
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class NotificationUserRefreshed : StatusNotificationBase
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class NotificationDisconnectAnotherSessionConflict : StatusNotificationBase
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotificationBase : NotificationBase
        {
            private IntPtr UserData;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewEventActivated : LiveOpsNotificationBase
        {
            public IntPtr EventInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnEventDeactivated : LiveOpsNotificationBase
        {
            public IntPtr EventInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOffer : LiveOpsNotificationBase
        {
            public int wasPurchased;
            public IntPtr OfferInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferActivated : LiveOpsNotification_OnNewOffer
        {

        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnOfferDeactivated : LiveOpsNotification_OnNewOffer
        {
            public bool WasPurchased => wasPurchased == 1;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferGroup : LiveOpsNotificationBase
        {
            protected int wasPurchased;
            public IntPtr OfferInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferGroupActivated : LiveOpsNotification_OnNewOfferGroup
        {

        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnOfferGroupDeactivated : LiveOpsNotification_OnNewOfferGroup
        {
            public bool WasPurchased => wasPurchased == 1;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_ABTestStarted : LiveOpsNotificationBase
        {
            public IntPtr ABTestInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_ABTestEnded : LiveOpsNotificationBase
        {
            public IntPtr ABTestInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_SegmentUpdated : LiveOpsNotificationBase
        {
            public IntPtr SegmentInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_ShopUpdated : LiveOpsNotificationBase
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_DailyBonusUpdated : LiveOpsNotificationBase
        {
            public IntPtr DailyBonusInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class NetworkNotificationBase : NotificationBase
        {
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class NetworkNotification_DownloadStarted : NetworkNotificationBase
        {
            public IntPtr UrlPtr;
            public IntPtr RelativePathPtr;
            public IntPtr DomainPtr;
            private int isCDNRequest;

            public bool IsCDNRequest => isCDNRequest == 1;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class NetworkNotification_DownloadFinished : NetworkNotificationBase
        {
            public IntPtr UrlPtr;
            public IntPtr RelativePathPtr;
            public IntPtr DomainPtr;
            public IntPtr ErrorMessagePtr;
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

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class PurchaseNotification_ShopSlotWasPurchased : LiveOpsNotificationBase
        {
            public IntPtr ShopSlot;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class PurchaseNotification_OfferWasPurchased : LiveOpsNotificationBase
        {
            public int wasPurchased;
            public IntPtr OfferInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class PurchaseNotification_OfferGroupWasPurchased : LiveOpsNotificationBase
        {
            public int StoreItemIndexInGroupOffer;
            public IntPtr OfferGroupInfo;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_InventoryUpdated : StatusNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Item;
            public int Count;
            public int SlotIndex;
            public int CurrentAmount;
            public IntPtr Inventory;
        }

        [Preserve, StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnLocalizationChanged : StatusNotificationBase
        {
            [MarshalAs(UnmanagedType.LPStr)] public string Code;
        }
    }
}
