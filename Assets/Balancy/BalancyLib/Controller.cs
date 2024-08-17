using System;
using System.Runtime.InteropServices;
using Balancy.Core;
using UnityEngine;

namespace Balancy
{
    public static class Controller
    {
        private static AppConfig _originalConfig;
        private static CppAppConfig _cppConfig;
        private static bool _isReadyToUse;

        public static bool IsReadyToUse => _isReadyToUse;
        
        public static void Init(AppConfig appConfig)
        {
            if (!CheckConfig(appConfig))
                return;
            
            LibraryMethods.General.balancySetLogCallback(LogMessage);
            UnityFileManager.Init();
            LibraryMethods.Models.balancySetModelOnRefresh(ModelRefreshed);

            CppAppConfig config = CreateConfigForCPP(appConfig);
            IntPtr configPtr = Marshal.AllocHGlobal(Marshal.SizeOf(config));
            Marshal.StructureToPtr(config, configPtr, false);
            // PrintSizeAndOffsets<CppAppConfig>();
            LibraryMethods.General.balancyInit(configPtr);
        }

        public static void Stop()
        {
            LibraryMethods.General.balancyStop();
        }

        private static void ModelRefreshed(string unnyId, IntPtr newPointer)
        {
            CMS.ModelRefreshed(unnyId, newPointer);
        }

        private static void DataUpdated(bool dictsChanged, bool profileChanged)
        {
            if (dictsChanged)
                CMS.RefreshAll();
            if (profileChanged)
                Profiles.RefreshAll();
        }

        private static CppAppConfig CreateConfigForCPP(AppConfig originalConfig)
        {
            _originalConfig = originalConfig;

            _cppConfig = new CppAppConfig
            {
                ApiGameId = _originalConfig.ApiGameId,
                PublicKey = _originalConfig.PublicKey,
                Environment = _originalConfig.Environment,
                UpdateType = _originalConfig.UpdateType,
                UpdatePeriod = _originalConfig.UpdatePeriod,
                LaunchType = _originalConfig.LaunchType,
                Platform = (int)FindPlatform(_originalConfig.Platform),
                AutoLogin = _originalConfig.AutoLogin,
                OnStatusUpdate = OnStatusUpdate,
                OnProgressUpdateCallback = _originalConfig.OnProgressUpdateCallback,
                DeviceId = string.IsNullOrEmpty(_originalConfig.DeviceId) ? Balancy.UnityUtils.GetUniqId() : _originalConfig.DeviceId,
                AppVersion = string.IsNullOrEmpty(_originalConfig.AppVersion) ? Application.version : _originalConfig.AppVersion,
                EngineVersion = string.IsNullOrEmpty(_originalConfig.EngineVersion) ? Balancy.UnityUtils.GetEngineVersion() : _originalConfig.EngineVersion,
                CustomId = string.IsNullOrEmpty(_originalConfig.CustomId) ? string.Empty : _originalConfig.CustomId,
                DeviceModel = SystemInfo.deviceModel,
                DeviceName = SystemInfo.deviceName,
                DeviceType = (int)SystemInfo.deviceType,
                OperatingSystem = SystemInfo.operatingSystem,
                OperatingSystemFamily = (int)SystemInfo.operatingSystemFamily,
                SystemMemorySize = SystemInfo.systemMemorySize,
                SystemLanguage = UnityEngine.Application.systemLanguage.ToString(),
            };

            return _cppConfig;
        }

        private static Constants.Platform FindPlatform(Constants.Platform originalPlatform)
        {
            if (originalPlatform == 0)
            {
                var platform = UnityEngine.Application.platform;

                switch (platform)
                {
                    case UnityEngine.RuntimePlatform.IPhonePlayer:
                        return Constants.Platform.IosAppStore;
                    case UnityEngine.RuntimePlatform.Android:
                    case UnityEngine.RuntimePlatform.OSXEditor:
                    case UnityEngine.RuntimePlatform.LinuxEditor:
                    case UnityEngine.RuntimePlatform.WindowsEditor:
                        return Constants.Platform.AndroidGooglePlay;
                    default:
                        return Constants.Platform.Unknown;
                }
            }

            return originalPlatform;
        }

        private static void OnStatusUpdate(IntPtr notificationPtr)
        {
            var baseNotification = Marshal.PtrToStructure<Notifications.StatusNotificationBase>(notificationPtr);
            Notifications.StatusNotificationBase notification = baseNotification;
            switch (baseNotification.Type)
            {
                case Notifications.NotificationType.DataIsReady:
                    var notificationDataIsReady = Marshal.PtrToStructure<Notifications.InitNotificationDataIsReady>(notificationPtr);
                    notification = notificationDataIsReady;
                    DataUpdated(notificationDataIsReady.IsCMSUpdated, notificationDataIsReady.IsProfileUpdated);
                    _isReadyToUse = true;
                    break;
                case Notifications.NotificationType.AuthFailed:
                    notification = Marshal.PtrToStructure<Notifications.InitNotificationAuthFailed>(notificationPtr);
                    break;
                case Notifications.NotificationType.CloudProfileFailed:
                    notification =
                        Marshal.PtrToStructure<Notifications.InitNotificationCloudProfileFailed>(notificationPtr);
                    break;
                default:
                    Debug.LogError("**==> Unknown notification type. " + baseNotification.Type);
                    break;
            }
           
            try
            {
                _originalConfig.OnStatusUpdate?.Invoke(notification);
            }
            catch (Exception e)
            {
                Debug.LogError($"{e}");
            }
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
            return true;
        }
        
        private enum Level {
            Off,
            Verbose,
            Debug,
            Info,
            Warn,
            Error
        };
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.LogCallback))]
        private static void LogMessage(int level, string message)
        {
            switch ((Level)level)
            {
                case Level.Error:
                    Debug.LogError(message);
                    break;
                case Level.Warn:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
        
        public static void PrintSizeAndOffsets<T>()
        {
            Debug.LogWarning($"Size of {typeof(T).Name}: {Marshal.SizeOf<T>()}");

            var fields = typeof(T).GetFields();
            foreach (var field in fields)
            {
                var offset = Marshal.OffsetOf<T>(field.Name);
                Debug.Log($"Field: {field.Name}, Offset: {offset}, Type: {field.FieldType}");
            }
        }
    }
}