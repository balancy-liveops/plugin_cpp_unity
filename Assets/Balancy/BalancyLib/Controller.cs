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
        
        public static void Init(AppConfig appConfig)
        {
            if (!CheckConfig(appConfig))
                return;
            
            LibraryMethods.General.balancySetLogCallback(LogMessage);
            UnityFileManager.Init();
            LibraryMethods.Models.balancySetModelOnRefresh(ModelRefreshed);
            Profiles.Init();

            CppAppConfig config = CreateConfigForCPP(appConfig);
            IntPtr configPtr = Marshal.AllocHGlobal(Marshal.SizeOf(config));
            Marshal.StructureToPtr(config, configPtr, false);
            // PrintSizeAndOffsets<CppAppConfig>();
            LibraryMethods.General.balancyInit(configPtr);
        }

        public static void Stop()
        {
            LibraryMethods.Models.balancySetModelOnRefresh(null);
            LibraryMethods.General.balancyStop();
            Profiles.CleanUp();
        }

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
            try
            {
                var baseNotification = Marshal.PtrToStructure<Notifications.NotificationBase>(notificationPtr);
                Notifications.NotificationBase notification = baseNotification;
                switch (baseNotification.Type)
                {
                    case Notifications.NotificationType.DataIsReady:
                        var notificationDataIsReady =
                            Marshal.PtrToStructure<Notifications.InitNotificationDataIsReady>(notificationPtr);
                        notification = notificationDataIsReady;
                        DataUpdated(notificationDataIsReady.IsCMSUpdated, notificationDataIsReady.IsProfileUpdated);
                        _isReadyToUse = true;
                        break;
                    case Notifications.NotificationType.AuthFailed:
                        notification =
                            Marshal.PtrToStructure<Notifications.InitNotificationAuthFailed>(notificationPtr);
                        break;
                    case Notifications.NotificationType.CloudProfileFailed:
                        notification =
                            Marshal.PtrToStructure<Notifications.InitNotificationCloudProfileFailed>(notificationPtr);
                        break;
                    case Notifications.NotificationType.OnNewEventActivated:
                    {
                        var liveOpsNewEvent =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewEventActivated>(
                                notificationPtr);
                        notification = liveOpsNewEvent;
                        var eventInfo = Profiles.System.SmartInfo.FindEventInfo(liveOpsNewEvent.EventInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnEventDeactivated:
                    {
                        var liveOpsEvent =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnEventDeactivated>(
                                notificationPtr);
                        notification = liveOpsEvent;

                        // var eventInfo = JsonBasedObject.CreateObject<EventInfo>(liveOpsEvent.EventInfo);
                        // Debug.LogError("**==> EVENT off " + notification.Type + " : " +
                        //                eventInfo?.GameEvent?.Name?.Key);
                        break;
                    }
                    case Notifications.NotificationType.OnNewOfferActivated:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewOfferActivated>(
                                notificationPtr);
                        notification = notificationTyped;
                        var offerInfo = Profiles.System.SmartInfo.FindOfferInfo(notificationTyped.OfferInfo);
                        Debug.LogError("**==> OFFER ON " + notification.Type + " : " + offerInfo?.GameOffer?.Name?.Key);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferDeactivated:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnOfferDeactivated>(
                                notificationPtr);
                        notification = notificationTyped;

                        // var offerInfo = JsonBasedObject.CreateObject<OfferInfo>(notificationTyped.OfferInfo);
                        // Debug.LogError("**==> OFFER off " + notification.Type + " : " +
                        //                offerInfo?.GameOffer?.Name?.Key);
                        break;
                    }

                    case Notifications.NotificationType.OnNewOfferGroupActivated:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewOfferGroupActivated>(
                                notificationPtr);
                        notification = notificationTyped;
                        var offerInfo = Profiles.System.SmartInfo.FindOfferGroupInfo(notificationTyped.OfferInfo);
                        Debug.LogError("**==> OFFER Group ON " + notification.Type + " : " +
                                       offerInfo?.GameOfferGroup?.Name?.Key);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferGroupDeactivated:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnOfferGroupDeactivated>(
                                notificationPtr);
                        notification = notificationTyped;

                        var offerInfo = JsonBasedObject.CreateObject<OfferGroupInfo>(notificationTyped.OfferInfo);
                        Debug.LogError("**==> OFFER Group off " + notification.Type + " : " +
                                       offerInfo?.GameOfferGroup?.Name?.Key);
                        break;
                    }
                    
                    case Notifications.NotificationType.OnABTestStarted:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_ABTestStarted>(
                                notificationPtr);
                        notification = notificationTyped;
                        var offerInfo = Profiles.System.TestsInfo.FindAbTestInfo(notificationTyped.ABTestInfo);
                        Debug.LogError("**==> ABTEST Group ON : " +
                                       offerInfo?.Test?.Name + " Variant = " + offerInfo?.Variant?.Name + " FINISHED = " + offerInfo?.Finished);
                        break;
                    }
                    
                    case Notifications.NotificationType.OnABTestEnded:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_ABTestEnded>(
                                notificationPtr);
                        notification = notificationTyped;
                        var offerInfo = Profiles.System.TestsInfo.FindAbTestInfo(notificationTyped.ABTestInfo);
                        Debug.LogError("**==> ABTEST Group OFF : " +
                                       offerInfo?.Test?.Name + " Variant = " + offerInfo?.Variant?.Name + " FINISHED = " + offerInfo?.Finished);
                        break;
                    }
                    
                    case Notifications.NotificationType.OnSegmentUpdated:
                    {
                        var notificationTyped =
                            Marshal.PtrToStructure<Notifications.LiveOpsNotification_SegmentUpdated>(
                                notificationPtr);
                        notification = notificationTyped;
                        var offerInfo = Profiles.System.SegmentsInfo.FindSegmentIndo(notificationTyped.SegmentInfo);
                        Debug.LogError("**==> SEGMENT : " +
                                       offerInfo?.Segment?.Name + " Is in = " + offerInfo?.IsIn);
                        break;
                    }
                    default:
                        Debug.LogError("**==> Unknown notification type. " + baseNotification.Type);
                        break;
                }

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