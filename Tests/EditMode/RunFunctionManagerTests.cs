using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class RunFunctionManagerTests
    {
        private static int _executions;
        private static readonly Type Manager = typeof(RunFunctionManager);
        private static readonly MethodInfo OnRunFunctionRequested = Manager.GetMethod(
            "OnRunFunctionRequested", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo CleanUp = Manager.GetMethod(
            "CleanUp", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ConvertStringToType = Manager.GetMethod(
            "ConvertStringToType", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo FormatInvariant = Manager.GetMethod(
            "FormatInvariant", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo PendingCallbacks = Manager.GetField(
            "_pendingCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo IsInitialized = Manager.GetField(
            "_isInitialized", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo Dispatcher = Manager.GetField(
            "_mainThreadDispatcher", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ProcessQueue = typeof(UnityMainThreadDispatcher)
            .GetMethod("ProcessQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearPendingActions = typeof(UnityMainThreadDispatcher)
            .GetMethod("ClearPendingActions", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => CleanUp?.Invoke(null, null);

        [TearDown]
        public void TearDown() => CleanUp?.Invoke(null, null);

        [Test]
        public void LateRequestOutsideActiveSessionCannotEscapeNativeBoundary()
        {
            Assert.That(CleanUp, Is.Not.Null, "manager must expose teardown for Controller.Stop");
            Assert.DoesNotThrow(() => OnRunFunctionRequested.Invoke(null, new object[]
            {
                "{\"path\":\"Game.Method\",\"parameters\":[]}", "old-session-callback"
            }));
        }

        [Test]
        public void CleanupDropsPendingResponsesFromPreviousSession()
        {
            Assert.That(CleanUp, Is.Not.Null, "manager must expose teardown for Controller.Stop");
            var pending = (IDictionary)PendingCallbacks.GetValue(null);
            var valueType = pending.GetType().GetGenericArguments()[1];
            pending.Add("stale", Activator.CreateInstance(valueType));

            CleanUp.Invoke(null, null);

            Assert.That(pending.Count, Is.Zero);
        }

        [Test]
        public void RequestQueuedByOldSessionCannotRunAfterReinitialization()
        {
            _executions = 0;
            ClearPendingActions.Invoke(null, null);
            var gameObject = new UnityEngine.GameObject("RunFunction generation test");
            try
            {
                var dispatcher = gameObject.AddComponent<UnityMainThreadDispatcher>();
                Dispatcher.SetValue(null, dispatcher);
                IsInitialized.SetValue(null, true);
                OnRunFunctionRequested.Invoke(null, new object[]
                {
                    "{\"path\":\"Balancy.Tests.RunFunctionManagerTests.MarkExecuted\",\"parameters\":[]}", "old-session"
                });

                CleanUp.Invoke(null, null);
                Dispatcher.SetValue(null, dispatcher);
                IsInitialized.SetValue(null, true); // simulate the next Init without native calls

                Assert.DoesNotThrow(() => ProcessQueue.Invoke(dispatcher, null));
                Assert.That(_executions, Is.Zero);
                Assert.That(((IDictionary)PendingCallbacks.GetValue(null)).Count, Is.Zero);
            }
            finally
            {
                ClearPendingActions.Invoke(null, null);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        public static void MarkExecuted() => _executions++;

        [Test]
        public void NumericParametersUseInvariantCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                Assert.That(Convert("1.5", "float"), Is.EqualTo(1.5f));
                Assert.That(Convert("1.5", "double"), Is.EqualTo(1.5d));
                Assert.That(Convert("9223372036854775807", "long"), Is.EqualTo(long.MaxValue));
                Assert.That(Convert("18446744073709551615", "ulong"), Is.EqualTo(ulong.MaxValue));
                Assert.That(FormatInvariant.Invoke(null, new object[] { 1.5d }), Is.EqualTo("1.5"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void EmptyParameterValuesRemainNullForOptionalHandling()
        {
            Assert.That(Convert(null, "int"), Is.Null);
            Assert.That(Convert("", "bool"), Is.Null);
            Assert.That(Convert("value", null), Is.EqualTo("value"));
            Assert.That(FormatInvariant.Invoke(null, new object[] { null }), Is.EqualTo(""));
        }

        private static object Convert(string value, string valueType) =>
            ConvertStringToType.Invoke(null, new object[] { value, valueType });
    }
}
