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
        [TestCase("http://sandbox-secure.xsolla.com/paystation4/")]
        [TestCase("https://secure.xsolla.com/paystation4/")]
        [TestCase("https://evil.com/paystation4/")]
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
        [Test] public void CheckoutAllowsSandboxPayStation()
        {
            var method = Adapter.GetMethod("ValidateCheckoutUrl", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { "https://sandbox-secure.xsolla.com/paystation4/?token=fixture" }));
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
