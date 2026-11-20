using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Balancy.Core;
using Balancy.XOffers;
using UnityEngine;

namespace Balancy
{
    public static partial class API
    {
        private sealed class XOffersCallbackWrapper<T> : CallbackWrapperBase where T : Responses.ResponseData, new()
        {
            private readonly ResponseCallback<T> callback;
            internal XOffersCallbackWrapper(ResponseCallback<T> value) { callback = value; }
            public override void InvokeCallback(IntPtr pointer)
            {
                T response;
                try { response = XOffersInterop.Read<T>(pointer); }
                catch (Exception error) { response = new T { Success = false, ErrorCode = -1, ErrorMessage = error.Message }; }
                InvokeXOffersCallback(callback, response);
            }
        }
        private static void InvokeXOffersCallback<T>(ResponseCallback<T> callback, T response) where T : Responses.ResponseData
        {
            try { callback?.Invoke(response); }
            catch (Exception error) { Debug.LogException(error); }
        }
        /// <summary>Explicit XOffers operations. The game owns segment calculation, goal checks and reward grants.</summary>
        public static class XOffers
        {
            [Serializable]
            private sealed class ContextRequest { public int[] targets; public string locale; public string origin; }

            public static void GetContext(XOffersContextOptions options, ResponseCallback<XOffersContextResponse> callback)
            {
                options = options ?? new XOffersContextOptions();
                if (options.Platform < -1 || options.Platform > short.MaxValue || options.Targets == null)
                {
                    InvokeXOffersCallback(callback, new XOffersContextResponse { Success = false, ErrorCode = 400, ErrorMessage = "Invalid XOffers options" });
                    return;
                }
                var targets = new int[options.Targets.Length];
                for (int i = 0; i < targets.Length; i++) targets[i] = (int)options.Targets[i];
                var json = JsonUtility.ToJson(new ContextRequest { targets = targets, locale = options.Locale, origin = options.Origin });
                if (options.Platform >= 0) json = json.Substring(0, json.Length - 1) + ",\"platform\":" + options.Platform.ToString(CultureInfo.InvariantCulture) + "}";
                Dispatch(callback, (id, cb) => Native.GetContext(json, id, cb));
            }
            public static void GetAssignments(string origin, ResponseCallback<XOffersAssignmentsResponse> callback, int platform = -1)
                => Dispatch(callback, (id, cb) => Native.GetAssignments(origin ?? string.Empty, platform, id, cb));
            public static void GetAssignments(ResponseCallback<XOffersAssignmentsResponse> callback)
                => GetAssignments(null, callback);
            /// <summary>Call only after the game decides the goal is complete. Success with Pending=true means queued for retry.</summary>
            public static void CompleteOffer(string offerId, ResponseCallback<XOffersCompletionResponse> callback, int platform = -1)
                => Dispatch(callback, (id, cb) => Native.CompleteOffer(offerId ?? string.Empty, platform, id, cb));

            private static void Dispatch<T>(ResponseCallback<T> callback, Action<int, LibraryMethods.API.ResponseCallback> start)
                where T : Responses.ResponseData, new()
            {
                var registration = ProtectedFromGCCallback(callback, cb => new XOffersCallbackWrapper<T>(cb));
                try
                {
                    if (Native.GetInteropVersion() != 1) throw new InvalidOperationException("Native Balancy library requires XOffers support");
                    start(registration.CallbackId, registration.StaticCallback);
                }
                catch (Exception error)
                {
                    bool ownsCallback;
                    lock (_callbackLock) ownsCallback = _callbackStorage.Remove(registration.CallbackId);
                    if (ownsCallback) InvokeXOffersCallback(callback, new T { Success = false, ErrorCode = -1,
                        ErrorMessage = error is EntryPointNotFoundException ? "Native Balancy library requires XOffers support" : error.Message });
                }
            }
            private static class Native
            {
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_GetInteropVersion")]
                internal static extern int GetInteropVersion();
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_GetContext")]
                internal static extern void GetContext([MarshalAs(UnmanagedType.LPUTF8Str)] string options, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_GetAssignments")]
                internal static extern void GetAssignments([MarshalAs(UnmanagedType.LPUTF8Str)] string origin, int platform, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_CompleteOffer")]
                internal static extern void CompleteOffer([MarshalAs(UnmanagedType.LPUTF8Str)] string offerId, int platform, int callbackId, LibraryMethods.API.ResponseCallback callback);
            }
        }
    }
}
