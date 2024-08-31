using System;
using System.Runtime.InteropServices;

namespace Balancy.Core
{
    public class Notifications
    {
        public enum NotificationType
        {
            Base = 0,
            DataIsReady = 1,
            AuthFailed = 2,
            CloudProfileFailed = 3,
            
            OnNewEventActivated = 100,
            OnEventDeactivated = 101,
            OnNewOfferActivated = 102,
            OnOfferDeactivated = 103,
            OnNewOfferGroupActivated = 104,
            OnOfferGroupDeactivated = 105,
            
            OnABTestStarted = 106,
            OnABTestEnded = 107,

            OnSegmentUpdated = 108,
            
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

            public bool IsCloudSynched => (flags & (1 << 0)) != 0;
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
        public class LiveOpsNotification_OnNewOfferActivated : LiveOpsNotificationBase
        {
            public IntPtr OfferInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnOfferDeactivated : LiveOpsNotificationBase
        {
            public IntPtr OfferInfo;
            private int wasPurchased;

            public bool WasPurchased => wasPurchased == 1;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnNewOfferGroupActivated : LiveOpsNotificationBase
        {
            public IntPtr OfferInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public class LiveOpsNotification_OnOfferGroupDeactivated : LiveOpsNotificationBase
        {
            public IntPtr OfferInfo;
            private int wasPurchased;

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
    }
}
