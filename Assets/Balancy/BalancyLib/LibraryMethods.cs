using System;
using System.Runtime.InteropServices;

namespace Balancy
{
    internal static class LibraryMethods
    {
        private const string DllName = "libBalancyCore";

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
            // public static extern void balancyInit([In] AppConfig config);
            public static extern void balancyInit(IntPtr config);
            
            
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyInitUnityFileHelper(string persistentDataPath, LoadFileCallback loadFromResources);
            
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
            public static extern IntPtr balancyGetObjectParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetObjectArrayParam(IntPtr instance, string paramName, out int size);
            
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
