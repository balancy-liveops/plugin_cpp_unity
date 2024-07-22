using System;
using System.Runtime.InteropServices;
using Balancy.Core;

namespace Balancy
{
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void StatusUpdateCallback(IntPtr notification);
    
    public delegate void StatusUpdateNotificationCallback(Notifications.StatusNotificationBase notification);
    
    [Flags]
    public enum LaunchType
    {
        None = 0,
        Local = 1 << 0,
        Cloud = 1 << 1,
        AutoRetry = 1 << 2,
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class CppBaseAppConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string ApiGameId;
        [MarshalAs(UnmanagedType.LPStr)]
        public string PublicKey;
        
        public Constants.Environment Environment = Constants.Environment.Development;
        public UpdateType UpdateType = UpdateType.FullUpdate;
        public int UpdatePeriod = 600;

        public StatusUpdateCallback OnStatusUpdate = null;

        public LaunchType LaunchType = LaunchType.Local | LaunchType.Cloud | LaunchType.AutoRetry;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal class CppAppConfig : CppBaseAppConfig
    {
        public Constants.Platform Platform;
        public byte AutoLogin = 1;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string DeviceId = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string CustomId = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string AppVersion = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string EngineVersion = null;
    }
    
    public class AppConfig
    {
        public string ApiGameId;
        public string PublicKey;
        
        public Constants.Environment Environment = Constants.Environment.Development;
        public UpdateType UpdateType = UpdateType.FullUpdate;
        public int UpdatePeriod = 600;

        public StatusUpdateNotificationCallback OnStatusUpdate = null;

        public LaunchType LaunchType = LaunchType.Local | LaunchType.Cloud | LaunchType.AutoRetry;
        
        public Constants.Platform Platform;
        public byte AutoLogin = 1;
        
        public string DeviceId = null;
        public string CustomId = null;
        public string AppVersion = null;
        public string EngineVersion = null;
    }
    
    public enum UpdateType
    {
        None,
        BuiltInFeaturesOnly,
        FullUpdate
    }
    
    public class Constants
    {
        public enum Environment
        {
            Development,
            Stage,
            Production
        }

        public enum Platform
        {
            Unknown = 1,
            //            Vkontakte = 3,
            Facebook = 4,
            //            Odnoklassniki = 5,
            FbInstant = 6,

            AndroidGooglePlay = 7,
            IosAppStore = 8,
            AmazonStore = 14,
            Yoomoney = 15
        }
    }
}