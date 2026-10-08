using System;
using System.Reflection;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class XsollaCheckoutTests
    {
        private static Type Adapter => typeof(API).Assembly.GetType("Balancy.XsollaPurchasing", true);
        [TearDown] public void Cleanup() => API.Xsolla.Configure(null);

        [Test] public void ConfigurationDoesNotAcceptMissingOrInvalidCountry()
        {
            Assert.Throws<ArgumentException>(() => API.Xsolla.Configure(new XsollaOptions { Enabled = true }));
            Assert.Throws<ArgumentException>(() => API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "us" }));
            Assert.Throws<ArgumentException>(() => API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", Language = "arbitrary" }));
            Assert.Throws<ArgumentException>(() => API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", NativeLabel = new string('x', 81) }));
        }
        [Test] public void DisabledIntegrationDoesNotCallNewNativeExports()
        {
            API.Xsolla.Configure(null);
            Assert.That(API.Xsolla.RecoverPurchases().IsCompleted, Is.True);
            var intercept = Adapter.GetMethod("TryIntercept", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(intercept.Invoke(null, new object[] { new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null), null, null }), Is.False);
        }
        [TestCase("http://sandbox-secure.xsolla.com/paystation4/?token=fixture")]
        [TestCase("http://secure.xsolla.com/paystation4/?token=fixture")]
        [TestCase("https://secure.xsolla.com.evil.com/paystation4/?token=fixture")]
        [TestCase("https://sandbox-secure.xsolla.com.evil.com/paystation4/?token=fixture")]
        [TestCase("https://secure-xsolla.com/paystation4/?token=fixture")]
        [TestCase("https://secure.xsolla.com:8443/paystation4/?token=fixture")]
        [TestCase("https://user@secure.xsolla.com/paystation4/?token=fixture")]
        [TestCase("https://secure.xsolla.com/wrong-path/?token=fixture")]
        [TestCase("https://secure.xsolla.com/paystation4/?token=fixture#fragment")]
        [TestCase("https://secure.xsolla.com/paystation4/")]
        [TestCase("https://evil.com/paystation4/?token=fixture")]
        [TestCase("https://sandbox-secure.xsolla.com/paystation4/")]
        [TestCase("https://sandbox-secure.xsolla.com/paystation4/?token=")]
        [TestCase("https://sandbox-secure.xsolla.com/paystation4/?token=a#fragment")]
        [TestCase("https://user@sandbox-secure.xsolla.com/paystation4/")]
        [TestCase("https://sandbox-secure.xsolla.com:8443/paystation4/")]
        public void CheckoutRejectsUntrustedUrls(string url)
        {
            var method = Adapter.GetMethod("ValidateCheckoutUrl", BindingFlags.Static | BindingFlags.NonPublic);
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { url }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }
        [TestCase("https://sandbox-secure.xsolla.com/paystation4/?token=fixture")]
        [TestCase("https://secure.xsolla.com/paystation4/?token=fixture")]
        public void CheckoutAllowsOfficialPayStation(string url)
        {
            var method = Adapter.GetMethod("ValidateCheckoutUrl", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { url }));
        }
        [TestCase("{}")]
        [TestCase("null")]
        [TestCase("not-json")]
        [TestCase("{\"acknowledged\":false,\"grantId\":\"grant\"}")]
        [TestCase("{\"acknowledged\":true,\"grantId\":\"other\"}")]
        [TestCase("{\"acknowledged\":\"true\",\"grantId\":\"grant\"}")]
        [TestCase("{\"acknowledged\":1,\"grantId\":\"grant\"}")]
        [TestCase("{\"acknowledged\":true,\"acknowledged\":\"true\",\"grantId\":\"grant\"}")]
        [TestCase("{\"nested\":{\"x\":0,\"acknowledged\":true},\"acknowledged\":\"true\",\"grantId\":\"grant\"}")]
        [TestCase("{\"acknowledged\":true,\"grantId\":\"grant\",\"grantId\":\"grant\"}")]
        [TestCase("{\"nested\":{\"acknowledged\":true},\"acknowledged\":false,\"grantId\":\"grant\"}")]
        [TestCase("{\"acknowledged\":true}")]
        [TestCase("{\"error\":\"xsolla_reconciliation_required\"}")]
        public void AcknowledgmentMustConfirmExactGrant(string json)
        {
            var method = Adapter.GetMethod("ValidateAcknowledgment", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { json, "grant" }));
        }
        [TestCase("{\"acknowledged\":true,\"grantId\":\"grant\"}")]
        [TestCase(" \t{ \"grantId\": \"grant\", \"acknowledged\": true }\r\n")]
        public void AcknowledgmentAcceptsConfirmedExactGrant(string json)
        {
            var method = Adapter.GetMethod("ValidateAcknowledgment", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { json, "grant" }));
        }
        [Test] public void ShowSerializesUnavailableOnlyForNativeOnlySelector()
        {
            var normalType = Adapter.GetNestedType("PresentationShow", BindingFlags.NonPublic);
            var normal = Activator.CreateInstance(normalType);
            normalType.GetField("requestId").SetValue(normal, "00000000-0000-4000-8000-000000000001");
            var normalJson = UnityEngine.JsonUtility.ToJson(normal);
            Assert.That(normalJson, Does.Not.Contain("xsollaUnavailable"));
            var unavailableType = Adapter.GetNestedType("UnavailablePresentationShow", BindingFlags.NonPublic);
            var unavailable = Activator.CreateInstance(unavailableType);
            unavailableType.GetField("requestId").SetValue(unavailable, "00000000-0000-4000-8000-000000000002");
            unavailableType.GetField("xsollaUnavailable").SetValue(unavailable, "Please use your regular payment method.");
            var unavailableJson = UnityEngine.JsonUtility.ToJson(unavailable);
            Assert.That(unavailableJson, Does.Contain("xsollaUnavailable"));
            Assert.That(unavailableJson, Does.Contain("00000000-0000-4000-8000-000000000002"));
        }
        [Test] public void ContextOmitsEmptyOptionalFieldsAndShopDescriptorUsesSlotFallback()
        {
            var contextType = Adapter.GetNestedType("Context", BindingFlags.NonPublic);
            var context = UnityEngine.JsonUtility.FromJson("{\"type\":\"ShopSlot\",\"storeItemUnnyId\":\"doc\",\"slotUnnyId\":\"slot\"}", contextType);
            // Unity's absent-string behavior is recorded through its actual serializer here.
            var absent = (string)contextType.GetField("offerUnnyId").GetValue(context);
            Assert.That(string.IsNullOrEmpty(absent), Is.True);
            TestContext.WriteLine("Unity JsonUtility absent offerUnnyId = " + (absent == null ? "null" : "empty string"));
            foreach (var name in new[] { "offerUnnyId", "offerInstanceId" }) contextType.GetField(name).SetValue(context, "");
            var serialize = Adapter.GetMethod("ContextJson", BindingFlags.Static | BindingFlags.NonPublic);
            var json = (string)serialize.Invoke(null, new object[] { context });
            Assert.That(json, Does.Not.Contain("offerUnnyId"));
            Assert.That(json, Does.Not.Contain("offerInstanceId"));
            Assert.That(json, Does.Contain("slotUnnyId"));
            var orderType = Adapter.GetNestedType("Order", BindingFlags.NonPublic);
            var order = Activator.CreateInstance(orderType); orderType.GetField("context").SetValue(order, context);
            var descriptor = (Actions.BalancyProductInfo)Adapter.GetMethod("Descriptor", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { order });
            Assert.That(descriptor.OfferUnnyId, Is.EqualTo("slot"));
        }
        [Serializable] private class ChoiceFixtureMessage { public string type, requestId, xsollaUnavailable; }
        private sealed class ChoiceFixtureLease : IDisposable { public void Dispose() { } }
        [TestCase(true)]
        [TestCase(false)]
        public void MissingXsollaExportsKeepNativeAndCancelUsable(bool missingPrepare)
        {
            var names = new[] { "Prepare", "SessionIdentity", "Present", "Send" };
            var originals = new object[names.Length];
            for (int i = 0; i < names.Length; i++) originals[i] = Adapter.GetField(names[i], BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int identityCalls = 0, nativeCalls = 0, callbacks = 0;
            string requestId = null, unavailable = null;
            void Set(string name, object value) => Adapter.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
            void Receive(string choice, string id = null) => Adapter.GetMethod("ReceivePresentationMessage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { "{\"type\":\"paymentChoiceSelected\",\"requestId\":\"" + (id ?? requestId) + "\",\"choice\":\"" + choice + "\"}" });
            void Start() => Adapter.GetMethod("TryIntercept", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {
                new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null), (Action<bool, string>)((ok, error) => callbacks++), (Action)(() => nativeCalls++) });
            try
            {
                Set("Prepare", (Func<Actions.BalancyProductInfo, string>)(_ => missingPrepare ? throw new EntryPointNotFoundException("prepare") :
                    "{\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"sku\",\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"doc\"}}"));
                Set("SessionIdentity", (Func<string>)(() => { identityCalls++; throw new EntryPointNotFoundException("identity"); }));
                Set("Present", (Func<Action, Action<string>, IDisposable>)((ready, failed) => { ready(); return new ChoiceFixtureLease(); }));
                Set("Send", (Action<string>)(json => {
                    var message = UnityEngine.JsonUtility.FromJson<ChoiceFixtureMessage>(json);
                    if (message.type != "paymentChoiceShow") return;
                    requestId = message.requestId; unavailable = message.xsollaUnavailable;
                    Adapter.GetMethod("ReceivePresentationMessage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { "{\"type\":\"paymentChoiceReady\",\"requestId\":\"" + requestId + "\"}" });
                }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US" });
                Start(); int capturedCalls = identityCalls;
                Assert.That(Guid.TryParse(requestId, out _), Is.True);
                Assert.That(unavailable, Is.Not.Empty);
                Receive("xsolla"); Receive("native");
                Assert.That(nativeCalls, Is.EqualTo(1)); Assert.That(callbacks, Is.Zero);
                Assert.That(identityCalls, Is.EqualTo(capturedCalls));
                Start(); Receive("cancel");
                Assert.That(callbacks, Is.EqualTo(1)); Assert.That(nativeCalls, Is.EqualTo(1));
                Assert.That(identityCalls, Is.EqualTo(capturedCalls));
                Start(); var staleId = requestId;
                Adapter.GetMethod("DataUpdated", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false, true });
                Receive("native", staleId);
                Assert.That(nativeCalls, Is.EqualTo(1)); Assert.That(callbacks, Is.EqualTo(2));
                Assert.That(identityCalls, Is.EqualTo(capturedCalls));
                Start(); staleId = requestId; API.Xsolla.Configure(null); Receive("native", staleId);
                Assert.That(nativeCalls, Is.EqualTo(1)); Assert.That(callbacks, Is.EqualTo(3));
                Assert.That(identityCalls, Is.EqualTo(capturedCalls));
            }
            finally
            {
                API.Xsolla.Configure(null);
                for (int i = 0; i < names.Length; i++) Set(names[i], originals[i]);
            }
        }
        [Test] public void CloudProfileFailureMarshalMatchesActualNativeHeaderLayout()
        {
            // Actual Notifications.h probe: non-polymorphic int32 type + aligned char* message.
            var type = typeof(API).Assembly.GetType("Balancy.Core.Notifications+InitNotificationCloudProfileFailed", true);
            int messageOffset = (sizeof(int) + IntPtr.Size - 1) / IntPtr.Size * IntPtr.Size;
            Assert.That(System.Runtime.InteropServices.Marshal.OffsetOf(type, "Type").ToInt32(), Is.Zero);
            Assert.That(System.Runtime.InteropServices.Marshal.OffsetOf(type, "Message").ToInt32(), Is.EqualTo(messageOffset));
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf(type), Is.EqualTo(messageOffset + IntPtr.Size));
            var message = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi("Profile load failed abi-profile, I'll retry in 4 seconds");
            var notification = System.Runtime.InteropServices.Marshal.AllocHGlobal(messageOffset + IntPtr.Size);
            try
            {
                System.Runtime.InteropServices.Marshal.WriteInt32(notification, 0, 3);
                System.Runtime.InteropServices.Marshal.WriteIntPtr(notification, messageOffset, message);
                var decoded = System.Runtime.InteropServices.Marshal.PtrToStructure(notification, type);
                var kindType = typeof(API).Assembly.GetType("Balancy.Core.Notifications+NotificationBase", true);
                var kind = System.Runtime.InteropServices.Marshal.PtrToStructure(notification, kindType);
                Assert.That(Convert.ToInt32(kindType.GetField("Type").GetValue(kind)), Is.EqualTo(3));
                var baseType = typeof(API).Assembly.GetType("Balancy.Core.Notifications+InitNotificationError", true);
                var baseDecoded = System.Runtime.InteropServices.Marshal.PtrToStructure(notification, baseType);
                TestContext.WriteLine("CloudProfileFailed derived Message=" + (type.GetField("Message").GetValue(decoded) ?? "null") + "; InitNotificationError Message=" + (baseType.GetField("Message").GetValue(baseDecoded) ?? "null"));
                Assert.That(type.GetField("Message").GetValue(decoded), Is.EqualTo("Profile load failed abi-profile, I'll retry in 4 seconds"));
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(notification);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(message);
            }
        }
        [Test] public void CallbackUsesOneByteNativeBooleanAndStableAbi()
        {
            var methods = typeof(API).Assembly.GetType("Balancy.LibraryMethods+API", true);
            var callback = methods.GetNestedType("XsollaCallback", BindingFlags.NonPublic);
            var parameter = callback.GetMethod("Invoke").GetParameters()[1];
            var marshal = parameter.GetCustomAttribute<System.Runtime.InteropServices.MarshalAsAttribute>();
            Assert.That(marshal.Value, Is.EqualTo(System.Runtime.InteropServices.UnmanagedType.I1));
        }
    }
}
