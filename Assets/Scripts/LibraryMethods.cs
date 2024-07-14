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

#if UNITY_IPHONE && !UNITY_EDITOR
        [DllImport ("__Internal")]
#else
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
#endif
            public static extern void setLogCallback(LogCallback callback);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            // public static extern void balancyInit([In] AppConfig config);
            public static extern void balancyInit(IntPtr config);
        }

        public static class Models
        {
            //Getters
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetModelByUnnyId(string unnyId);    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetObjectParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int balancyGetIntParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern float balancyGetFloatParam(IntPtr instance, string paramName);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetStringParam(IntPtr instance, string paramName);
    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool balancyGetBoolParam(IntPtr instance, string paramName);
    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetIntArrayParam(IntPtr instance, string paramName, out int size);
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetFloatArrayParam(IntPtr instance, string paramName, out int size);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetBoolArrayParam(IntPtr instance, string paramName, out int size);
    
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr balancyGetStringArrayParam(IntPtr instance, string paramName, out int size);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void balancyFreeStringArray(IntPtr array);
        }
    }
}
