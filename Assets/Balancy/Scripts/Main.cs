using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy
{
    public static class Main
    {
        public static void Init(AppConfig appConfig)
        {
            if (!CheckConfig(appConfig))
                return;
            
            LibraryMethods.General.balancySetLogCallback(LogMessage);
            LibraryMethods.General.balancySetDataUpdatedCallback(DataUpdated);
            UnityFileManager.Init();
            
            IntPtr configPtr = Marshal.AllocHGlobal(Marshal.SizeOf(appConfig));
            Marshal.StructureToPtr(appConfig, configPtr, false);
            LibraryMethods.General.balancyInit(configPtr);
        }

        private static void DataUpdated(bool dictsChanged, bool profileChanged)
        {
            Debug.LogError($"DataUpdated = {dictsChanged} ; {profileChanged}");
            if (dictsChanged)
                DataManager.RefreshAll();
        }

        private static bool CheckConfig(AppConfig appConfig)
        {
            if (string.IsNullOrEmpty(appConfig.ApiGameId))
            {
                Debug.LogError("Please provide Api Game Id in Config;");
                return false;
            }
            
            if (string.IsNullOrEmpty(appConfig.PublicKey))
            {
                Debug.LogError("Please provide Public Key in Config;");
                return false;
            }
            
            if (string.IsNullOrEmpty(appConfig.DeviceId))
                appConfig.DeviceId = Balancy.UnityUtils.GetUniqId();

            if (string.IsNullOrEmpty(appConfig.AppVersion))
                appConfig.AppVersion = Application.version;

            if (string.IsNullOrEmpty(appConfig.EngineVersion))
                appConfig.EngineVersion = Balancy.UnityUtils.GetEngineVersion();

            if (string.IsNullOrEmpty(appConfig.CustomId))
                appConfig.CustomId = string.Empty;

            return true;
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.LogCallback))]
        private static void LogMessage(int level, string message)
        {
            //TODO show different Levels
            Debug.Log(message);
        }
    }
}