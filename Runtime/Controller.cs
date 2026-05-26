using System;
using System.Collections;
using System.Runtime.InteropServices;
using Balancy.Core;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Balancy
{
    public static class Controller
    {
        private static AppConfig _originalConfig;
        private static CppAppConfig _cppConfig;
        private static bool _isReadyToUse;
        private static bool _isInitialized;

        public static bool IsReadyToUse => _isReadyToUse;

        public static AppConfig Config => _originalConfig;

        private static UnityMainThreadDispatcher _mainThreadInstance;

        public static event Action OnCloudSynced;
        public static event Action<bool, bool> OnDataUpdated;

#if UNITY_EDITOR
        private static bool _editorCallbackRegistered;
#endif

        public static void Init(AppConfig appConfig)
        {
            if (!CheckConfig(appConfig))
                return;

            if (!CheckCallbacks())
                return;

            // If SDK was previously initialized (e.g. re-entering Play Mode with
            // "Do not reload Domain or Scene"), clean up the old session first.
            // Without this, stale C++ callbacks capture dangling pointers and SEGV.
            if (_isInitialized)
            {
                Stop();
            }

#if UNITY_EDITOR
            if (!_editorCallbackRegistered)
            {
                EditorApplication.playModeStateChanged += OnEditorPlayModeChanged;
                _editorCallbackRegistered = true;
            }
#endif

            _isReadyToUse = false;
            _isInitialized = true;
            CMS.SetIsReady(false);

            LibraryMethods.General.balancySetLogCallback(LogMessage);
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();
            _mainThreadInstance.StartCoroutine(InitCoroutine(appConfig));
        }

#if UNITY_EDITOR
        private static void OnEditorPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && _isInitialized)
            {
                Stop();
            }
        }
#endif

        private static IEnumerator InitCoroutine(AppConfig appConfig)
        {
            Balancy.Network.UnityWebRequestBridge.Initialize();
            Balancy.Network.UnityWebSocketBridge.Initialize();//temporary turn it off
            Balancy.UnzipBridge.Initialize(); // Initialize Unity ZIP bridge for all platforms

            LibraryMethods.General.balancySetInvokeInMainThreadCallback(InvokeInMainThread);
            yield return UnityFileManager.InitRuntime();

            LibraryMethods.Models.balancySetModelOnRefresh(ModelRefreshed);
            LibraryMethods.Models.balancySetUserDataInitializedCallback(UserDataInitialized);
            Profiles.Init();
            RenderViewsManager.Init();
            RunFunctionManager.Init();
            ScriptCompletionManager.Init();

            CustomConditions.Register();

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
            if (!_isInitialized)
                return;

            _isInitialized = false;

            try
            {
                // CRITICAL: Clear log callback FIRST before any other cleanup
                // Other cleanup operations may trigger logging, which would crash if callback is invalid
                LibraryMethods.General.balancySetLogCallback(null);

                _isReadyToUse = false;
                OnDataUpdated = null;
                LibraryMethods.Models.balancySetModelOnRefresh(null);
                LibraryMethods.Models.balancySetUserDataInitializedCallback(null);

                // Stop C# bridges BEFORE destroying C++ objects.
                // Running coroutines (web requests, unzip) can call back into C++
                // after balancyStop() destroys the native manager, causing
                // "mutex lock failed" and use-after-free crashes.
                Balancy.Network.UnityWebRequestBridge.Clear();
                UnzipBridge.Cleanup();

                CustomConditions.Unregister();
                LibraryMethods.General.balancyStop();
                Balancy.Dictionaries.DataObjectsManager.CleanUp();
                Profiles.CleanUp();
                CMS.CleanUp();
                LibraryMethods.General.balancySetInvokeInMainThreadCallback(null);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Balancy] Error during stop: {e.Message}");
            }
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

            if (profileChanged)
                RenderViewsManager.OnProfileUpdated();

            OnDataUpdated?.Invoke(dictsChanged, profileChanged);
        }

        public static Constants.DevicePlatform GetDevicePlatform()
        {
            return (Constants.DevicePlatform)(_cppConfig?.DevicePlatform ?? -1);
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
                Platform = (int)FindPlatform(_originalConfig.BalancyPlatform),
                DevicePlatform = (int)FindDevicePlatform(_originalConfig.DevicePlatform),
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

        private static Constants.BalancyPlatform FindPlatform(Constants.BalancyPlatform originalBalancyPlatform)
        {
            if (originalBalancyPlatform == Constants.BalancyPlatform.Undefined)
            {
#if UNITY_EDITOR
                var targetPlatform = EditorUserBuildSettings.activeBuildTarget;
                switch (targetPlatform)
                {
                    case BuildTarget.iOS:
                        return Constants.BalancyPlatform.IosAppStore;
                }
                return Constants.BalancyPlatform.AndroidGooglePlay;
#endif
                var platform = UnityEngine.Application.platform;

                switch (platform)
                {
                    case UnityEngine.RuntimePlatform.IPhonePlayer:
                        return Constants.BalancyPlatform.IosAppStore;
                    case UnityEngine.RuntimePlatform.Android:
                    case UnityEngine.RuntimePlatform.OSXEditor:
                    case UnityEngine.RuntimePlatform.LinuxEditor:
                    case UnityEngine.RuntimePlatform.WindowsEditor:
                        return Constants.BalancyPlatform.AndroidGooglePlay;
                    default:
                        return Constants.BalancyPlatform.Unknown;
                }
            }

            return originalBalancyPlatform;
        }
        
        private static Constants.DevicePlatform FindDevicePlatform(Constants.DevicePlatform originalPlatform)
        {
            if (originalPlatform == Constants.DevicePlatform.Unknown)
            {
#if UNITY_EDITOR
                var targetPlatform = EditorUserBuildSettings.activeBuildTarget;
                switch (targetPlatform)
                {
                    case BuildTarget.StandaloneOSX:
                        return Constants.DevicePlatform.OSXPlayer;
                    case BuildTarget.StandaloneWindows:
                        return Constants.DevicePlatform.WindowsPlayer;
                    case BuildTarget.iOS:
                        return Constants.DevicePlatform.IPhonePlayer;
                    case BuildTarget.Android:
                        return Constants.DevicePlatform.Android;
                    case BuildTarget.StandaloneWindows64:
                        return Constants.DevicePlatform.WindowsPlayer;
                    case BuildTarget.WebGL:
                        return Constants.DevicePlatform.WebGLPlayer;
                    case BuildTarget.WSAPlayer:
                        break;
                    case BuildTarget.StandaloneLinux64:
                        return Constants.DevicePlatform.LinuxPlayer;
                }
#endif
                var platform = UnityEngine.Application.platform;
                switch (platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        return Constants.DevicePlatform.WindowsPlayer;
                    case RuntimePlatform.OSXEditor:
                        return Constants.DevicePlatform.OSXPlayer;
                    case RuntimePlatform.LinuxEditor:
                        return Constants.DevicePlatform.LinuxPlayer;
                    default:
                        return (Constants.DevicePlatform)(int)platform;
                }
            }

            return originalPlatform;
        }
        
        [AOT.MonoPInvokeCallback(typeof(Balancy.ProgressUpdateCallback))]
        private static void OnProgressUpdate(string fileName, float progress)
        {
            _originalConfig?.OnProgressUpdateCallback?.Invoke(fileName, progress);
        }

        [AOT.MonoPInvokeCallback(typeof(Balancy.StatusUpdateCallback))]
        private static void OnStatusUpdate(IntPtr notificationPtr)
        {
            try
            {
                // Debug.Log($"[C# Notification] OnStatusUpdate called. Platform: {Application.platform}, notificationPtr: {notificationPtr}");
#if UNITY_WEBGL && !UNITY_EDITOR
                // In WebGL, notificationPtr is actually a notification ID, not a memory pointer
                int notificationId = (int)notificationPtr;
                var notificationType = (Notifications.NotificationType)LibraryMethods.General.balancyNotification_GetType(notificationId);
#else
                // On native platforms, unmarshal the notification struct from memory
                var baseNotification = Marshal.PtrToStructure<Notifications.NotificationBase>(notificationPtr);
                var notificationType = baseNotification.Type;
#endif
                switch (notificationType)
                {
                    case Notifications.NotificationType.CMSInited:
                    {
                        CMS.SetIsReady(true);
                        break;
                    }
                    case Notifications.NotificationType.DataIsReady:
#if UNITY_WEBGL && !UNITY_EDITOR
                        // In WebGL, use accessor functions
                        bool isCloudSynced = LibraryMethods.General.balancyNotification_IsCloudSynchronized(notificationId);
                        bool isCMSUpdated = LibraryMethods.General.balancyNotification_IsCMSUpdated(notificationId);
                        bool isProfileUpdated = LibraryMethods.General.balancyNotification_IsProfileUpdated(notificationId);
#else
                        var notificationDataIsReady = Marshal.PtrToStructure<Notifications.InitNotificationDataIsReady>(notificationPtr);
                        bool isCloudSynced = notificationDataIsReady.IsCloudSynced;
                        bool isCMSUpdated = notificationDataIsReady.IsCMSUpdated;
                        bool isProfileUpdated = notificationDataIsReady.IsProfileUpdated;
#endif
                        RenderViewsManager.RefreshScripts();
                        DataUpdated(isCMSUpdated, isProfileUpdated);
                        _isReadyToUse = true;
                        if (isCloudSynced)
                            OnCloudSynced?.Invoke();
                        Balancy.Callbacks.OnDataUpdated?.Invoke(new Balancy.Callbacks.DataUpdatedStatus(
                            isCloudSynced, 
                            isCMSUpdated,
                            isProfileUpdated));
                        break;
                    case Notifications.NotificationType.AuthFailed:
#if UNITY_WEBGL && !UNITY_EDITOR
                        string authMessage = Marshal.PtrToStringAnsi(LibraryMethods.General.balancyNotification_GetMessage(notificationId));
                        Balancy.Callbacks.OnAuthFailed?.Invoke(new Balancy.Callbacks.ErrorStatus(authMessage));
#else
                        var authNotification = Marshal.PtrToStructure<Notifications.InitNotificationAuthFailed>(notificationPtr);
                        Balancy.Callbacks.OnAuthFailed?.Invoke(new Balancy.Callbacks.ErrorStatus(authNotification.Message));
#endif
                        break;
                    case Notifications.NotificationType.CloudProfileFailed:
#if UNITY_WEBGL && !UNITY_EDITOR
                        string profileMessage = Marshal.PtrToStringAnsi(LibraryMethods.General.balancyNotification_GetMessage(notificationId));
                        Balancy.Callbacks.OnCloudProfileFailedToLoad?.Invoke(new Balancy.Callbacks.ErrorStatus(profileMessage));
#else
                        var profileNotification = Marshal.PtrToStructure<Notifications.InitNotificationCloudProfileFailed>(notificationPtr);
                        Balancy.Callbacks.OnCloudProfileFailedToLoad?.Invoke(new Balancy.Callbacks.ErrorStatus(profileNotification.Message));
#endif
                        break;
                    case Notifications.NotificationType.UserRefreshed:
                        Balancy.Callbacks.OnGameRefreshed?.Invoke();
                        break;
                    case Notifications.NotificationType.OnNewEventActivated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr eventInfoPtr = LibraryMethods.General.balancyNotification_GetEventInfo(notificationId);
                        var eventInfo = Profiles.System.SmartInfo.FindEventInfo(eventInfoPtr);
#else
                        var liveOpsNewEvent = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewEventActivated>(notificationPtr);
                        var eventInfo = Profiles.System.SmartInfo.FindEventInfo(liveOpsNewEvent.EventInfo);
#endif
                        if (eventInfo != null)
                            Balancy.Callbacks.OnNewEventActivated?.Invoke(eventInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnEventDeactivated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr eventInfoPtr = LibraryMethods.General.balancyNotification_GetEventInfo(notificationId);
                        var eventInfo = JsonBasedObject.CreateObject<EventInfo>(eventInfoPtr);
#else
                        var liveOpsEvent = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnEventDeactivated>(notificationPtr);
                        var eventInfo = JsonBasedObject.CreateObject<EventInfo>(liveOpsEvent.EventInfo);
#endif
                        if (eventInfo != null)
                            Balancy.Callbacks.OnEventDeactivated?.Invoke(eventInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnNewOfferActivated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr offerInfoPtr = LibraryMethods.General.balancyNotification_GetOfferInfo(notificationId);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferInfo(offerInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewOfferActivated>(notificationPtr);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferInfo(notificationTyped.OfferInfo);
#endif
                        if (offerInfo != null)
                            Balancy.Callbacks.OnNewOfferActivated?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferDeactivated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr offerInfoPtr = LibraryMethods.General.balancyNotification_GetOfferInfo(notificationId);
                        bool wasPurchased = LibraryMethods.General.balancyNotification_WasPurchased(notificationId);
                        var offerInfo = JsonBasedObject.CreateObject<OfferInfo>(offerInfoPtr);
                        if (offerInfo != null)
                            Balancy.Callbacks.OnOfferDeactivated?.Invoke(offerInfo, wasPurchased);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnOfferDeactivated>(notificationPtr);
                        var offerInfo = JsonBasedObject.CreateObject<OfferInfo>(notificationTyped.OfferInfo);
                        if (offerInfo != null)
                            Balancy.Callbacks.OnOfferDeactivated?.Invoke(offerInfo, notificationTyped.WasPurchased);
#endif
                        break;
                    }
                    case Notifications.NotificationType.OnNewOfferGroupActivated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr offerGroupInfoPtr = LibraryMethods.General.balancyNotification_GetOfferGroupInfo(notificationId);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferGroupInfo(offerGroupInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnNewOfferGroupActivated>(notificationPtr);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferGroupInfo(notificationTyped.OfferInfo);
#endif
                        if (offerInfo != null)
                            Balancy.Callbacks.OnNewOfferGroupActivated?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferGroupDeactivated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr offerGroupInfoPtr = LibraryMethods.General.balancyNotification_GetOfferGroupInfo(notificationId);
                        var offerInfo = JsonBasedObject.CreateObject<OfferGroupInfo>(offerGroupInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_OnOfferGroupDeactivated>(notificationPtr);
                        var offerInfo = JsonBasedObject.CreateObject<OfferGroupInfo>(notificationTyped.OfferInfo);
#endif
                        if (offerInfo != null)
                            Balancy.Callbacks.OnOfferGroupDeactivated?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnABTestStarted: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr abTestInfoPtr = LibraryMethods.General.balancyNotification_GetAbTestInfo(notificationId);
                        var abTestInfo = Profiles.System.TestsInfo.FindAbTestInfo(abTestInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_ABTestStarted>(notificationPtr);
                        var abTestInfo = Profiles.System.TestsInfo.FindAbTestInfo(notificationTyped.ABTestInfo);
#endif
                        Balancy.Callbacks.OnNewAbTestStarted?.Invoke(abTestInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnABTestEnded: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr abTestInfoPtr = LibraryMethods.General.balancyNotification_GetAbTestInfo(notificationId);
                        var abTestInfo = Profiles.System.TestsInfo.FindAbTestInfo(abTestInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_ABTestEnded>(notificationPtr);
                        var abTestInfo = Profiles.System.TestsInfo.FindAbTestInfo(notificationTyped.ABTestInfo);
#endif
                        Balancy.Callbacks.OnAbTestEnded?.Invoke(abTestInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnSegmentUpdated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr segmentInfoPtr = LibraryMethods.General.balancyNotification_GetSegmentInfo(notificationId);
                        var segmentInfo = Profiles.System.SegmentsInfo.FindSegmentInfo(segmentInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_SegmentUpdated>(notificationPtr);
                        var segmentInfo = Profiles.System.SegmentsInfo.FindSegmentInfo(notificationTyped.SegmentInfo);
#endif
                        Balancy.Callbacks.OnSegmentInfoUpdated?.Invoke(segmentInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnDailyBonusUpdated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr dailyBonusInfoPtr = LibraryMethods.General.balancyNotification_GetDailyBonusInfo(notificationId);
                        var dailyInfo = Profiles.System.LiveOpsInfo.FindDailyBonusInfo(dailyBonusInfoPtr);
                        if (dailyInfo == null && dailyBonusInfoPtr != IntPtr.Zero)
                            dailyInfo = JsonBasedObject.CreateObject<DailyBonusInfo>(dailyBonusInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_DailyBonusUpdated>(notificationPtr);
                        var dailyInfo = Profiles.System.LiveOpsInfo.FindDailyBonusInfo(notificationTyped.DailyBonusInfo);
                        if (dailyInfo == null && notificationTyped.DailyBonusInfo != IntPtr.Zero)
                            dailyInfo = JsonBasedObject.CreateObject<DailyBonusInfo>(notificationTyped.DailyBonusInfo);
#endif
                        Balancy.Callbacks.OnDailyBonusUpdated?.Invoke(dailyInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnShopUpdated: {
                        // var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_ShopUpdated>(notificationPtr);
                        Balancy.Callbacks.OnShopUpdated?.Invoke();
                        break;
                    }
                    case Notifications.NotificationType.OnNetworkDownloadStarted: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        // Debug.Log("[C# Notification] OnNetworkDownloadStarted - skipping in WebGL (not implemented)");
#else
                        var downloadStartedInfo = ReadNetworkDownloadStarted(notificationPtr);
                        if (downloadStartedInfo.HasValue)
                            Balancy.Callbacks.OnNetworkDownloadStarted?.Invoke(downloadStartedInfo.Value);
#endif
                        break;
                    }
                    case Notifications.NotificationType.OnNetworkDownloadFinished: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        // Debug.Log("[C# Notification] OnNetworkDownloadFinished - skipping in WebGL (not implemented)");
#else
                        var downloadFinishedInfo = ReadNetworkDownloadFinished(notificationPtr);
                        if (downloadFinishedInfo.HasValue)
                            Balancy.Callbacks.OnNetworkDownloadFinished?.Invoke(downloadFinishedInfo.Value);
#endif
                        break;
                    }
                    case Notifications.NotificationType.OnShopSlotWasPurchased: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr shopSlotPtr = LibraryMethods.General.balancyNotification_GetShopSlot(notificationId);
                        var offerInfo = Profiles.System.ShopsInfo.FindShopSlot(shopSlotPtr);
                        if (offerInfo == null)
                            offerInfo = JsonBasedObject.CreateObject<Balancy.Data.SmartObjects.ShopSlot>(shopSlotPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.PurchaseNotification_ShopSlotWasPurchased>(notificationPtr);
                        var offerInfo = Profiles.System.ShopsInfo.FindShopSlot(notificationTyped.ShopSlot);
                        if (offerInfo == null)
                            offerInfo = JsonBasedObject.CreateObject<Balancy.Data.SmartObjects.ShopSlot>(notificationTyped.ShopSlot);
#endif
                        Balancy.Callbacks.OnShopSlotWasPurchased?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferWasPurchased: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr offerInfoPtr = LibraryMethods.General.balancyNotification_GetOfferInfo(notificationId);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferInfo(offerInfoPtr);
                        if (offerInfo == null)
                            offerInfo = JsonBasedObject.CreateObject<OfferInfo>(offerInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.PurchaseNotification_OfferWasPurchased>(notificationPtr);
                        var offerInfo = Profiles.System.SmartInfo.FindOfferInfo(notificationTyped.OfferInfo);
                        if (offerInfo == null)
                            offerInfo = JsonBasedObject.CreateObject<OfferInfo>(notificationTyped.OfferInfo);
#endif
                        Balancy.Callbacks.OnOfferWasPurchased?.Invoke(offerInfo);
                        break;
                    }
                    case Notifications.NotificationType.DisconnectAnotherSessionConflict:
                    {
                        Balancy.Callbacks.OnDisconnected?.Invoke(Callbacks.DisconnectReason.AnotherSessionConflict);
                        break;
                    }
                    case Notifications.NotificationType.OnOfferGroupWasPurchased: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr offerGroupInfoPtr = LibraryMethods.General.balancyNotification_GetOfferGroupInfo(notificationId);
                        int storeItemIndex = LibraryMethods.General.balancyNotification_GetStoreItemIndexInGroupOffer(notificationId);
                        var offerGroupInfo = Profiles.System.SmartInfo.FindOfferGroupInfo(offerGroupInfoPtr);
                        if (offerGroupInfo == null)
                            offerGroupInfo = JsonBasedObject.CreateObject<OfferGroupInfo>(offerGroupInfoPtr);
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.PurchaseNotification_OfferGroupWasPurchased>(notificationPtr);
                        int storeItemIndex = notificationTyped.StoreItemIndexInGroupOffer;
                        var offerGroupInfo = Profiles.System.SmartInfo.FindOfferGroupInfo(notificationTyped.OfferGroupInfo);
                        if (offerGroupInfo == null)
                            offerGroupInfo = JsonBasedObject.CreateObject<OfferGroupInfo>(notificationTyped.OfferGroupInfo);
#endif
                        
                        Balancy.Models.SmartObjects.StoreItem storeItem = null;
                        if (offerGroupInfo?.GameOfferGroup?.StoreItems != null && 
                            storeItemIndex >= 0 && 
                            storeItemIndex < offerGroupInfo.GameOfferGroup.StoreItems.Length)
                        {
                            storeItem = offerGroupInfo.GameOfferGroup.StoreItems[storeItemIndex];
                        }
                        
                        Balancy.Callbacks.OnOfferGroupWasPurchased?.Invoke(offerGroupInfo, storeItem);
                        break;
                    }
                    case Notifications.NotificationType.OnInventoryUpdated: {
#if UNITY_WEBGL && !UNITY_EDITOR
                        IntPtr inventoryPtr = LibraryMethods.General.balancyNotification_GetInventory(notificationId);
                        var inventory = Profiles.System.Inventories.FindInventory(inventoryPtr);
                        if (inventory != null)
                        {
                            string itemId = Marshal.PtrToStringAnsi(LibraryMethods.General.balancyNotification_GetInventoryItem(notificationId));
                            int count = LibraryMethods.General.balancyNotification_GetInventoryCount(notificationId);
                            int slotIndex = LibraryMethods.General.balancyNotification_GetInventorySlotIndex(notificationId);
                            int currentAmount = LibraryMethods.General.balancyNotification_GetInventoryCurrentAmount(notificationId);
                            var item = !string.IsNullOrEmpty(itemId) ? CMS.GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(itemId) : null;
                            Balancy.Callbacks.OnInventoryUpdated?.Invoke(inventory, item, count, slotIndex, currentAmount);
                        }
#else
                        var notificationTyped = Marshal.PtrToStructure<Notifications.LiveOpsNotification_InventoryUpdated>(notificationPtr);
                        var inventory = Profiles.System.Inventories.FindInventory(notificationTyped.Inventory);
                        if (inventory != null)
                        {
                            string itemId = notificationTyped.Item;
                            var item = !string.IsNullOrEmpty(itemId) ? CMS.GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(itemId) : null;
                            Balancy.Callbacks.OnInventoryUpdated?.Invoke(inventory, item, notificationTyped.Count, notificationTyped.SlotIndex, notificationTyped.CurrentAmount);
                        }
#endif
                        break;
                    }
                    default:
                        Debug.LogError("**==> Unknown notification type. " + notificationType);
                        break;
                }
#if UNITY_WEBGL && !UNITY_EDITOR
                // Release notification after processing in WebGL
                LibraryMethods.General.balancyNotification_Release(notificationId);
#endif
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

        // Safe string read from native char* pointer — returns null if the pointer looks invalid.
        // Prevents SIGSEGV in strlen when pointer is corrupted (e.g. 0x00000001).
        private static string SafePtrToStringAnsi(IntPtr strPtr)
        {
            if (strPtr == IntPtr.Zero || (long)strPtr < 0x1000)
                return null;
            return Marshal.PtrToStringAnsi(strPtr);
        }

        // Read NetworkNotification_DownloadStarted using PtrToStructure with IntPtr fields
        // (no LPStr marshalling) to avoid native crash when string pointers are corrupted.
        private static Callbacks.NetworkDownloadInfo? ReadNetworkDownloadStarted(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var n = Marshal.PtrToStructure<Notifications.NetworkNotification_DownloadStarted>(ptr);

            string url = SafePtrToStringAnsi(n.UrlPtr);
            string relativePath = SafePtrToStringAnsi(n.RelativePathPtr);
            string domain = SafePtrToStringAnsi(n.DomainPtr);

            if (url == null && relativePath == null && domain == null)
                return null; // All pointers invalid — notification memory likely corrupted

            return new Callbacks.NetworkDownloadInfo(
                url ?? "",
                relativePath ?? "",
                domain ?? "",
                n.IsCDNRequest);
        }

        // Read NetworkNotification_DownloadFinished using PtrToStructure with IntPtr fields.
        private static Callbacks.NetworkDownloadCompletedInfo? ReadNetworkDownloadFinished(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var n = Marshal.PtrToStructure<Notifications.NetworkNotification_DownloadFinished>(ptr);

            string url = SafePtrToStringAnsi(n.UrlPtr);
            string relativePath = SafePtrToStringAnsi(n.RelativePathPtr);
            string domain = SafePtrToStringAnsi(n.DomainPtr);
            string errorMessage = SafePtrToStringAnsi(n.ErrorMessagePtr);

            if (url == null && relativePath == null && domain == null)
                return null; // All pointers invalid — notification memory likely corrupted

            return new Callbacks.NetworkDownloadCompletedInfo(
                url ?? "",
                relativePath ?? "",
                domain ?? "",
                n.IsCDNRequest,
                n.TimeMs,
                n.SpeedKBps,
                n.DownloadedBytes,
                n.Success,
                n.ErrorCode,
                errorMessage ?? "",
                n.Attempts);
        }
    }
}