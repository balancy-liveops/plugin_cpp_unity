using System;
using System.Runtime.InteropServices;

namespace Balancy
{
    internal static class LibraryMethods
    {
#if (UNITY_IPHONE || UNITY_WEBGL) && !UNITY_EDITOR
        internal const string DllName = "__Internal";
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        internal const string DllName = "libBalancyCore";
        //internal const string DllName = "Assets/Balancy/Plugins/Windows/x86_64/libBalancyCore";
#else
        internal const string DllName = "libBalancyCore";
#endif
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ModelRefreshedCallback(string unnyId, IntPtr newPointer);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void UserDataInitializedCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RunFunctionCallback(string callbackDataJson, string responseCallbackId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ScriptCompletionCallback(string instanceId, string exitPort, string outputsJson);

        public static class General
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void LogCallback(int level, string message);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void InvokeInMainThreadCallback(int id);
            
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // public delegate void SaveFileCallback(string path, string data);
                        
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DataRequestedCallback(string sender, int command, string paramsJson, int requestId);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void WebviewRequestCallback(string message);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetLogCallback(LogCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetInvokeInMainThreadCallback(InvokeInMainThreadCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInvokeMethodInMainThread(int id);

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL-specific notification accessor functions (mirrors JSStatusNotification)
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetType(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyNotification_Release(int notificationId);
            
            // InitNotificationDataIsReady
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyNotification_IsCloudSynchronized(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyNotification_IsCMSUpdated(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyNotification_IsProfileUpdated(int notificationId);
            
            // InitNotificationError
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetMessage(int notificationId);
            
            // LiveOps notifications
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetSegmentInfo(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetDailyBonusInfo(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetEventInfo(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetOfferInfo(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetOfferGroupInfo(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetAbTestInfo(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetShopSlot(int notificationId);
            
            // Additional properties
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetStoreItemIndexInGroupOffer(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyNotification_WasPurchased(int notificationId);
            
            // Inventory notifications
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetInventory(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetInventoryItem(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetInventoryCount(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetInventorySlotIndex(int notificationId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetInventoryCurrentAmount(int notificationId);

            // Localization notifications
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetLocalizationCode(int notificationId);

            // Shop update notifications
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetShopChangeType(int notificationId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetShopPageIndex(int notificationId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyNotification_GetShopSlotIndex(int notificationId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyNotification_GetShopUnnyId(int notificationId);
#endif

            //[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInit(IntPtr config);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyStop();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyUpdate();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInitUnityFileHelper(string persistentDataPath, string resourcesPath, string codePath);

#if UNITY_ANDROID && !UNITY_EDITOR
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInitUnityFileHelperAndroid(string persistentDataPath, string streamingAssetsSubpath, string codePath);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAndroidPreloadResource(string fileName, string content);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAndroidSetResourceExists(string fileName, bool exists);
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void PreloadCompleteCallback();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyPreloadFromIndexedDB(PreloadCompleteCallback onComplete);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyPreloadFileFromStreamingAssets(string fileName, byte[] fileData, int dataSize);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyLoadFileFromCache(string fileName);
#endif

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetInheritance(out int size);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetStatus();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyWebViewRequest(IntPtr owner, string request, WebviewRequestCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetParsedObject(IntPtr instance, int depth, bool pretty);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetDataRequestedCallback(DataRequestedCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetRunFunctionCallback(RunFunctionCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyRunFunctionResponse(string responseCallbackId, string responseJson);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetScriptCompletionCallback(ScriptCompletionCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyDataRequestedResponse(int requestId, string response);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyViewAllowOptimization(bool allow);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetViewNotificationsCallback(WebviewRequestCallback callback);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void OnUnzipCallback(string id, string zipFilePath);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetUnzipCallback(OnUnzipCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyUnzipCompleted(string id, string zipFolderPath);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            public delegate string ExtractZipFromMemoryCallback(IntPtr zipData, int dataSize, bool includeHeaders);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetExtractZipFromMemoryCallback(ExtractZipFromMemoryCallback callback);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyInventory_AddItems(IntPtr itemRef, int count);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyInventory_RemoveItems(IntPtr itemRef, int count);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyInventory_GetTotalItemsCount(IntPtr itemRef);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyStartAbTestManually(IntPtr abTest, IntPtr abTestVariant);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyDataObjectCompileAllScripts();
        }

        public static class WebSocket
        {
            // Delegate types for WebSocket callbacks
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void ConnectRequestDelegate(int connectionId, string url, string authDataJson);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DisconnectRequestDelegate(int connectionId);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void SubscribeEventDelegate(int connectionId, string eventName);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void SendAckDelegate(int connectionId, int ackId, string responseData);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void SendMessageDelegate(int connectionId, string eventName, string data);
            
            // WebSocket registration callbacks (FROM C++ TO Unity)
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyRegisterWSConnectRequestCallback(ConnectRequestDelegate callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyRegisterWSDisconnectRequestCallback(DisconnectRequestDelegate callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyRegisterWSSubscribeEventCallback(SubscribeEventDelegate callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyRegisterWSSendAckCallback(SendAckDelegate callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyRegisterWSSendMessageCallback(SendMessageDelegate callback);

            // WebSocket event callbacks (FROM Unity TO C++)
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHandleWSConnectionStatusChanged(int connectionId, bool connected, string errorMessage);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHandleWSSocketIOEvent(int connectionId, string eventName, string eventData, bool needsAck, int ackId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHandleWSAckResponse(int connectionId, int ackId, string responseData);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHandleWSSocketIOError(int connectionId, int errorCode, string errorMessage);
        }

        public static class Models
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DataObjectWasCachedCallback(string id, IntPtr ptr);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DataObjectViewWasCachedCallback(string id, string oath);

            
            //Getters
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetModelByUnnyId(string unnyId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetModels(string templateName, bool includeChildren, out int size);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetModelUnnyIds(string templateName, bool includeChildren, out int size);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetTemplateName(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetObjectParam(IntPtr instance, string paramName, string fileName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetObjectArrayParam(IntPtr instance, string paramName, string fileName, out int size);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGetIntParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern long balancyGetLongParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern float balancyGetFloatParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetStringParam(IntPtr instance, string paramName);
    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyGetBoolParam(IntPtr instance, string paramName);
    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetIntArrayParam(IntPtr instance, string paramName, out int size);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetLongArrayParam(IntPtr instance, string paramName, out int size);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetFloatArrayParam(IntPtr instance, string paramName, out int size);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetBoolArrayParam(IntPtr instance, string paramName, out int size);
    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetStringArrayParam(IntPtr instance, string paramName, out int size);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyFreeStringArray(IntPtr array, int size);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetModelOnRefresh(ModelRefreshedCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetUserDataInitializedCallback(UserDataInitializedCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyDataObjectLoad(string unnyId, DataObjectWasCachedCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyDataObjectDeleteFromDisk(string unnyId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyDataObjectViewPreload(string unnyId, DataObjectViewWasCachedCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern sbyte balancyDataObjectIsCached(string unnyId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern sbyte balancyIsPreloadingInProgress();

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void PreloadCompleteCallback();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyWaitForPreloading(PreloadCompleteCallback callback);
        }

        public static class Singletons
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void SingletonChangedCallback(string templateName, string unnyId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetSingleton(string templateName);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySubscribeSingletonChanged(string templateName, SingletonChangedCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyUnsubscribeSingletonChanged(string templateName, int callbackId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyClearAllSingletonCallbacks();
        }

        public static class ConditionalTemplates
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void ConditionalTemplateChangedCallback(string templateName, string unnyId, bool passed);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySubscribeConditionalTemplateChanged(string templateName, ConditionalTemplateChangedCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyUnsubscribeConditionalTemplateChanged(string templateName, int callbackId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetActiveConditionalTemplates(string templateName, out int size);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyClearAllConditionalTemplateCallbacks();
        }

        public static class Data
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void ParamChangedCallback(IntPtr baseData, string paramName);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DataDestroyedCallback(IntPtr baseData);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetProfile(string profileName);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyResetAllProfiles();

            public delegate void ResetProfilesCallback();
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyResetAllProfilesWithCallback(ResetProfilesCallback onComplete);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyForceSaveSmartObjects();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetBaseDataParam(IntPtr instance, string paramName, string fileName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetListBaseDataParam(IntPtr instance, string paramName, string fileName);

            // SmartListSimple getter P/Invoke declarations
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetListSimpleIntParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetListSimpleFloatParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern IntPtr balancyGetListSimpleStringParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetListSimpleLongParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetListSimpleBoolParam(IntPtr instance, string paramName);

            // [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            // public static extern int balancySubscribeBaseDataParamChange(IntPtr instance, string paramName, IntPtr callback);
            //
            // [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            // public static extern IntPtr balancyUnsubscribeBaseDataParamChange(IntPtr instance, string paramName, int callbackId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetIntParam(IntPtr instance, string paramName, int value);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetLongParam(IntPtr instance, string paramName, long value);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetStringParam(IntPtr instance, string paramName, string value);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetFloatParam(IntPtr instance, string paramName, float value);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetBoolParam(IntPtr instance, string paramName, bool value);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancySmartListAddElement(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListGetSize(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancySmartListGetElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListRemoveElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListClear(IntPtr instance);

            // SmartListSimple<int> P/Invoke declarations
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleIntAddElement(IntPtr instance, int value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListSimpleIntGetSize(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListSimpleIntGetElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleIntSetElementAt(IntPtr instance, int index, int value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleIntRemoveAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleIntClear(IntPtr instance);

            // SmartListSimple<float> P/Invoke declarations
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleFloatAddElement(IntPtr instance, float value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListSimpleFloatGetSize(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern float balancySmartListSimpleFloatGetElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleFloatSetElementAt(IntPtr instance, int index, float value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleFloatRemoveAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleFloatClear(IntPtr instance);

            // SmartListSimple<string> P/Invoke declarations
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern void balancySmartListSimpleStringAddElement(IntPtr instance, string value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListSimpleStringGetSize(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern IntPtr balancySmartListSimpleStringGetElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern void balancySmartListSimpleStringSetElementAt(IntPtr instance, int index, string value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleStringRemoveAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleStringClear(IntPtr instance);

            // SmartListSimple<long> P/Invoke declarations
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleLongAddElement(IntPtr instance, long value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListSimpleLongGetSize(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern long balancySmartListSimpleLongGetElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleLongSetElementAt(IntPtr instance, int index, long value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleLongRemoveAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleLongClear(IntPtr instance);

            // SmartListSimple<bool> P/Invoke declarations
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleBoolAddElement(IntPtr instance, bool value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancySmartListSimpleBoolGetSize(IntPtr instance);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySmartListSimpleBoolGetElementAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleBoolSetElementAt(IntPtr instance, int index, bool value);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleBoolRemoveAt(IntPtr instance, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySmartListSimpleBoolClear(IntPtr instance);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetProfileOnReset(ModelRefreshedCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetBaseDataParamChanged(ParamChangedCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetBaseDataDestroyed(DataDestroyedCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyProfile_GetLastCloudSyncTime(IntPtr profile);
        }

        public static class Profile
        {
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySystemProfileTrackRevenue(Balancy.API.AdType adType, double revenue, string placement);
        }

        public static class Extra
        {
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyOfferInfo_GetSecondsLeftBeforeDeactivation(IntPtr offerInfoPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyOfferInfo_Activate(IntPtr offerInfoPointer);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyOfferInfo_DeactivateOffer(IntPtr offerBasePointer);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyOfferGroupInfo_CanPurchase(IntPtr offerInfoPointer, IntPtr storeItemPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyEventInfo_GetSecondsLeftBeforeDeactivation(IntPtr eventInfoPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGameEvent_GetSecondsLeftBeforeDeactivation(IntPtr gameEventPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGameEvent_GetSecondsBeforeActivation(IntPtr gameEventPointer, bool ignoreTriggers);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyGameEvent_StopEventManually(IntPtr gameEventPointer, int cooldown);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGetTimeOffset();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetTimeOffset(int seconds);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate uint CustomUTCTimeProviderDelegate();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetCustomUTCTimeProvider(CustomUTCTimeProviderDelegate callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyResetCustomUTCTimeProvider();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyNotifyAppPause(int secondsElapsed);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyNotifyAppResume();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyStoreItem_GetAdsWatched(IntPtr storeItemPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyStoreItem_AdWasWatched(IntPtr storeItemPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyStoreItem_HaveEnoughResources(IntPtr storeItemPointer);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyShopSlot_IsAvailable(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyShopSlot_GetSecondsLeftUntilAvailable(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyShopSlot_HasLimits(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyShopSlot_GetPurchasesLimitForCycle(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyShopSlot_GetPurchasesDoneDuringTheLastCycle(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern float balancyShopSlot_GetMultiplier(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyShopSlot_HasMultiplier(IntPtr shopSlotPointer);
        }

        public static class Localization
        {
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyLocalization_GetLocalizedValue(string key);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyLocalization_GetCurrentLocalizationCode();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLocalization_ChangeLocalization(string key);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyLocalization_GetAllLocalizationCodes(out int size);
        }

        #if UNITY_EDITOR
        public static class Editor
        {
            public enum Language {
                CSharp = 1,
                Cpp = 2,
                UnrealCpp = 3
            }
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void AuthCallback(IntPtr statusPtr);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void StringArrayCallback(IntPtr statusPtr, int size);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigLaunch(Language language);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigClose();
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyConfigGetStatus();
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigLoadListOfGames(StringArrayCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigLoadBranches(StringArrayCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigAuth(string email, string password, AuthCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigSignOut();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyConfigGetSelectedGame();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigSetSelectedGame(string gameId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyConfigGetSelectedBranchId();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigSetSelectedBranch(int branchId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigDownloadContentToResources(DownloadCompleteCallback onReadyCallback, ProgressUpdateCallback onProgressCallback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigGenerateCode(DownloadCompleteCallback onReadyCallback);
        }
        #endif
        
        public static class API
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void ResponseCallback(int callbackId, IntPtr responseData);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseStoreItem(IntPtr storeItemPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseShopSlot(IntPtr shopSlotPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseGameOffer(IntPtr gameOfferPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseGameOfferGroup(IntPtr gameOfferPointer, IntPtr storeItemPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyNutakuComplete(int userId, string orderId, int callbackId, ResponseCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHoolipayPending(string itemId, int platform, int callbackId, ResponseCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHoolipayClaim(string orderId, int callbackId, ResponseCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySteamInit(string userId, string itemId, string description, int callbackId, ResponseCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySteamComplete(string orderId, int callbackId, ResponseCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyGetProducts(int callbackId, ResponseCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyGetProduct(string productId, int callbackId, ResponseCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseStoreItem(IntPtr storeItemPointer, Balancy.Core.PaymentInfo paymentInfo, int callbackId, ResponseCallback callback, bool requireValidation);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseGameOffer(IntPtr gameOfferPointer, Balancy.Core.PaymentInfo paymentInfo, int callbackId, ResponseCallback callback, bool requireValidation);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseShopSlot(IntPtr shopSlotPointer, Balancy.Core.PaymentInfo paymentInfo, int callbackId, ResponseCallback callback, bool requireValidation);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseGameOfferGroup(IntPtr gameOfferPointer, IntPtr storeItemPointer, Balancy.Core.PaymentInfo paymentInfo, int callbackId, ResponseCallback callback, bool requireValidation);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyDailyBonus_claimNextReward(IntPtr dailyBonusInfo);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyDailyBonus_canClaimNextReward(IntPtr dailyBonusInfo);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyDailyBonus_getSecondsTillTheNextReward(IntPtr dailyBonusInfo);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyBattlePass_claimReward(IntPtr bpLinePointer, int index);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyBattlePass_getRewardStatus(IntPtr bpLinePointer, int index);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_Nutaku(string userId, string token, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_Steam(string userId, string token, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_AsGuest(int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_NameAndPassword(string name, string password, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLink_NameAndPassword(string name, string password, bool forceLink, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_EmailAndPassword(string email, string password, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLink_EmailAndPassword(string email, string password, bool forceLink, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_GetInfo(int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_UnlinkName(string name, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_UnlinkEmail(string email, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_SignOut(int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_Apple(string userId, string token, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_Google(string userId, string token, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_Facebook(string userId, string token, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyAuth_Firebase(string userId, string token, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLink_Apple(string userId, string token, bool forceLink, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLink_Google(string userId, string token, bool forceLink, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLink_Facebook(string userId, string token, bool forceLink, int callbackId, ResponseCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyLink_Firebase(string userId, string token, bool forceLink, int callbackId, ResponseCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyGenenal_LevelStarted();
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyGenenal_LevelCompleted();
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyGenenal_LevelFailed();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyTasks_ActivateTask(IntPtr taskPointer, IntPtr gameEventPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyTasks_DeactivateTask(IntPtr taskPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyTasks_ClaimReward(IntPtr taskPointer);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyTasks_RestoreFailedTask(IntPtr taskPointer);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyScripts_RunById(string scriptId, string launcherId, string inputJson);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyScripts_RunByName(string scriptName, string launcherId, string inputJson);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyScripts_Stop(string instanceId);
        }

        public static class Conditions
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void ConditionStatusChangedCallback(string unnyId, bool passed);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyConditionCanPass(IntPtr conditionPtr);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConditionSubscribe(IntPtr conditionPtr, ConditionStatusChangedCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConditionUnsubscribe(IntPtr conditionPtr);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyConditionGetSecondsLeftBeforeDeactivation(IntPtr conditionPtr);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyConditionGetSecondsBeforeActivation(IntPtr conditionPtr, bool ignoreTriggers);
        }

        public static class CustomConditions
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate bool CustomConditionCanPassCallback([MarshalAs(UnmanagedType.LPStr)] string unnyId);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void CustomConditionSubscribeCallback([MarshalAs(UnmanagedType.LPStr)] string unnyId);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyCustomConditionRegisterHandler(CustomConditionCanPassCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyCustomConditionRegisterSubscribeHandler(CustomConditionSubscribeCallback subscribeCallback, CustomConditionSubscribeCallback unsubscribeCallback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyCustomConditionUnregisterHandler();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyCustomConditionForceUpdate(string unnyId);
        }
    }
}
