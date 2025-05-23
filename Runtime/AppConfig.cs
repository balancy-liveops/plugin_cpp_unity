using System;
using System.Runtime.InteropServices;
using Balancy.Core;

namespace Balancy
{
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void StatusUpdateCallback(IntPtr notification);
    
    // public delegate void StatusUpdateNotificationCallback(Notifications.NotificationBase notification);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ProgressUpdateCallback(string fileName, float progress);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DownloadCompleteCallback(bool success, string message);
    
    
    [Flags]
    public enum LaunchType
    {
        None = 0,
        Local = 1 << 0,
        Cloud = 1 << 1,
        AutoRetry = 1 << 2,
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
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
        public ProgressUpdateCallback OnProgressUpdateCallback = null;

        public LaunchType LaunchType = LaunchType.Local | LaunchType.Cloud | LaunchType.AutoRetry;
        [MarshalAs(UnmanagedType.LPStr)]
        public string BranchName;//If left blank, we'll use the branchConditions to find the best branch.
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string CdnCustomUrl;
        public int CdnTimeout = 10;
        public int CdnRetries = 3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class CppAppConfig : CppBaseAppConfig
    {
        public int Platform;
        public byte AutoLogin = 1;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string DeviceId = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string CustomId = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string AppVersion = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string BundleId = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string EngineVersion = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string DeviceModel = null;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string DeviceName = null;

        public int DeviceType;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string OperatingSystem = null;
        
        public int OperatingSystemFamily;
        public int SystemMemorySize;
        
        [MarshalAs(UnmanagedType.LPStr)]
        public string SystemLanguage = null;
    }
    
    public class AppConfig
    {
        public string ApiGameId;
        public string PublicKey;
        
        public Constants.Environment Environment = Constants.Environment.Development;
        public UpdateType UpdateType = UpdateType.FullUpdate;
        public int UpdatePeriod = 600;

        // public StatusUpdateNotificationCallback OnStatusUpdate = null;
        public ProgressUpdateCallback OnProgressUpdateCallback = null;

        public LaunchType LaunchType = LaunchType.Local | LaunchType.Cloud | LaunchType.AutoRetry;
        public string BranchName;//If left blank, we'll use the branchConditions to find the best branch.
        
        public Constants.Platform Platform;
        public bool AutoLogin = true;
        
        public string DeviceId = null;
        public string CustomId = null;
        public string AppVersion = null;
        public string BundleId = null;
        public string EngineVersion = null;
    }
    
    public enum UpdateType
    {
        None,
        BuiltInFeaturesOnly,
        FullUpdate
    }
}