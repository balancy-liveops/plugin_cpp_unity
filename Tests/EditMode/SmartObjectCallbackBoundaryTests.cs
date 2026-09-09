using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Balancy.Models;
using Balancy.SmartObjects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class SmartObjectCallbackBoundaryTests
    {
        private const string TemplateName = "Tests.CallbackModel";

        private sealed class CallbackModel : BaseModel
        {
        }

        private sealed class DisposableProbe : IDisposable
        {
            private readonly Action _onDispose;

            public DisposableProbe(Action onDispose) => _onDispose = onDispose;

            public void Dispose() => _onDispose();
        }

        private static readonly Type SingletonDispatcher = typeof(BalancySingleton<>).Assembly
            .GetType("Balancy.SmartObjects.BalancySingletonDispatcher");
        private static readonly MethodInfo RegisterSingletonHandler = SingletonDispatcher
            .GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo DispatchSingleton = SingletonDispatcher
            .GetMethod("OnSingletonChangedStatic", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearSingletonHandlers = SingletonDispatcher
            .GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly Type ConditionalRegistry = typeof(BalancyConditionalTemplate<>).Assembly
            .GetType("Balancy.SmartObjects.BalancyConditionalTemplateRegistry");
        private static readonly MethodInfo RegisterConditionalInstance = ConditionalRegistry
            .GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearConditionalRegistry = ConditionalRegistry
            .GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly Type ConditionalType = typeof(BalancyConditionalTemplate<CallbackModel>);
        private static readonly IDictionary ConditionalInstances = (IDictionary)ConditionalType
            .GetField("_instances", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly MethodInfo DispatchConditional = ConditionalType
            .GetMethod("OnConditionalTemplateChangedStatic", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly IDictionary AllModels = (IDictionary)typeof(CMS)
            .GetField("AllModels", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly FieldInfo IsReady = typeof(CMS)
            .GetField("IsReadyToUse", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo SetModelData = typeof(BaseModel)
            .GetMethod("SetData", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            ClearSingletonHandlers.Invoke(null, null);
            ConditionalInstances.Clear();
            AllModels.Clear();
            IsReady.SetValue(null, true);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonHandlers.Invoke(null, null);
            ConditionalInstances.Clear();
            foreach (DictionaryEntry entry in AllModels)
                ((BaseModel)entry.Value).SetData(IntPtr.Zero);
            AllModels.Clear();
            IsReady.SetValue(null, false);
        }

        [Test]
        public void ThrowingSingletonHandlerCannotEscapeNativeDispatcher()
        {
            RegisterSingletonHandler.Invoke(null, new object[]
            {
                TemplateName,
                new Action<string>(_ => throw new InvalidOperationException("singleton handler failed"))
            });
            LogAssert.Expect(LogType.Exception, new Regex("singleton handler failed"));

            Assert.DoesNotThrow(() => DispatchSingleton.Invoke(null, new object[] { TemplateName, "model" }));
        }

        [Test]
        public void ThrowingConditionalSubscriberDoesNotSkipLaterSubscriberOrEscape()
        {
            CacheModel("model", new IntPtr(501));
            var wrapper = (BalancyConditionalTemplate<CallbackModel>)
                FormatterServices.GetUninitializedObject(ConditionalType);
            var laterCalls = 0;
            wrapper.OnStatusChanged += (_, __) =>
                throw new InvalidOperationException("conditional wrapper subscriber failed");
            wrapper.OnStatusChanged += (_, passed) =>
            {
                Assert.That(passed, Is.True);
                laterCalls++;
            };
            ConditionalInstances.Add(TemplateName, wrapper);
            LogAssert.Expect(LogType.Exception, new Regex("conditional wrapper subscriber failed"));

            Assert.DoesNotThrow(() => DispatchConditional.Invoke(null,
                new object[] { TemplateName, "model", true }));
            Assert.That(laterCalls, Is.EqualTo(1));
        }

        [Test]
        public void RegistryCleanupContinuesAfterOneWrapperThrows()
        {
            var laterCalls = 0;
            RegisterConditionalInstance.Invoke(null, new object[]
            {
                new DisposableProbe(() => throw new InvalidOperationException("wrapper dispose failed"))
            });
            RegisterConditionalInstance.Invoke(null, new object[]
            {
                new DisposableProbe(() => laterCalls++)
            });
            LogAssert.Expect(LogType.Exception, new Regex("wrapper dispose failed"));

            Assert.DoesNotThrow(() => ClearConditionalRegistry.Invoke(null, null));
            Assert.That(laterCalls, Is.EqualTo(1));
        }

        private static void CacheModel(string unnyId, IntPtr pointer)
        {
            var model = new CallbackModel();
            SetModelData.Invoke(model, new object[] { pointer, unnyId, TemplateName });
            AllModels.Add(unnyId, model);
        }
    }
}
