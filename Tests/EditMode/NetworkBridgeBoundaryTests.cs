using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Balancy.Network;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class NetworkBridgeBoundaryTests
    {
        private static readonly MethodInfo ClearPendingActions = typeof(UnityMainThreadDispatcher)
            .GetMethod("ClearPendingActions", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly ICollection ExecutionQueue = (ICollection)typeof(UnityMainThreadDispatcher)
            .GetField("_executionQueue", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly FieldInfo WebSocketDispatcher = typeof(UnityWebSocketBridge)
            .GetField("_mainThreadInstance", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            ClearPendingActions.Invoke(null, null);
            WebSocketDispatcher.SetValue(null, null);
        }

        [TearDown]
        public void TearDown() => ClearPendingActions.Invoke(null, null);

        [Test]
        public void WebSocketNativeCallbacksCanArriveBeforeDispatcherInitialization()
        {
            AssertNativeCallbackQueues(typeof(UnityWebSocketBridge), "StaticOnConnectRequest",
                1, "wss://example.invalid", "{}");
            AssertNativeCallbackQueues(typeof(UnityWebSocketBridge), "StaticOnDisconnectRequest", 1);
            AssertNativeCallbackQueues(typeof(UnityWebSocketBridge), "StaticOnSubscribeEvent", 1, "event");
            AssertNativeCallbackQueues(typeof(UnityWebSocketBridge), "StaticOnSendAck", 1, 2, "ack");
            AssertNativeCallbackQueues(typeof(UnityWebSocketBridge), "StaticOnSendMessage", 1, "event", "data");

            Assert.That(QueueCount(), Is.EqualTo(5));
        }

        [Test]
        public void WebRequestNativeCallbacksAlsoRemainDispatcherIndependent()
        {
            AssertNativeCallbackQueues(typeof(UnityWebRequestBridge), "StaticOnWebRequestReceived",
                7, "https://example.invalid", "GET", null, null, 10);
            AssertNativeCallbackQueues(typeof(UnityWebRequestBridge), "StaticOnFileLoadReceived",
                8, "https://example.invalid/file", 10);

            Assert.That(QueueCount(), Is.EqualTo(2));
        }

        [Test]
        public void WebSocketCallbacksOnlyBelongToTheCurrentLiveConnection()
        {
            var canForward = typeof(UnityWebSocketBridge)
                .GetMethod("CanForwardForState", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = new WebSocketConnection(17, null);
            var current = new WebSocketConnection(17, null);
            var active = new Dictionary<int, WebSocketConnection> { [17] = current };

            Assert.That(canForward.Invoke(null, new object[] { true, active, 17, current }), Is.True);
            Assert.That(canForward.Invoke(null, new object[] { true, active, 17, previous }), Is.False);
            Assert.That(canForward.Invoke(null, new object[] { false, active, 17, current }), Is.False);

            previous.Dispose();
            current.Dispose();
        }

        private static void AssertNativeCallbackQueues(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.DoesNotThrow(() => method.Invoke(null, arguments));
        }

        private static int QueueCount()
        {
            lock (ExecutionQueue)
                return ExecutionQueue.Count;
        }
    }
}
