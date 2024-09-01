using System;
using System.Runtime.InteropServices;
using Balancy.LiveOps;

namespace Balancy
{
    internal static class LibraryMethods
    {
        private const string DllName = "libBalancyCore";
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ModelRefreshedCallback(string unnyId, IntPtr newPointer);

        public static class General
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void LogCallback(int level, string message);
            
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void SaveFileCallback(string path, string data);
                        
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate string LoadFileCallback(string path);
            
#if UNITY_IPHONE && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
#endif
            public static extern void balancySetLogCallback(LogCallback callback);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInit(IntPtr config);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyStop();
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInitUnityFileHelper(string persistentDataPath, string assetDataPath, LoadFileCallback loadFromResources);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetInheritance(out int size);
        }

        public static class Models
        {
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
            public static extern IntPtr balancySetModelOnRefresh(ModelRefreshedCallback callback);
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
        }

        public static class Profile
        {
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancySystemProfileTrackRevenue(Ads.AdType adType, double revenue, string placement);
        }

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
            public static extern void balancyConfigAuth(string email, string password, AuthCallback callback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigSignOut();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyConfigGetSelectedGame();
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigSetSelectedGame(string gameId);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigDownloadContentToResources(Constants.Environment environment, DownloadCompleteCallback onReadyCallback, ProgressUpdateCallback onProgressCallback);
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyConfigGenerateCode(Constants.Environment environment, DownloadCompleteCallback onReadyCallback);
        }
    }
}
