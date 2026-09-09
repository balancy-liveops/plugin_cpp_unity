using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Balancy.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class AuthCallbackInteropTests
    {
        private static readonly MethodInfo ProtectCallback = typeof(API).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => method.Name == "ProtectedFromGCCallback" && method.IsGenericMethodDefinition);
        private static readonly MethodInfo StaticResponseHandler = typeof(API)
            .GetMethod("StaticResponseHandler", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo CallbackStorage = typeof(API)
            .GetField("_callbackStorage", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo CleanupCallbacks = typeof(API)
            .GetMethod("CleanUpPendingCallbacks", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => CleanupCallbacks.Invoke(null, null);

        [TearDown]
        public void TearDown() => CleanupCallbacks.Invoke(null, null);

        [Test]
        public void AuthResponseIsCopiedFromNativeAndDeliveredExactlyOnce()
        {
            Responses.AuthResponseData received = null;
            var callbackId = Register<Responses.AuthResponseData>(response => received = response);
            var native = new Responses.AuthResponseData
            {
                Success = false,
                ErrorCode = 401,
                ErrorMessage = "bad credentials",
                UserId = "user-17"
            };

            WithNativeStruct(native, pointer => StaticResponseHandler.Invoke(null, new object[] { callbackId, pointer }));

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Success, Is.False);
            Assert.That(received.ErrorCode, Is.EqualTo(401));
            Assert.That(received.ErrorMessage, Is.EqualTo("bad credentials"));
            Assert.That(received.UserId, Is.EqualTo("user-17"));

            LogAssert.Expect(LogType.Error, $"Callback with ID {callbackId} not found");
            WithNativeStruct(native, pointer => StaticResponseHandler.Invoke(null, new object[] { callbackId, pointer }));
        }

        [Test]
        public void LinkAndUserInfoResponsesKeepTheirDerivedFields()
        {
            Responses.LinkResponseData link = null;
            Responses.UserInfoResponseData info = null;
            var linkId = Register<Responses.LinkResponseData>(response => link = response);
            var infoId = Register<Responses.UserInfoResponseData>(response => info = response);

            WithNativeStruct(new Responses.LinkResponseData { Success = true, UserId = "linked" },
                pointer => StaticResponseHandler.Invoke(null, new object[] { linkId, pointer }));
            WithNativeStruct(new Responses.UserInfoResponseData
                {
                    Success = true,
                    UserId = "current",
                    NetworksJson = "[{\"kind\":\"google\",\"value\":\"g\"}]"
                },
                pointer => StaticResponseHandler.Invoke(null, new object[] { infoId, pointer }));

            Assert.That(link.UserId, Is.EqualTo("linked"));
            Assert.That(info.UserId, Is.EqualTo("current"));
            Assert.That(info.NetworksJson, Does.Contain("google"));
        }

        [Test]
        public void ThrowingGameCallbackIsRemovedAndCannotEscapeNativeDispatch()
        {
            var calls = 0;
            var callbackId = Register<Responses.AuthResponseData>(_ =>
            {
                calls++;
                throw new InvalidOperationException("auth callback failure");
            });
            LogAssert.Expect(LogType.Error, new Regex("auth callback failure"));

            Assert.DoesNotThrow(() => WithNativeStruct(new Responses.AuthResponseData { Success = true },
                pointer => StaticResponseHandler.Invoke(null, new object[] { callbackId, pointer })));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(StorageCount(), Is.Zero);
        }

        [Test]
        public void CleanupDiscardsCallbacksFromPreviousSession()
        {
            var calls = 0;
            var callbackId = Register<Responses.ResponseData>(_ => calls++);
            Assert.That(StorageCount(), Is.EqualTo(1));
            CleanupCallbacks.Invoke(null, null);

            LogAssert.Expect(LogType.Error, $"Callback with ID {callbackId} not found");
            WithNativeStruct(new Responses.ResponseData { Success = true },
                pointer => StaticResponseHandler.Invoke(null, new object[] { callbackId, pointer }));

            Assert.That(calls, Is.Zero);
            Assert.That(StorageCount(), Is.Zero);
        }

        [Test]
        public void ConcurrentRegistrationsGetUniqueIdsAndCanBeCleanedAtomically()
        {
            const int count = 64;
            var tasks = Enumerable.Range(0, count)
                .Select(_ => Task.Run(() => Register<Responses.AuthResponseData>(response => { })))
                .ToArray();
            Task.WaitAll(tasks);
            var ids = tasks.Select(task => task.Result).ToArray();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(count));
            Assert.That(StorageCount(), Is.EqualTo(count));
            CleanupCallbacks.Invoke(null, null);
            Assert.That(StorageCount(), Is.Zero);
        }

        private static int Register<T>(ResponseCallback<T> callback) where T : Responses.ResponseData
        {
            var result = ProtectCallback.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { callback, null });
            return (int)result.GetType().GetField("CallbackId").GetValue(result);
        }

        private static int StorageCount() => ((IDictionary)CallbackStorage.GetValue(null)).Count;

        private static void WithNativeStruct<T>(T value, Action<IntPtr> action)
        {
            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            try
            {
                Marshal.StructureToPtr(value, pointer, false);
                action(pointer);
            }
            finally
            {
                Marshal.DestroyStructure<T>(pointer);
                Marshal.FreeHGlobal(pointer);
            }
        }
    }
}
