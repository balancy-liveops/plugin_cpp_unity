using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy
{
    public static class Main
    {
        public static void Init(AppConfig appConfig)
        {
            LibraryMethods.General.setLogCallback(LogMessage);
            CheckConfig(appConfig);
            IntPtr configPtr = Marshal.AllocHGlobal(Marshal.SizeOf(appConfig));
            Marshal.StructureToPtr(appConfig, configPtr, false);
            LibraryMethods.General.balancyInit(configPtr);
        }

        private static void CheckConfig(AppConfig appConfig)
        {
            if (string.IsNullOrEmpty(appConfig.DeviceId))
                appConfig.DeviceId = Balancy.UnityUtils.GetUniqId();

            if (string.IsNullOrEmpty(appConfig.AppVersion))
                appConfig.AppVersion = Application.version;

            if (string.IsNullOrEmpty(appConfig.EngineVersion))
                appConfig.EngineVersion = Balancy.UnityUtils.GetEngineVersion();

            if (string.IsNullOrEmpty(appConfig.CustomId))
                appConfig.CustomId = string.Empty;
        }
        
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.LogCallback))]
        private static void LogMessage(int level, string message)
        {
            //TODO show different Levels
            Debug.Log(message);
        }
    }
}