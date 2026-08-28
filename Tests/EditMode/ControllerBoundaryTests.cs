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
    }
}
