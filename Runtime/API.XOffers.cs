using System;
using System.Runtime.InteropServices;
using Balancy.Core;
using Balancy.Mail;

namespace Balancy
{
    public sealed class XOffersResponse : Responses.ResponseData
    {
        /// <summary>JSON validated by the native XOffers parser, or empty on failure.</summary>
        public string DataJson { get; internal set; } = string.Empty;
        /// <summary>The context object for GetContext, or empty for other operations.</summary>
        public string ContextJson { get; internal set; } = string.Empty;

        internal static XOffersResponse Copy(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                throw new InvalidOperationException("Null XOffers response");
            const int errorOffset = 1 + sizeof(int);
            int dataOffset = errorOffset + IntPtr.Size;
            return new XOffersResponse
            {
                Success = Marshal.ReadByte(pointer) == 1,
                ErrorCode = Marshal.ReadInt32(pointer, 1),
                ErrorMessage = MailboxInterop.Utf8(Marshal.ReadIntPtr(pointer, errorOffset)),
                DataJson = MailboxInterop.Utf8(Marshal.ReadIntPtr(pointer, dataOffset)) ?? string.Empty,
                ContextJson = MailboxInterop.Utf8(Marshal.ReadIntPtr(pointer, dataOffset + IntPtr.Size)) ?? string.Empty
            };
        }
    }

    public static partial class API
    {
        public static class XOffers
        {
            private sealed class Callback : CallbackWrapperBase
            {
                private readonly ResponseCallback<XOffersResponse> callback;
                internal Callback(ResponseCallback<XOffersResponse> value) { callback = value; }

                public override void InvokeCallback(IntPtr pointer)
                {
                    XOffersResponse response;
                    try { response = XOffersResponse.Copy(pointer); }
                    catch (Exception error) { response = Failure(error); }
                    Invoke(callback, response);
                }
            }

            public static void GetContext(string optionsJson, ResponseCallback<XOffersResponse> callback) =>
                Dispatch(callback, (id, done) => Native.GetContext(optionsJson ?? string.Empty, id, done));

            public static void GetAssignments(string origin, int? platform, ResponseCallback<XOffersResponse> callback) =>
                Dispatch(callback, (id, done) => Native.GetAssignments(origin ?? string.Empty, platform ?? -1, id, done));

            public static void CompleteOffer(string offerId, int? platform, ResponseCallback<XOffersResponse> callback) =>
                Dispatch(callback, (id, done) => Native.CompleteOffer(offerId ?? string.Empty, platform ?? -1, id, done));

            private static XOffersResponse Failure(Exception error) => new XOffersResponse
            {
                Success = false, ErrorCode = -1,
                ErrorMessage = error is EntryPointNotFoundException
                    ? "Native Balancy library must support XOffers interop version 1" : error.Message
            };

            private static void Invoke(ResponseCallback<XOffersResponse> callback, XOffersResponse response)
            {
                try { callback?.Invoke(response); }
                catch (Exception error) { UnityEngine.Debug.LogException(error); }
            }

            private static void Dispatch(ResponseCallback<XOffersResponse> callback, Action<int, LibraryMethods.API.ResponseCallback> start)
            {
                var registration = ProtectedFromGCCallback(callback, cb => new Callback(cb));
                try
                {
                    if (Native.GetInteropVersion() != 1)
                        throw new InvalidOperationException("Native Balancy library must support XOffers interop version 1");
                    start(registration.CallbackId, registration.StaticCallback);
                }
                catch (Exception error)
                {
                    bool ownsCallback;
                    lock (_callbackLock) ownsCallback = _callbackStorage.Remove(registration.CallbackId);
                    if (ownsCallback) Invoke(callback, Failure(error));
                }
            }

            private static class Native
            {
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_GetInteropVersion")]
                internal static extern int GetInteropVersion();
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_GetContext")]
                internal static extern void GetContext([MarshalAs(UnmanagedType.LPUTF8Str)] string optionsJson, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_GetAssignments")]
                internal static extern void GetAssignments([MarshalAs(UnmanagedType.LPUTF8Str)] string origin, int platform, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyXOffers_CompleteOffer")]
                internal static extern void CompleteOffer([MarshalAs(UnmanagedType.LPUTF8Str)] string offerId, int platform, int callbackId, LibraryMethods.API.ResponseCallback callback);
            }
        }
    }
}
