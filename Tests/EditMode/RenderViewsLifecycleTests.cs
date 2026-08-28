using System;
using System.Linq;
using System.Reflection;
using Balancy.WebView;
using NUnit.Framework;
using UnityEngine;

namespace Balancy.Tests
{
    public class RenderViewsLifecycleTests
    {
        private static readonly Type ManagerType = typeof(RenderViewsManager);
        private static readonly FieldInfo WebViewField = ManagerType
            .GetField("_webView", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MessageHandler = ManagerType
            .GetField("_onMessageReceived", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo LastOwner = ManagerType
            .GetField("m_LastOpenedOwnerPtr", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo CleanUpManagedState = ManagerType
            .GetMethod("CleanUpManagedState", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo OnMessageReceived = ManagerType
            .GetMethod("OnMessageReceived", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo HandleLoadCompleted = ManagerType
            .GetMethod("HandleLoadCompleted", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo HandleWebViewClosed = ManagerType
            .GetMethod("HandleWebViewClosed", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo OnNotificationReceived = ManagerType
            .GetMethod("OnNotificationReceived", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo OnMessageResponseReceived = ManagerType
            .GetMethod("OnMessageResponseReceived", BindingFlags.Static | BindingFlags.NonPublic);

        private GameObject _gameObject;
        private BalancyWebView _webView;

        [SetUp]
        public void SetUp()
        {
            CleanUpManagedState.Invoke(null, null);
            _gameObject = new GameObject("Balancy render views lifecycle test");
            _webView = _gameObject.AddComponent<BalancyWebView>();
        }

        [TearDown]
        public void TearDown()
        {
            CleanUpManagedState.Invoke(null, null);
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void CleanupRemovesSdkHandlersAndResetsSessionState()
        {
            var externalMessageCalls = 0;
            var externalLoadCalls = 0;
            var externalCloseCalls = 0;
            Action<string> sdkMessage = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), OnMessageReceived);
            Action<bool> sdkLoad = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), HandleLoadCompleted);
            Action sdkClose = (Action)Delegate.CreateDelegate(typeof(Action), HandleWebViewClosed);
            _webView.OnMessage = sdkMessage;
            _webView.OnMessage += _ => externalMessageCalls++;
            _webView.OnLoadCompleted += sdkLoad;
            _webView.OnLoadCompleted += _ => externalLoadCalls++;
            _webView.OnClosed += sdkClose;
            _webView.OnClosed += () => externalCloseCalls++;
            WebViewField.SetValue(null, _webView);
            MessageHandler.SetValue(null, new Func<string, bool>(_ => true));
            LastOwner.SetValue(null, new IntPtr(901));

            CleanUpManagedState.Invoke(null, null);

            Assert.That(WebViewField.GetValue(null), Is.Null);
            Assert.That(MessageHandler.GetValue(null), Is.Null);
            Assert.That((IntPtr)LastOwner.GetValue(null), Is.EqualTo(IntPtr.Zero));
            Assert.That(_webView.OnMessage.GetInvocationList().Contains((Delegate)sdkMessage), Is.False);
            _webView.OnMessage("message");
            InvokeEventBackingField(_webView, "OnLoadCompleted", true);
            InvokeEventBackingField(_webView, "OnClosed");
            Assert.That(externalMessageCalls, Is.EqualTo(1));
            Assert.That(externalLoadCalls, Is.EqualTo(1));
            Assert.That(externalCloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void LateNativeViewMessagesAreIgnoredAfterCleanup()
        {
            WebViewField.SetValue(null, null);

            Assert.DoesNotThrow(() => OnNotificationReceived.Invoke(null, new object[] { "notification" }));
            Assert.DoesNotThrow(() => OnMessageResponseReceived.Invoke(null, new object[] { "response" }));
        }

        private static void InvokeEventBackingField(object owner, string fieldName, params object[] arguments)
        {
            var callback = (Delegate)owner.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(owner);
            callback?.DynamicInvoke(arguments);
        }
    }
}
