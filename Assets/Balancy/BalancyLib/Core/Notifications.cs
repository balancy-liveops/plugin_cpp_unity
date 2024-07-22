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
    }
}
