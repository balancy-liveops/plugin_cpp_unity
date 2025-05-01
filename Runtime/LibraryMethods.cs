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

        public static class General
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void LogCallback(int level, string message);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void InvokeInMainThreadCallback(int id);
            
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // public delegate void SaveFileCallback(string path, string data);
                        
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate string LoadFileCallback(string path);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate bool IsFileExistsCallback(string path);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetLogCallback(LogCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetInvokeInMainThreadCallback(InvokeInMainThreadCallback callback);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInvokeMethodInMainThread(int id);
            
            //[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInit(IntPtr config);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyStop();
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInitUnityFileHelper(string persistentDataPath, string assetDataPath, LoadFileCallback loadFromResources, IsFileExistsCallback isFileExistsCallback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetInheritance(out int size);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetStatus();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyWebViewRequest(string paramName);
        }

        public static class Models
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DataObjectWasCachedCallback(string id, IntPtr ptr);

            
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
            public static extern int balancyGetLongParam(IntPtr instance, string paramName);
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
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetBaseDataParam(IntPtr instance, string paramName, string fileName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetListBaseDataParam(IntPtr instance, string paramName, string fileName);
            
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
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancySetProfileOnReset(ModelRefreshedCallback callback);
            
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
            public static extern bool balancyOfferGroupInfo_CanPurchase(IntPtr offerInfoPointer, IntPtr storeItemPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyEventInfo_GetSecondsLeftBeforeDeactivation(IntPtr eventInfoPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGameEvent_GetSecondsLeftBeforeDeactivation(IntPtr gameEventPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGameEvent_GetSecondsBeforeActivation(IntPtr gameEventPointer, bool ignoreTriggers);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGetTimeOffset();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySetTimeOffset(int seconds);
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
            internal delegate void ResponseCallback(IntPtr responseData);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseStoreItem(IntPtr storeItemPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseGameOffer(IntPtr gameOfferPointer);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancySoftPurchaseGameOfferGroup(IntPtr gameOfferPointer, IntPtr storeItemPointer);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseStoreItem(IntPtr storeItemPointer, Balancy.Core.PaymentInfo paymentInfo, ResponseCallback callback, bool requireValidation);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseGameOffer(IntPtr gameOfferPointer, Balancy.Core.PaymentInfo paymentInfo, ResponseCallback callback, bool requireValidation);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyHardPurchaseGameOfferGroup(IntPtr gameOfferPointer, IntPtr storeItemPointer, Balancy.Core.PaymentInfo paymentInfo, ResponseCallback callback, bool requireValidation);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyDailyBonus_claimNextReward(IntPtr dailyBonusInfo);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyDailyBonus_canClaimNextReward(IntPtr dailyBonusInfo);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyDailyBonus_getSecondsTillTheNextReward(IntPtr dailyBonusInfo);
        }
    }
}
