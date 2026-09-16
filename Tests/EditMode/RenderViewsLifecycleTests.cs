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

        private static Func<string> ReadScripts {
            get => (Func<string>)ManagerType.GetField("ReadScripts", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            set => ManagerType.GetField("ReadScripts", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }
        private static void DataUpdated(Callbacks.DataUpdatedStatus status) =>
            ManagerType.GetMethod("HandleContentUpdated", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { status });

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

        private static Array ParseRequests(string json)
        {
            return (Array)ManagerType.GetMethod("ParseBridgeRequests", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { json });
        }

        [Test]
        public void SingleAndBatchRequestsDeserializeWithoutRecursiveSerializationErrors()
        {
            var single = ParseRequests("{\"type\":\"request\",\"id\":\"a\",\"viewId\":\"view-a\",\"params\":{\"path\":\"image\"}}");
            Assert.That(single.Length, Is.EqualTo(1));
            Assert.That(single.GetValue(0).GetType().GetField("viewId").GetValue(single.GetValue(0)), Is.EqualTo("view-a"));
            var batch = ParseRequests("{\"type\":\"batch\",\"requests\":[{\"id\":\"a\",\"viewId\":\"view-a\"},{\"id\":\"b\",\"viewId\":\"view-a\"}]}");
            Assert.That(batch.Length, Is.EqualTo(2));
            Assert.That(batch.GetValue(1).GetType().GetField("id").GetValue(batch.GetValue(1)), Is.EqualTo("b"));
        }

        [TestCase("null")]
        [TestCase("{\"type\":\"batch\"}")]
        [TestCase("{\"type\":\"batch\",\"requests\":[]}")]
        [TestCase("{\"type\":\"batch\",\"requests\":[null]}")]
        [TestCase("{\"type\":\"batch\",\"requests\":[{\"type\":\"batch\",\"id\":\"a\"}]}")]
        public void MalformedBatchesAreRejectedBeforeNativeDispatch(string json)
        {
            var error = Assert.Throws<TargetInvocationException>(() => ParseRequests(json));
            Assert.That(error.InnerException is FormatException || error.InnerException is ArgumentException, Is.True);
            Assert.That(error.InnerException, Is.Not.InstanceOf<NullReferenceException>());
        }

        [Test]
        public void LegacyOwnerlessRequestsRemainSupported()
        {
            Assert.That(ParseRequests("{\"id\":\"legacy\",\"action\":10}").Length, Is.EqualTo(1));
            Assert.That(ParseRequests("{\"action\":10}").Length, Is.EqualTo(1));
        }

        [Test]
        public void BatchErrorsPreserveEveryCorrelationId()
        {
            var json = (string)ManagerType.GetMethod("RequestError", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "{\"type\":\"batch\",\"requests\":[{\"id\":\"a\"},{\"id\":\"b\"}]}", "closed" });
            StringAssert.Contains("\"batch-response\"", json);
            StringAssert.Contains("\"id\":\"a\"", json);
            StringAssert.Contains("\"id\":\"b\"", json);
        }

        [Test]
        public void NativeResponseCallbackIsStaticRootedAndAotCompatible()
        {
            var field = ManagerType.GetField("CoreResponseCallback", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field.IsInitOnly, Is.True);
            var weak = new WeakReference(field.GetValue(null));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var callback = (Delegate)weak.Target;
            Assert.That(callback, Is.Not.Null);
            Assert.That(callback.Target, Is.Null);
            Assert.That(callback.Method, Is.EqualTo(OnMessageResponseReceived));
            Assert.That(callback.Method.GetCustomAttributes(false).Any(a => a.GetType().Name == "MonoPInvokeCallbackAttribute"), Is.True);
            WebViewField.SetValue(null, null);
            Assert.DoesNotThrow(() => callback.DynamicInvoke("{\"id\":\"late\"}"));
        }

        [Test]
        public void PresentationDefaultsAreZeroAndPublicSettingsRemainConfigurable()
        {
            var type = typeof(BalancyWebView);
            var delay = type.GetField("_showDelay", BindingFlags.NonPublic | BindingFlags.Instance);
            var fade = type.GetField("_animationDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(delay.GetValue(_webView), Is.EqualTo(0f));
            Assert.That(fade.GetValue(_webView), Is.EqualTo(0f));
            WebViewField.SetValue(null, _webView);
            RenderViewsManager.SetViewDelays(0.2f, 0.3f);
            Assert.That(delay.GetValue(_webView), Is.EqualTo(0.2f));
            Assert.That(fade.GetValue(_webView), Is.EqualTo(0.3f));
            RenderViewsManager.SetViewDelays(-1f, -2f);
            Assert.That(delay.GetValue(_webView), Is.EqualTo(0f));
            Assert.That(fade.GetValue(_webView), Is.EqualTo(0f));
        }

        [Test]
        public void ScriptUpdatesRecreateShellAndNeverTravelInViewPayload()
        {
            string payload = null;
            var state = new PersistentViewState(message => { payload = message; return true; },
                () => {}, () => {}, () => {}, () => {}, _ => {}, () => 0);
            var type = typeof(BalancyWebView);
            type.GetField("_persistent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_webView, state);
            var onMessage = type.GetMethod("OnMessageReceivedPrivate", BindingFlags.Instance | BindingFlags.NonPublic);
            var versionField = type.GetField("_scriptsVersion", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<string> receive = message => onMessage.Invoke(_webView, new object[] { message });
            try
            {
                _webView.SetScriptsCode("bundle-one");
                var version = (string)versionField.GetValue(_webView);
                state.Prepare(() => {
                    type.GetField("_shellScriptsVersion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_webView, versionField.GetValue(_webView));
                    return true;
                }, null, null);
                receive("{\"type\":\"shellReady\",\"shellId\":\"" + state.ShellId + "\",\"scriptsVersion\":\"" + version + "\"}");
                _webView.ShowView("<div>A</div>", "", ""); state.Tick();
                StringAssert.DoesNotContain("scriptsBase64", payload);
                StringAssert.Contains(version, payload);
                state.Close(); state.Receive("viewCleared", state.ClosingId, null, null);
                _webView.SetScriptsCode("bundle-one");
                Assert.That(versionField.GetValue(_webView), Is.EqualTo(version));
                _webView.SetScriptsCode("bundle-two");
                var next = (string)versionField.GetValue(_webView);
                _webView.ShowView("<div>B</div>", "", ""); state.Tick();
                Assert.That(state.Preparing, Is.True);
                Assert.That(payload, Does.Not.Contain("bundle-two"));
                receive("{\"type\":\"shellReady\",\"shellId\":\"" + state.ShellId + "\",\"scriptsVersion\":\"" + next + "\"}");
                StringAssert.DoesNotContain("scriptsBase64", payload);
                StringAssert.Contains(next, payload);
                receive("{\"type\":\"viewReady\",\"viewId\":\"stale\",\"scriptsVersion\":\"" + next + "\"}");
                Assert.That(type.GetField("_acknowledgedScriptsVersion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_webView), Is.EqualTo(next));
                receive("{\"type\":\"viewReady\",\"viewId\":\"" + state.CurrentId + "\",\"scriptsVersion\":\"" + next + "\"}");
                state.Close(); state.Receive("viewCleared", state.ClosingId, null, null);
                _webView.ShowView("<div>C</div>", "", ""); state.Tick();
                StringAssert.DoesNotContain("scriptsBase64", payload);
            }
            finally { state.Reset(); }
        }

        [Test]
        public void PrepareBeforeInitializationRecordsIntentWithoutReadingNativeCode()
        {
            var read = ReadScripts;
            int reads = 0;
            ReadScripts = () => { reads++; return "code"; };
            try {
                RenderViewsManager.PrepareWebView(); RenderViewsManager.PrepareWebView();
                Assert.That(reads, Is.Zero);
                Assert.That(ManagerType.GetField("_prepareRequested", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.True);
                CleanUpManagedState.Invoke(null, null);
                Assert.That(ManagerType.GetField("_prepareRequested", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.False);
            } finally { ReadScripts = read; }
        }

        [Test]
        public void DataUpdateWithoutOptInDoesNotReadOrPrepareScripts()
        {
            var read = ReadScripts;
            int reads = 0;
            ReadScripts = () => { reads++; return "code"; };
            WebViewField.SetValue(null, _webView);
            try {
                DataUpdated(new Callbacks.DataUpdatedStatus(false, true, true));
                DataUpdated(new Callbacks.DataUpdatedStatus(true, true, true));
                Assert.That(reads, Is.Zero);
                Assert.That(_webView.IsPersistentModeEnabled(), Is.False);
            } finally { ReadScripts = read; }
        }

        [Test]
        public void SameScriptUpdatePreservesShellAndReadsOnlyOnDataUpdate()
        {
            var read = ReadScripts;
            int reads = 0, destroyed = 0;
            var state = new PersistentViewState(_ => true, () => {}, () => {}, () => destroyed++, () => {}, _ => {}, () => 0);
            typeof(BalancyWebView).GetField("_persistent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_webView, state);
            _webView.SetScriptsCode("code");
            state.Prepare(() => true, null, null); state.Receive("shellReady", null, state.ShellId, null);
            WebViewField.SetValue(null, _webView);
            ReadScripts = () => { reads++; return "code"; };
            try {
                int ready = 0;
                RenderViewsManager.PrepareWebView(() => ready++);
                Assert.That(reads, Is.Zero); Assert.That(ready, Is.Zero);
                DataUpdated(new Callbacks.DataUpdatedStatus(false, false, true));
                Assert.That(reads, Is.EqualTo(1)); Assert.That(ready, Is.EqualTo(1));
                RenderViewsManager.PrepareWebView(() => ready++);
                Assert.That(reads, Is.EqualTo(1)); Assert.That(ready, Is.EqualTo(2));
                DataUpdated(new Callbacks.DataUpdatedStatus(true, false, true));
                state.Tick(); Assert.That(reads, Is.EqualTo(2)); Assert.That(destroyed, Is.Zero);
            } finally { state.Reset(); ReadScripts = read; }
        }

        [Test]
        public void FailedReadReportsFailureAndRetainsPrepareIntentForNextUpdate()
        {
            var read = ReadScripts;
            int failures = 0, reads = 0;
            WebViewField.SetValue(null, _webView);
            ReadScripts = () => { reads++; throw new InvalidOperationException("test unavailable"); };
            try {
                RenderViewsManager.PrepareWebView(null, error => failures++);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[RenderViewsManager] Failed to compile scripts, keeping previous bundle: test unavailable");
                DataUpdated(new Callbacks.DataUpdatedStatus(false, false, true));
                Assert.That(reads, Is.EqualTo(1)); Assert.That(failures, Is.EqualTo(1));
                Assert.That(_webView.IsPersistentModeEnabled(), Is.False);
                Assert.That(ManagerType.GetField("_prepareRequested", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.True);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[RenderViewsManager] Failed to compile scripts, keeping previous bundle: test unavailable");
                DataUpdated(new Callbacks.DataUpdatedStatus(true, false, true));
                Assert.That(reads, Is.EqualTo(2)); Assert.That(failures, Is.EqualTo(1));
            } finally { ReadScripts = read; }
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
