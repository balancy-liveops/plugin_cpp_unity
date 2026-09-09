using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Balancy.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class CmsCallbackBoundaryTests
    {
        private sealed class TestModel : BaseModel
        {
            public bool ThrowOnInit { get; set; }

            public override void InitData()
            {
                if (ThrowOnInit)
                    throw new InvalidOperationException("CMS model init failed");
            }
        }

        private static readonly IDictionary AllModels = (IDictionary)typeof(CMS)
            .GetField("AllModels", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly IDictionary Subscriptions = (IDictionary)typeof(CMS)
            .GetField("AllConditionalTemplateSubscriptions", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
        private static readonly FieldInfo IsReady = typeof(CMS)
            .GetField("IsReadyToUse", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo SetModelData = typeof(BaseModel)
            .GetMethod("SetData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ModelRefreshed = typeof(Controller)
            .GetMethod("ModelRefreshed", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ConditionalChanged = typeof(CMS)
            .GetMethod("OnConditionalTemplateChangedStatic", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly Type SubscriptionType = typeof(CMS)
            .GetNestedType("ConditionalTemplateSubscription", BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            AllModels.Clear();
            Subscriptions.Clear();
            IsReady.SetValue(null, true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (DictionaryEntry entry in AllModels)
                ((BaseModel)entry.Value).SetData(IntPtr.Zero);
            AllModels.Clear();
            Subscriptions.Clear();
            IsReady.SetValue(null, false);
        }

        [Test]
        public void ThrowingModelInitCannotEscapeNativeRefreshCallback()
        {
            var model = CacheModel("model", new IntPtr(101));
            model.ThrowOnInit = true;
            LogAssert.Expect(LogType.Exception, new Regex("CMS model init failed"));

            Assert.DoesNotThrow(() => ModelRefreshed.Invoke(null,
                new object[] { "model", new IntPtr(102) }));
            Assert.That(model.Equals(new IntPtr(102)), Is.True);
        }

        [Test]
        public void ThrowingModelChangedSubscriberDoesNotSkipLaterSubscriberOrEscape()
        {
            var model = CacheModel("model", new IntPtr(201));
            var laterCalls = 0;
            model.OnChanged += () => throw new InvalidOperationException("CMS changed subscriber failed");
            model.OnChanged += () => laterCalls++;
            LogAssert.Expect(LogType.Exception, new Regex("CMS changed subscriber failed"));

            Assert.DoesNotThrow(() => ModelRefreshed.Invoke(null,
                new object[] { "model", new IntPtr(202) }));
            Assert.That(laterCalls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingConditionalSubscriberDoesNotSkipLaterSubscriberOrEscape()
        {
            CacheModel("document", new IntPtr(301));
            var laterCalls = 0;
            AddSubscription("first", "Tests.Template",
                (_, __) => throw new InvalidOperationException("conditional subscriber failed"));
            AddSubscription("second", "Tests.Template", (_, passed) =>
            {
                Assert.That(passed, Is.True);
                laterCalls++;
            });
            LogAssert.Expect(LogType.Exception, new Regex("conditional subscriber failed"));

            Assert.DoesNotThrow(() => ConditionalChanged.Invoke(null,
                new object[] { "Tests.Template", "document", true }));
            Assert.That(laterCalls, Is.EqualTo(1));
        }

        [Test]
        public void ConditionalSubscriberCanRemoveItselfDuringDispatch()
        {
            CacheModel("document", new IntPtr(401));
            var laterCalls = 0;
            AddSubscription("first", "Tests.Template", (_, __) => Subscriptions.Remove("first"));
            AddSubscription("second", "Tests.Template", (_, __) => laterCalls++);

            Assert.DoesNotThrow(() => ConditionalChanged.Invoke(null,
                new object[] { "Tests.Template", "document", true }));
            Assert.That(laterCalls, Is.EqualTo(1));
            Assert.That(Subscriptions.Contains("first"), Is.False);
        }

        private static TestModel CacheModel(string unnyId, IntPtr pointer)
        {
            var model = new TestModel();
            SetModelData.Invoke(model, new object[] { pointer, unnyId, "Tests.Template" });
            AllModels.Add(unnyId, model);
            return model;
        }

        private static void AddSubscription(string key, string templateName, Action<BaseModel, bool> callback)
        {
            var subscription = Activator.CreateInstance(SubscriptionType, true);
            SubscriptionType.GetField("TemplateName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(subscription, templateName);
            SubscriptionType.GetField("Callback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(subscription, callback);
            Subscriptions.Add(key, subscription);
        }
    }
}
