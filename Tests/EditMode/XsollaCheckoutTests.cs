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
