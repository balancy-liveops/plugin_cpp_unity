using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Balancy.Network;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void FragmentedUtf8WebSocketMessageIsDecodedOnlyAfterTheFinalFragment()
        {
            var bufferType = typeof(WebSocketConnection).Assembly
                .GetType("Balancy.Network.WebSocketTextMessageBuffer", true);
            var append = bufferType.GetMethod("Append", BindingFlags.Instance | BindingFlags.NonPublic);
            var buffer = (IDisposable)Activator.CreateInstance(bufferType, true);
            var bytes = Encoding.UTF8.GetBytes("42[\"profile:updated\",{\"name\":\"Игрок\"}]");
            var split = Array.IndexOf(bytes, (byte)0xD0) + 1;

            try
            {
                var first = append.Invoke(buffer, new object[] { bytes, split, false });
                var remainder = new byte[bytes.Length - split];
                Array.Copy(bytes, split, remainder, 0, remainder.Length);
                var completed = append.Invoke(buffer, new object[] { remainder, remainder.Length, true });

                Assert.That(first, Is.Null);
                Assert.That(completed, Is.EqualTo(Encoding.UTF8.GetString(bytes)));
            }
            finally
            {
                buffer.Dispose();
            }
        }

        [Test]
        public void WebSocketMessageBufferRejectsUnboundedPayloads()
        {
            var bufferType = typeof(WebSocketConnection).Assembly
                .GetType("Balancy.Network.WebSocketTextMessageBuffer", true);
            var append = bufferType.GetMethod("Append", BindingFlags.Instance | BindingFlags.NonPublic);
            var maxBytes = (int)bufferType.GetField("MaxMessageBytes",
                BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            var buffer = (IDisposable)Activator.CreateInstance(bufferType, true);

            try
            {
                var exception = Assert.Throws<TargetInvocationException>(() =>
                    append.Invoke(buffer, new object[] { new byte[maxBytes + 1], maxBytes + 1, true }));
                Assert.That(exception.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
            }
            finally
            {
                buffer.Dispose();
            }
        }

        [Test]
        public void SocketAuthenticationJsonEscapesUntrustedIdentifiers()
        {
            var buildAuth = typeof(WebSocketConnection)
                .GetMethod("BuildConnectAuthJson", BindingFlags.Static | BindingFlags.NonPublic);
            var auth = new SocketIOAuthData
            {
                gameId = "game-id",
                userId = "user\"\\line\nnext",
                environment = 2,
                token = "token\"\\value\nnext",
                deviceId = "device\"\\value"
            };

            var json = (string)buildAuth.Invoke(null, new object[] { auth });
            var parsed = JsonUtility.FromJson<AuthPayload>(json);

            Assert.That(parsed.game_id, Is.EqualTo(auth.gameId));
            Assert.That(parsed.user_id, Is.EqualTo(auth.userId));
            Assert.That(parsed.env, Is.EqualTo(auth.environment));
            Assert.That(parsed.token, Is.EqualTo(auth.token));
            Assert.That(parsed.device_id, Is.EqualTo(auth.deviceId));
        }

        [Test]
        public void SocketParserTreatsNullInputAsMalformedInsteadOfThrowing()
        {
            Assert.DoesNotThrow(() => SimpleJsonParser.ParseSocketIOEvent(null));
            Assert.That(SimpleJsonParser.ParseSocketIOEvent(null), Is.Null);
            Assert.That(SimpleJsonParser.UnquoteString(null), Is.Null);
        }

        [Serializable]
        private class AuthPayload
        {
            public string game_id;
            public string user_id;
            public int env;
            public string token;
            public string device_id;
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
