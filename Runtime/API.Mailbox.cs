using System;
using System.Runtime.InteropServices;
using Balancy.Core;
using Balancy.Mail;

namespace Balancy
{
    public static partial class API
    {
        private sealed class MailboxCallbackWrapper : CallbackWrapperBase
        {
            private readonly ResponseCallback<MailboxResponse> callback;
            internal MailboxCallbackWrapper(ResponseCallback<MailboxResponse> value) { callback = value; }
            public override void InvokeCallback(IntPtr pointer)
            {
                MailboxResponse response;
                try { response = MailboxInterop.Read(pointer); }
                catch (Exception error) { response = new MailboxResponse { Success = false, ErrorCode = -1, ErrorMessage = error.Message }; }
                InvokeMailboxCallback(callback, response);
            }
        }
        private static void InvokeMailboxCallback(ResponseCallback<MailboxResponse> callback, MailboxResponse response)
        {
            try { callback?.Invoke(response); }
            catch (Exception error) { UnityEngine.Debug.LogException(error); }
        }
        /// <summary>Explicit mailbox operations. No method grants items or changes the game save.</summary>
        public static class Mailbox
        {
            public static void Sync(ResponseCallback<MailboxResponse> callback) => Dispatch(callback, (id, cb) => Native.Sync(id, cb));
            public static void GetMessages(MailboxListOptions options, ResponseCallback<MailboxResponse> callback)
            {
                options = options ?? new MailboxListOptions();
                Dispatch(callback, (id, cb) => Native.GetMessages(options.Limit, options.Cursor ?? string.Empty, id, cb));
            }
            public static void GetMessages(ResponseCallback<MailboxResponse> callback) => GetMessages(null, callback);
            public static void GetMessage(string messageId, ResponseCallback<MailboxResponse> callback) => Dispatch(callback, (id, cb) => Native.GetMessage(messageId, id, cb));
            public static void MarkRead(string messageId, ResponseCallback<MailboxResponse> callback) => Dispatch(callback, (id, cb) => Native.MarkRead(messageId, id, cb));
            public static void MarkProcessed(string messageId, ResponseCallback<MailboxResponse> callback) => Dispatch(callback, (id, cb) => Native.MarkProcessed(messageId, id, cb));
            public static void Delete(string messageId, ResponseCallback<MailboxResponse> callback) => Dispatch(callback, (id, cb) => Native.Delete(messageId, id, cb));

            private static void Dispatch(ResponseCallback<MailboxResponse> callback, Action<int, LibraryMethods.API.ResponseCallback> start)
            {
                var registration = ProtectedFromGCCallback(callback, cb => new MailboxCallbackWrapper(cb));
                try
                {
                    if (Native.GetInteropVersion() != 3)
                        throw new InvalidOperationException("Native Balancy library must be rebuilt with numeric mailbox type and status support");
                    start(registration.CallbackId, registration.StaticCallback);
                }
                catch (Exception error)
                {
                    bool ownsCallback;
                    lock (_callbackLock) ownsCallback = _callbackStorage.Remove(registration.CallbackId);
                    if (ownsCallback) InvokeMailboxCallback(callback, new MailboxResponse {Success = false, ErrorCode = -1,
                        ErrorMessage = error is EntryPointNotFoundException ? "Native Balancy library must be rebuilt with mailbox support" : error.Message});
                }
            }
            private static class Native
            {
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_GetInteropVersion")]
                internal static extern int GetInteropVersion();
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_Sync")]
                internal static extern void Sync(int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_GetMessages")]
                internal static extern void GetMessages(int limit, [MarshalAs(UnmanagedType.LPUTF8Str)] string cursor, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_GetMessage")]
                internal static extern void GetMessage([MarshalAs(UnmanagedType.LPUTF8Str)] string messageId, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_MarkRead")]
                internal static extern void MarkRead([MarshalAs(UnmanagedType.LPUTF8Str)] string messageId, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_MarkProcessed")]
                internal static extern void MarkProcessed([MarshalAs(UnmanagedType.LPUTF8Str)] string messageId, int callbackId, LibraryMethods.API.ResponseCallback callback);
                [DllImport(LibraryMethods.DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "balancyMailbox_Delete")]
                internal static extern void Delete([MarshalAs(UnmanagedType.LPUTF8Str)] string messageId, int callbackId, LibraryMethods.API.ResponseCallback callback);
            }
        }
    }
}
