using System.Runtime.InteropServices;

namespace Balancy.Core
{
    public class Notifications
    {
        public enum NotificationType
        {
            Base = 0,
            LocalReady = 1,
            CloudSynched = 2,
            AuthFailed = 3,
            CloudProfileFailed = 4,
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
        public class InitNotificationLocalReady : StatusNotificationBase
        {
        }

        [StructLayout(LayoutKind.Sequential)]
        public class InitNotificationCloudSynched : StatusNotificationBase
        {
            public byte WereDictUpdated;
            public byte WereProfilesUpdated;
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
