using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class ControllerBoundaryTests
    {
        private static readonly MethodInfo OnStatusUpdate = typeof(Controller)
            .GetMethod("OnStatusUpdate", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearCallbacks = typeof(Callbacks)
            .GetMethod("ClearAll", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            Main.Stop();
            ClearCallbacks.Invoke(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Main.Stop();
            ClearCallbacks.Invoke(null, null);
        }

        [Test]
        public void InitRejectsNullConfigBeforeCrossingNativeBoundary()
        {
            LogAssert.Expect(LogType.Error, "Balancy Init Failed. Config must not be null;");
            Assert.DoesNotThrow(() => Main.Init(null));
            Assert.That(Main.IsReadyToUse, Is.False);
        }

        [Test]
        public void InitRejectsMissingCredentialsBeforeCrossingNativeBoundary()
        {
            LogAssert.Expect(LogType.Error, "Balancy Init Failed. Please provide Api Game Id in Config;");
            Assert.DoesNotThrow(() => Main.Init(new AppConfig { PublicKey = "key" }));

            LogAssert.Expect(LogType.Error, "Balancy Init Failed. Please provide Public Key in Config;");
            Assert.DoesNotThrow(() => Main.Init(new AppConfig { ApiGameId = "game" }));
            Assert.That(Main.IsReadyToUse, Is.False);
        }

        [Test]
        public void NullNativeNotificationCannotEscapeIntoNativeCaller()
        {
            LogAssert.Expect(LogType.Error, new Regex("System\\.NullReferenceException"));
            Assert.DoesNotThrow(() => OnStatusUpdate.Invoke(null, new object[] { IntPtr.Zero }));
        }

        [Test]
        public void ThrowingGameNotificationCallbackCannotEscapeIntoNativeCaller()
        {
            var pointer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                // Notifications.NotificationType.SignedOut == 20. NotificationBase
                // starts with that enum on native platforms.
                Marshal.WriteInt32(pointer, 20);
                var calls = 0;
                Callbacks.OnSignedOut += () =>
                {
                    calls++;
                    throw new InvalidOperationException("signed-out callback failure");
                };
                LogAssert.Expect(LogType.Error, new Regex("signed-out callback failure"));

                Assert.DoesNotThrow(() => OnStatusUpdate.Invoke(null, new object[] { pointer }));
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [Test]
        public void ThrowingSignedOutSubscriberDoesNotSkipLaterSubscribers()
        {
            var calls = 0;
            Callbacks.OnSignedOut += () => throw new InvalidOperationException("first signed-out subscriber failed");
            Callbacks.OnSignedOut += () => calls++;

            LogAssert.Expect(LogType.Error, new Regex("first signed-out subscriber failed"));
            Assert.DoesNotThrow(() => DispatchNotification(20)); // SignedOut
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingUserRefreshedSubscriberDoesNotSkipLaterSubscribers()
        {
            var calls = 0;
            Callbacks.OnGameRefreshed += () => throw new InvalidOperationException("first refreshed subscriber failed");
            Callbacks.OnGameRefreshed += () => calls++;

            LogAssert.Expect(LogType.Error, new Regex("first refreshed subscriber failed"));
            Assert.DoesNotThrow(() => DispatchNotification(4)); // UserRefreshed
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingDisconnectedSubscriberDoesNotSkipLaterSubscribers()
        {
            var calls = 0;
            Callbacks.OnDisconnected += _ => throw new InvalidOperationException("first disconnected subscriber failed");
            Callbacks.OnDisconnected += reason =>
            {
                Assert.That(reason, Is.EqualTo(Callbacks.DisconnectReason.AnotherSessionConflict));
                calls++;
            };

            LogAssert.Expect(LogType.Error, new Regex("first disconnected subscriber failed"));
            Assert.DoesNotThrow(() => DispatchNotification(6)); // DisconnectAnotherSessionConflict
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void UnknownNotificationIsReportedWithoutThrowing()
        {
            var pointer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(pointer, int.MaxValue);
                LogAssert.Expect(LogType.Error, new Regex("Unknown notification type"));
                Assert.DoesNotThrow(() => OnStatusUpdate.Invoke(null, new object[] { pointer }));
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        private static void DispatchNotification(int type)
        {
            var pointer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(pointer, type);
                OnStatusUpdate.Invoke(null, new object[] { pointer });
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }
}
