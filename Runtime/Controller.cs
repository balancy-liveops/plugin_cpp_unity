using System;
using System.Runtime.InteropServices;
using Balancy.Core;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using UnityEngine;

namespace Balancy
{
    public static class Controller
    {
        private static AppConfig _originalConfig;
        private static CppAppConfig _cppConfig;
        private static bool _isReadyToUse;

        public static bool IsReadyToUse => _isReadyToUse;
        
        private static UnityMainThreadDispatcher _mainThreadInstance; 
        
        public static void Init(AppConfig appConfig)
        {
            if (!CheckConfig(appConfig))
                return;
            
            if (!CheckCallbacks())
                return;

            LibraryMethods.General.balancySetLogCallback(LogMessage);
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();
            Balancy.Network.UnityWebRequestBridge.Initialize();
            LibraryMethods.General.balancySetInvokeInMainThreadCallback(InvokeInMainThread);
            UnityFileManager.Init();
            LibraryMethods.Models.balancySetModelOnRefresh(ModelRefreshed);
            LibraryMethods.Models.balancySetUserDataInitializedCallback(UserDataInitialized);
            Profiles.Init();

            CppAppConfig config = CreateConfigForCPP(appConfig);
            IntPtr configPtr = Marshal.AllocHGlobal(Marshal.SizeOf(config));
            Marshal.StructureToPtr(config, configPtr, false);
            // PrintSizeAndOffsets<CppAppConfig>();
            LibraryMethods.General.balancyInit(configPtr);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.UserDataInitializedCallback))]
        private static void UserDataInitialized()
        {
            CMS.RefreshAll();
        }

        public static void Stop()
        {
            LibraryMethods.Models.balancySetModelOnRefresh(null);
            LibraryMethods.Models.balancySetUserDataInitializedCallback(null);
            LibraryMethods.General.balancyStop();
            Profiles.CleanUp();
            CMS.CleanUp();
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.ModelRefreshedCallback))]
        private static void ModelRefreshed(string unnyId, IntPtr newPointer)
        {
            CMS.ModelRefreshed(unnyId, newPointer);
        }

        private static void DataUpdated(bool dictsChanged, bool profileChanged)
        {
            if (dictsChanged)
                CMS.RefreshAll();
        }

        private static CppAppConfig CreateConfigForCPP(AppConfig originalConfig)
        {
            _originalConfig = originalConfig;

            var config = BalancyConfiguration.Instance;

            _cppConfig = new CppAppConfig
            {
                ApiGameId = _originalConfig.ApiGameId,
                PublicKey = _originalConfig.PublicKey,
                Environment = _originalConfig.Environment,
                UpdateType = _originalConfig.UpdateType,
                UpdatePeriod = _originalConfig.UpdatePeriod,
                LaunchType = _originalConfig.LaunchType,
                BranchName = _originalConfig.BranchName,
                Platform = (int)FindPlatform(_originalConfig.Platform),
                AutoLogin = (byte)(_originalConfig.AutoLogin ? 1 : 0),
                OnStatusUpdate = OnStatusUpdate,
                OnProgressUpdateCallback = OnProgressUpdate,
                DeviceId = string.IsNullOrEmpty(_originalConfig.DeviceId) ? Balancy.UnityUtils.GetUniqId() : _originalConfig.DeviceId,
                AppVersion = string.IsNullOrEmpty(_originalConfig.AppVersion) ? Application.version : _originalConfig.AppVersion,
                BundleId = string.IsNullOrEmpty(_originalConfig.BundleId) ? Application.identifier : _originalConfig.BundleId,
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

            if (config != null)
            {
                _cppConfig.CdnTimeout = config.Timeout;
                _cppConfig.CdnRetries = config.Retries;
                if (config.UseCustomCDN)
                    _cppConfig.CdnCustomUrl = config.Url;
            }

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
        
        [AOT.MonoPInvokeCallback(typeof(Balancy.ProgressUpdateCallback))]
        private static void OnProgressUpdate(string fileName, float progress)
        {
            _originalConfig?.OnProgressUpdateCallback(fileName, progress);
        }

        [AOT.MonoPInvokeCallback(typeof(Balancy.StatusUpdateCallback))]
        private static void OnStatusUpdate(IntPtr notificationPtr)
        {
            try
            {
                var baseNotification = Marshal.PtrToStructure<Notifications.NotificationBase>(notificationPtr);
                Notifications.NotificationBase notification = baseNotification;
                switch (baseNotification.Type)
                {
                    case Notifications.NotificationType.DataIsReady:
                        var notificationDataIsReady = Marshal.PtrToStructure<Notifications.InitNotificationDataIsReady>(notificationPtr);
                        DataUpdated(notificationDataIsReady.IsCMSUpdated, notificationDataIsReady.IsProfileUpdated);
                        _isReadyToUse = true;
                        Balancy.Callbacks.OnDataUpdated?.Invoke(new Balancy.Callbacks.DataUpdatedStatus(
                            notificationDataIsReady.IsCloudSynced, 
                            notificationDataIsReady.IsCMSUpdated,
                            notificationDataIsReady.IsProfileUpdated));
                        break;
                    case Notifications.NotificationType.AuthFailed:
                        var authNotification = Marshal.PtrToStructure<Notifications.InitNotificationAuthFailed>(notificationPtr);
                        Balancy.Callbacks.OnAuthFailed?.Invoke(new Balancy.Callbacks.ErrorStatus(authNotification.Message));
                        break;
                    case Notifications.NotificationType.CloudProfileFailed:
                        var profileNotification = Marshal.PtrToStructure<Notifications.InitNotificationCloudProfileFailed>(notificationPtr);
                        Balancy.Callbacks.OnCloudProfileFailedToLoad?.Invoke(new Balancy.Callbacks.ErrorStatus(profileNotification.Message));
                        break;
                    case Notifications.NotificationType.OnNewEventActivated: {
                        var liveOpsNewEvent = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewEventActivated>(notificationPtr);
                        var eventInfo = Profiles.System.SmartInfo.FindEventInfo(liveOpsNewEvent.EventInfo);
                        Balancy.Callbacks.OnNewEventActivated?.Invoke(eventInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnEventDeactivated: {
                        var liveOpsEvent = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnEventDeactivated>(notificationPtr);
                        var eventInfo = JsonBasedObject.CreateObject<EventInfo>(liveOpsEvent.EventInfo);
                        Balancy.Callbacks.OnEventDeactivated?.Invoke(eventInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnNewOfferActivated: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewOfferActivated>(notificationPtr);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferInfo(notificationTyped.OfferInfo);
                        Balancy.Callbacks.OnNewOfferActivated?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferDeactivated: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnOfferDeactivated>(notificationPtr);
                        var offerInfo = JsonBasedObject.CreateObject<OfferInfo>(notificationTyped.OfferInfo);
                        Balancy.Callbacks.OnOfferDeactivated?.Invoke(offerInfo, notificationTyped.WasPurchased);
                        break;
                    }
                    case Notifications.NotificationType.OnNewOfferGroupActivated: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewOfferGroupActivated>(notificationPtr);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferGroupInfo(notificationTyped.OfferInfo);
                        Balancy.Callbacks.OnNewOfferGroupActivated?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferGroupDeactivated: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnOfferGroupDeactivated>(notificationPtr);
                        var offerInfo = JsonBasedObject.CreateObject<OfferGroupInfo>(notificationTyped.OfferInfo);
                        Balancy.Callbacks.OnOfferGroupDeactivated?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnABTestStarted: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_ABTestStarted>(notificationPtr);
                        var abTestInfo = Profiles.System.TestsInfo.FindAbTestInfo(notificationTyped.ABTestInfo);
                        Balancy.Callbacks.OnNewAbTestStarted?.Invoke(abTestInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnABTestEnded: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_ABTestEnded>(notificationPtr);
                        var abTestInfo = Profiles.System.TestsInfo.FindAbTestInfo(notificationTyped.ABTestInfo);
                        Balancy.Callbacks.OnAbTestEnded?.Invoke(abTestInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnSegmentUpdated: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_SegmentUpdated>(notificationPtr);
                        var segmentInfo = Profiles.System.SegmentsInfo.FindSegmentInfo(notificationTyped.SegmentInfo);
                        Balancy.Callbacks.OnSegmentInfoUpdated?.Invoke(segmentInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnDailyBonusUpdated: {
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_DailyBonusUpdated>(notificationPtr);
                        var dailyInfo = Profiles.System.LiveOpsInfo.FindDailyBonusInfo(notificationTyped.DailyBonusInfo);
                        if (dailyInfo == null && notificationTyped.DailyBonusInfo != IntPtr.Zero)
                            dailyInfo = JsonBasedObject.CreateObject<DailyBonusInfo>(notificationTyped.DailyBonusInfo);
                        Balancy.Callbacks.OnDailyBonusUpdated?.Invoke(dailyInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnShopUpdated: {
                        // var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_ShopUpdated>(notificationPtr);
                        Balancy.Callbacks.OnShopUpdated?.Invoke();
                        break;
                    }
                    default:
                        Debug.LogError("**==> Unknown notification type. " + baseNotification.Type);
                        break;
                }
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
                Debug.LogError("Balancy Init Failed. Please provide Api Game Id in Config;");
                return false;
            }
            
            if (string.IsNullOrEmpty(appConfig.PublicKey))
            {
                Debug.LogError("Balancy Init Failed. Please provide Public Key in Config;");
                return false;
            }
            return true;
        }

        private static bool CheckCallbacks()
        {
            if (Balancy.Callbacks.OnDataUpdated == null)
            {
                Debug.LogError("Balancy Init Failed. Balancy.Callbacks.OnDataUpdated is mandatory, please provide it; You can't access any data before the callback is invoked;");
                return false;
            }

            return true;
        }
        
        internal enum Level {
            Off,
            Verbose,
            Debug,
            Info,
            Warn,
            Error
        };

        internal static void LogMessage(Level level, string message)
        {
            LogMessage((int)level, message);
        }
        
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
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.InvokeInMainThreadCallback))]
        private static void InvokeInMainThread(int id)
        {
            _mainThreadInstance.Enqueue(() =>
            {
                LibraryMethods.General.balancyInvokeMethodInMainThread(id);
            });
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