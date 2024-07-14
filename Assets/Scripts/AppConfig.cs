using System;
using System.Runtime.InteropServices;

namespace Balancy
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FileSystemCallback(string path, string data);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void StatusUpdateCallback(IntPtr notification);
    
    
    [Flags]
    public enum LaunchType
    {
        None = 0,
        Local = 1 << 0,
        Cloud = 1 << 1,
        AutoRetry = 1 << 2,
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class BaseAppConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string ApiGameId;
        [MarshalAs(UnmanagedType.LPStr)]
        public string PublicKey;
        
        public Constants.Environment Environment = Constants.Environment.Development;
        public UpdateType UpdateType = UpdateType.FullUpdate;
        public int UpdatePeriod = 600;

        public FileSystemCallback OnSaveFileInCache = null;
        public FileSystemCallback OnSaveFileInResources = null;
        public StatusUpdateCallback OnStatusUpdate = null;

        public LaunchType LaunchType = LaunchType.Local | LaunchType.Cloud | LaunchType.AutoRetry;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class AppConfig : BaseAppConfig
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
            Windows,
            Mac,
            Linux,
            iOS,
            Android
        }
    }
}