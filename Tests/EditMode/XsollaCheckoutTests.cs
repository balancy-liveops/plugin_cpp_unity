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
            Assert.Throws<ArgumentException>(() => API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true, Language = "arbitrary" }));
            Assert.Throws<ArgumentException>(() => API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true, NativeLabel = new string('x', 81) }));
        }
        [Test] public void DisabledIntegrationDoesNotCallNewNativeExports()
        {
            API.Xsolla.Configure(null);
            Assert.That(API.Xsolla.RecoverPurchases().IsCompleted, Is.True);
            var intercept = Adapter.GetMethod("TryIntercept", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(intercept.Invoke(null, new object[] { new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null), null, null }), Is.False);
        }
        [TestCase("Payments.AlternativePaymentRule", typeof(Models.Payments.AlternativePaymentRule))]
        [TestCase("Payments.PurchaseBonus", typeof(Models.Payments.PurchaseBonus))]
        [TestCase("Payments.PurchaseBonusFixedReward", typeof(Models.Payments.PurchaseBonusFixedReward))]
        [TestCase("Payments.PurchaseBonusPerSpend", typeof(Models.Payments.PurchaseBonusPerSpend))]
        public void PaymentModelsAreRegisteredByCanonicalTemplateName(string name, Type expected)
        {
            var original = CMS.OnTypeRequested;
            try
            {
                CMS.OnTypeRequested = null;
                var instance = typeof(CMS).GetMethod("InstantiateByType", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { name });
                Assert.That(instance, Is.TypeOf(expected));
                Assert.That(typeof(Models.JsonBasedObject).GetMethod("GetModelClassName").MakeGenericMethod(expected).Invoke(null, null), Is.EqualTo(name));
            }
            finally { CMS.OnTypeRequested = original; }
        }
        [Test] public void AlternativeRuleUsesInheritedConditionAndPriorityAndPolymorphicBonus()
        {
            var rule = typeof(Models.Payments.AlternativePaymentRule);
            Assert.That(rule.BaseType, Is.EqualTo(typeof(Models.SmartObjects.ConditionalTemplate)));
            Assert.That(rule.GetProperty("Condition").DeclaringType, Is.EqualTo(rule.BaseType));
            Assert.That(rule.GetProperty("Priority").DeclaringType, Is.EqualTo(rule.BaseType));
            Assert.That(rule.GetProperty("IncludedProducts").PropertyType, Is.EqualTo(typeof(Models.UnnyProduct[])));
            Assert.That(rule.GetProperty("Bonus").PropertyType, Is.EqualTo(typeof(Models.Payments.PurchaseBonus)));
            Assert.That(rule.GetProperty("ChoiceView").PropertyType, Is.EqualTo(typeof(Models.UnnyObject)));
            Assert.That(rule.GetProperty("Enabled"), Is.Null); Assert.That(rule.GetProperty("Platforms"), Is.Null);
            Assert.That((int)Models.Payments.AlternativePaymentProvider.Xsolla, Is.EqualTo(50));
            Assert.That((int)Models.Payments.AlternativeProductSelection.IncludeOnly, Is.EqualTo(1));
        }
        [TestCase(Actions.BalancyProductInfo.PurchaseType.StoreItem, false)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.ShopSlot, false)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.Offer, false)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.OfferGroup, false)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.StoreItem, true)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.ShopSlot, true)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.Offer, true)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.OfferGroup, true)]
        public void EveryHardPurchaseKindPreservesNativeWhenHostOrCurrentRuleUnavailable(Actions.BalancyProductInfo.PurchaseType kind, bool hostAllowed)
        {
            var prepareField = Adapter.GetField("Prepare", BindingFlags.Static | BindingFlags.NonPublic);
            var previousPrepare = prepareField.GetValue(null);
            var providerField = typeof(Actions.Purchasing).GetField("_hardPurchaseCallback", BindingFlags.Static | BindingFlags.NonPublic);
            var previousProvider = (Actions.Purchasing.HardPurchaseCallback)providerField.GetValue(null);
            int preparations = 0, providerCalls = 0, callbacks = 0;
            try
            {
                prepareField.SetValue(null, (Func<Actions.BalancyProductInfo, string>)(_ => { preparations++; return "{\"eligible\":false,\"reason\":\"no_rule\"}"; }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = hostAllowed ? () => true : (Func<bool>)null });
                Actions.Purchasing.SetHardPurchaseCallback(_ => providerCalls++);
                var product = new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null) { Type = kind };
                typeof(API).GetMethod("HardPurchase", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { product, (Action<bool, string>)((ok, error) => callbacks++) });
                Assert.That(providerCalls, Is.EqualTo(1)); Assert.That(callbacks, Is.Zero);
                Assert.That(preparations, Is.EqualTo(hostAllowed ? 1 : 0));
                Assert.That(Adapter.GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
            }
            finally
            {
                API.Xsolla.Configure(null);
                typeof(API).GetMethod("CleanUpPendingPurchaseCallbacks", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Actions.Purchasing.SetHardPurchaseCallback(previousProvider); prepareField.SetValue(null, previousPrepare);
            }
        }
        [TestCase("request")]
        [TestCase("requested")]
        [TestCase("canonical")]
        [TestCase("rule")]
        [TestCase("version")]
        [TestCase("price")]
        [TestCase("currency")]
        [TestCase("count")]
        public void QuotePresentationRejectsUncorrelatedOrInvalidAuthoritativeData(string invalid)
        {
            var preparedType = Adapter.GetNestedType("Prepared", BindingFlags.NonPublic);
            var quoteType = Adapter.GetNestedType("PriceQuote", BindingFlags.NonPublic);
            var prepared = UnityEngine.JsonUtility.FromJson("{\"eligible\":true,\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"requested\",\"baseProductId\":\"canonical\",\"ruleId\":\"rule\",\"configVersion\":\"v1\"}", preparedType);
            var quote = UnityEngine.JsonUtility.FromJson("{\"quoteId\":\"00000000-0000-4000-8000-000000000099\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"canonical\",\"requestedProductId\":\"requested\",\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"priceUsd\":\"4.99\",\"currency\":\"USD\",\"bonus\":[{\"itemUnnyId\":\"booster\",\"count\":1}]}", quoteType);
            var nameField = Adapter.GetField("BonusName", BindingFlags.Static | BindingFlags.NonPublic); var originalName = nameField.GetValue(null);
            try
            {
                nameField.SetValue(null, (Func<string, string>)(_ => "Localized booster"));
                var method = Adapter.GetMethod("QuotePresentation", BindingFlags.Static | BindingFlags.NonPublic);
                var presentation = method.Invoke(null, new[] { quote, prepared });
                var price = presentation.GetType().GetField("price").GetValue(presentation);
                Assert.That(price.GetType().GetField("amount").GetValue(price), Is.EqualTo("4.99"));
                var bonus = (Array)presentation.GetType().GetField("bonus").GetValue(presentation);
                Assert.That(bonus.GetValue(0).GetType().GetField("name").GetValue(bonus.GetValue(0)), Is.EqualTo("Localized booster"));
                if (invalid == "count")
                {
                    var entries = (Array)quoteType.GetField("bonus").GetValue(quote);
                    entries.GetValue(0).GetType().GetField("count").SetValue(entries.GetValue(0), 0);
                }
                else
                {
                    var field = invalid == "request" ? "requestId" : invalid == "requested" ? "requestedProductId" : invalid == "canonical" ? "productId" : invalid == "rule" ? "ruleId" : invalid == "version" ? "configVersion" : invalid == "price" ? "priceUsd" : "currency";
                    quoteType.GetField(field).SetValue(quote, invalid == "price" ? "NaN" : "wrong");
                }
                Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new[] { quote, prepared }));
            }
            finally { nameField.SetValue(null, originalName); }
        }
        [Test] public void CapabilityApiUsesOneByteNativeBoolean()
        {
            var native = typeof(API).Assembly.GetType("Balancy.LibraryMethods+API", true).GetMethod("balancyXsollaPreparePurchaseWithCapability", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(native.GetParameters()[3].GetCustomAttribute<System.Runtime.InteropServices.MarshalAsAttribute>().Value,
                Is.EqualTo(System.Runtime.InteropServices.UnmanagedType.I1));
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
        private sealed class ChoiceFixtureLease : IDisposable { private readonly Action closed; internal ChoiceFixtureLease(Action closed = null) { this.closed = closed; } public void Dispose() { closed?.Invoke(); } }
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
                    "{\"eligible\":true,\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"baseProductId\":\"sku\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"sku\",\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"doc\"}}"));
                Set("SessionIdentity", (Func<string>)(() => { identityCalls++; throw new EntryPointNotFoundException("identity"); }));
                Set("Present", (Func<Action, Action<string>, IDisposable>)((ready, failed) => { ready(); return new ChoiceFixtureLease(); }));
                Set("Send", (Action<string>)(json => {
                    var message = UnityEngine.JsonUtility.FromJson<ChoiceFixtureMessage>(json);
                    if (message.type != "paymentChoiceShow") return;
                    requestId = message.requestId; unavailable = message.xsollaUnavailable;
                    Adapter.GetMethod("ReceivePresentationMessage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { "{\"type\":\"paymentChoiceReady\",\"requestId\":\"" + requestId + "\"}" });
                }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true });
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
        [TestCase(false)]
        [TestCase(true)]
        public void ExpiredChooserNeverAttemptsCheckoutAndDurablyDiscards(bool expiresDuringFlush)
        {
            var names = new[] { "Prepare", "SessionIdentity", "Present", "Send", "Request", "Persist", "UtcNow", "OpenCheckout", "BrowserPopup" };
            var originals = new object[names.Length];
            for (int i = 0; i < names.Length; i++) originals[i] = Adapter.GetField(names[i], BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            void Set(string name, object value) => Adapter.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
            var now = DateTimeOffset.Parse("2098-12-31T23:59:59Z");
            int discarded = 0, flushed = 0, checkoutRequests = 0, opened = 0, callbacks = 0, nativeCalls = 0, disposals = 0;
            bool expirationArmed = false; string id = null, failure = null;
            try
            {
                Set("UtcNow", (Func<DateTimeOffset>)(() => now)); Set("BrowserPopup", false);
                Set("Prepare", (Func<Actions.BalancyProductInfo, string>)(_ => "{\"eligible\":true,\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"baseProductId\":\"sku\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"sku\",\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"doc\"}}"));
                Set("SessionIdentity", (Func<string>)(() => "expiry-player"));
                Set("Present", (Func<Action, Action<string>, IDisposable>)((ready, failed) => { ready(); return new ChoiceFixtureLease(() => { disposals++; failed("host closed during cleanup"); }); }));
                Set("Send", (Action<string>)(json => {
                    var message = UnityEngine.JsonUtility.FromJson<ChoiceFixtureMessage>(json);
                    if (message.type != "paymentChoiceShow") return;
                    id = message.requestId;
                    Adapter.GetMethod("ReceivePresentationMessage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { "{\"type\":\"paymentChoiceReady\",\"requestId\":\"" + id + "\"}" });
                }));
                Set("OpenCheckout", (Action<string>)(_ => opened++));
                Set("Persist", (Func<System.Threading.Tasks.Task>)(() => { flushed++; if (expirationArmed) now = DateTimeOffset.Parse("2099-01-01T00:00:00Z"); return System.Threading.Tasks.Task.CompletedTask; }));
                Set("Request", (Func<string, string, System.Threading.Tasks.Task<string>>)((action, body) => {
                    if (action == "quote") return System.Threading.Tasks.Task.FromResult("{\"quote\":{\"quoteId\":\"00000000-0000-4000-8000-000000000099\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"sku\",\"requestedProductId\":\"sku\",\"ruleId\":\"rule\",\"configVersion\":\"v1\",\"priceUsd\":\"4.99\",\"currency\":\"USD\",\"expiresAt\":\"2099-01-01T00:00:00Z\",\"bonus\":[]}}");
                    if (action == "discard") discarded++;
                    if (action == "checkout") checkoutRequests++;
                    return System.Threading.Tasks.Task.FromResult("{}");
                }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true });
                Adapter.GetMethod("TryIntercept", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {
                    new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null), (Action<bool, string>)((ok, error) => { Assert.That(ok, Is.False); callbacks++; failure = error; }), (Action)(() => nativeCalls++) });
                Assert.That(id, Is.Not.Null); int initialFlushes = flushed;
                expirationArmed = expiresDuringFlush;
                if (!expiresDuringFlush) now = DateTimeOffset.Parse("2099-01-01T00:00:00Z");
                Adapter.GetMethod("ReceivePresentationMessage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { "{\"type\":\"paymentChoiceSelected\",\"requestId\":\"" + id + "\",\"choice\":\"xsolla\"}" });
                Assert.That(checkoutRequests, Is.Zero); Assert.That(opened, Is.Zero); Assert.That(nativeCalls, Is.Zero);
                Assert.That(disposals, Is.EqualTo(1)); Assert.That(discarded, Is.EqualTo(1)); Assert.That(flushed, Is.GreaterThan(initialFlushes));
                Assert.That(callbacks, Is.EqualTo(1)); Assert.That(failure, Is.EqualTo("Offer expired; start the purchase again"));
                Assert.That(Adapter.GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
            }
            finally { API.Xsolla.Configure(null); for (int i = 0; i < names.Length; i++) Set(names[i], originals[i]); }
        }
        [TestCase("canceled")]
        [TestCase("reconciliation")]
        [TestCase("listed-canceled")]
        public void TerminalDeferredOrdersDoNotStarveHealthyPendingAndReadbackRotates(string terminal)
        {
            var names = new[] { "SessionIdentity", "Request", "Complete", "Persist" };
            var originals = new object[names.Length];
            for (int i = 0; i < names.Length; i++) originals[i] = Adapter.GetField(names[i], BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            void Set(string name, object value) => Adapter.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
            bool seed = false, healthy = false; int completions = 0, healthyAcks = 0;
            var reads = new System.Collections.Generic.List<string>();
            string Order(string id, string state = "paid", bool reconcile = false) => "{\"id\":\"" + id + "\",\"requestId\":\"request-" + id + "\",\"productId\":\"sku-" + id + "\",\"requestedProductId\":\"sku-" + id + "\",\"state\":\"" + state + "\",\"reconciliationRequired\":" + (reconcile ? "true" : "false") + "}";
            try
            {
                Set("SessionIdentity", (Func<string>)(() => "unity-recovery-fixture"));
                Set("Persist", (Func<System.Threading.Tasks.Task>)(() => System.Threading.Tasks.Task.CompletedTask));
                Set("Complete", (Func<string, System.Threading.Tasks.Task<string>>)(id => {
                    completions++;
                    return System.Threading.Tasks.Task.FromResult("{\"order\":" + Order(id) + ",\"grant\":{\"grantId\":\"grant-" + id + "\"},\"applied\":true,\"requiresPersistence\":true}");
                }));
                Set("Request", (Func<string, string, System.Threading.Tasks.Task<string>>)((action, body) => {
                    if (action == "pending")
                    {
                        var orders = new System.Collections.Generic.List<string>();
                        if (seed) for (int i = 0; i < 70; i++) orders.Add(Order("terminal-" + i));
                        else if (healthy)
                        {
                            if (terminal == "listed-canceled") for (int i = 0; i < 64; i++) orders.Add(Order("terminal-" + i, "canceled"));
                            orders.Add(Order("healthy"));
                        }
                        return System.Threading.Tasks.Task.FromResult("{\"orders\":[" + string.Join(",", orders) + "]}");
                    }
                    var request = UnityEngine.JsonUtility.FromJson<RecoveryOrderFixture>(body);
                    if (action == "ack")
                    {
                        if (request.orderId != "healthy") return System.Threading.Tasks.Task.FromException<string>(new InvalidOperationException("ACK unavailable"));
                        healthyAcks++; return System.Threading.Tasks.Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-healthy\"}");
                    }
                    reads.Add(request.orderId);
                    return System.Threading.Tasks.Task.FromResult("{\"order\":" + Order(request.orderId, terminal != "reconciliation" ? "canceled" : "paid", terminal == "reconciliation") + "}");
                }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true });
                seed = true;
                Assert.Throws<InvalidOperationException>(() => API.Xsolla.RecoverPurchases().GetAwaiter().GetResult());
                Assert.That(completions, Is.EqualTo(70));
                seed = false; healthy = true;
                Assert.Throws<InvalidOperationException>(() => API.Xsolla.RecoverPurchases().GetAwaiter().GetResult());
                Assert.That(healthyAcks, Is.EqualTo(1)); Assert.That(completions, Is.EqualTo(71));
                Assert.That(reads.Count, Is.EqualTo(terminal == "listed-canceled" ? 6 : 64));
                if (terminal == "listed-canceled") Assert.That(reads, Does.Not.Contain("terminal-0"));
                healthy = false;
                Assert.Throws<InvalidOperationException>(() => API.Xsolla.RecoverPurchases().GetAwaiter().GetResult());
                Assert.That(reads.Count, Is.EqualTo(terminal == "listed-canceled" ? 70 : 128)); Assert.That(reads, Does.Contain("terminal-69"));
                var deferred = Adapter.GetField("deferredOrders", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That((int)deferred.GetType().GetProperty("Count").GetValue(deferred), Is.EqualTo(70));
            }
            finally
            {
                API.Xsolla.Configure(null);
                for (int i = 0; i < names.Length; i++) Set(names[i], originals[i]);
            }
        }
        [Test] public void RecoveryPreservesOriginalLiveOverrideDescriptorAfterFailedAcknowledgment()
        {
            var names = new[] { "SessionIdentity", "Request", "Complete", "Persist" };
            var originals = new object[names.Length];
            for (int i = 0; i < names.Length; i++) originals[i] = Adapter.GetField(names[i], BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            void Set(string name, object value) => Adapter.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
            var originalCallback = Callbacks.OnHardPurchasedStoreItem;
            var original = new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null) { ProductId = "override-sku", StoreItemUnnyId = "original-store" };
            const string identity = "unity-override-fixture", grantId = "grant";
            const string orderJson = "{\"id\":\"order\",\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"requestedProductId\":\"override-sku\",\"productId\":\"canonical-sku\",\"state\":\"paid\",\"acknowledged\":false,\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"original-store\"}}";
            int phase = 0, completions = 0, acknowledgments = 0;
            try
            {
                Callbacks.OnHardPurchasedStoreItem = null; // No live CMS required for this adapter contract.
                Set("SessionIdentity", (Func<string>)(() => identity));
                Set("Persist", (Func<System.Threading.Tasks.Task>)(() => System.Threading.Tasks.Task.CompletedTask));
                Set("Complete", (Func<string, System.Threading.Tasks.Task<string>>)(id => {
                    completions++;
                    return System.Threading.Tasks.Task.FromResult("{\"order\":" + orderJson + ",\"grant\":{\"grantId\":\"grant\",\"orderId\":\"order\",\"baseProductId\":\"canonical-sku\"},\"applied\":" + (completions == 1 ? "true" : "false") + ",\"requiresPersistence\":true}");
                }));
                Set("Request", (Func<string, string, System.Threading.Tasks.Task<string>>)((action, body) => {
                    if (action == "pending") return System.Threading.Tasks.Task.FromResult(phase == 2 ? "{\"orders\":[" + orderJson + "]}" : "{\"orders\":[]}");
                    if (phase == 1) return System.Threading.Tasks.Task.FromException<string>(new InvalidOperationException("ACK unavailable"));
                    var reports = (System.Collections.IDictionary)Adapter.GetField("deferredReports", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    var retained = (Actions.BalancyProductInfo)reports[identity + ":" + grantId];
                    Assert.That(retained, Is.SameAs(original));
                    Assert.That(retained.ProductId, Is.EqualTo("override-sku"));
                    Assert.That(retained.StoreItemUnnyId, Is.EqualTo("original-store"));
                    acknowledgments++; phase = 3;
                    return System.Threading.Tasks.Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant\"}");
                }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true });
                var order = UnityEngine.JsonUtility.FromJson(orderJson, Adapter.GetNestedType("Order", BindingFlags.NonPublic));
                var prepared = UnityEngine.JsonUtility.FromJson("{\"requestId\":\"00000000-0000-4000-8000-000000000001\",\"productId\":\"override-sku\"}", Adapter.GetNestedType("Prepared", BindingFlags.NonPublic));
                int epoch = (int)Adapter.GetField("generation", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                phase = 1;
                var apply = (System.Threading.Tasks.Task)Adapter.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { (object)"order", original, identity, epoch, prepared, order });
                Assert.Throws<InvalidOperationException>(() => apply.GetAwaiter().GetResult());
                phase = 2; API.Xsolla.RecoverPurchases().GetAwaiter().GetResult();
                Assert.That(completions, Is.EqualTo(2)); Assert.That(acknowledgments, Is.EqualTo(1));
                var reported = Adapter.GetField("reportedGrants", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(reported.GetType().GetMethod("Contains").Invoke(reported, new object[] { identity + ":" + grantId }), Is.True);
            }
            finally
            {
                API.Xsolla.Configure(null); Callbacks.OnHardPurchasedStoreItem = originalCallback;
                for (int i = 0; i < names.Length; i++) Set(names[i], originals[i]);
            }
        }
        [TestCase("canceled")]
        [TestCase("reconciliation")]
        public void SingleKnownTerminalOrderInPendingStillSignalsReconciliationWithoutGet(string terminal)
        {
            var names = new[] { "SessionIdentity", "Request", "Complete", "Persist" };
            var originals = new object[names.Length];
            for (int i = 0; i < names.Length; i++) originals[i] = Adapter.GetField(names[i], BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            void Set(string name, object value) => Adapter.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
            var cmsReady = typeof(CMS).GetField("IsReadyToUse", BindingFlags.Static | BindingFlags.NonPublic);
            var oldCmsReady = cmsReady.GetValue(null);
            var oldCallback = Callbacks.OnHardPurchasedStoreItem;
            bool seed = false, configured = false; int gets = 0, completions = 0, healthyAcks = 0, reports = 0; string reportedOrder = null;
            const string identity = "unity-single-terminal";
            string Order(string id, string state = "paid", bool reconciliation = false) => "{\"id\":\"" + id + "\",\"requestId\":\"request-" + id + "\",\"requestedProductId\":\"sku-" + id + "\",\"productId\":\"sku-" + id + "\",\"state\":\"" + state + "\",\"reconciliationRequired\":" + (reconciliation ? "true" : "false") + ",\"context\":{\"type\":\"StoreItem\",\"storeItemUnnyId\":\"doc-" + id + "\"}}";
            try
            {
                cmsReady.SetValue(null, false); // Callback receives no model; this test needs no native CMS.
                Callbacks.OnHardPurchasedStoreItem = (payment, item) => { reports++; reportedOrder = payment.OrderId; };
                Set("SessionIdentity", (Func<string>)(() => identity));
                Set("Persist", (Func<System.Threading.Tasks.Task>)(() => System.Threading.Tasks.Task.CompletedTask));
                Set("Complete", (Func<string, System.Threading.Tasks.Task<string>>)(id => {
                    completions++; return System.Threading.Tasks.Task.FromResult("{\"order\":" + Order(id) + ",\"grant\":{\"grantId\":\"grant-" + id + "\",\"orderId\":\"" + id + "\"},\"applied\":true,\"requiresPersistence\":true}");
                }));
                Set("Request", (Func<string, string, System.Threading.Tasks.Task<string>>)((action, body) => {
                    if (action == "pending") return System.Threading.Tasks.Task.FromResult(!configured ? "{\"orders\":[]}" : "{\"orders\":[" + (seed ? Order("known") : Order("known", terminal == "canceled" ? "canceled" : "paid", terminal == "reconciliation") + "," + Order("healthy")) + "]}");
                    if (action == "get") { gets++; return System.Threading.Tasks.Task.FromException<string>(new InvalidOperationException("unexpected GET")); }
                    if (seed) return System.Threading.Tasks.Task.FromException<string>(new InvalidOperationException("seed ACK unavailable"));
                    healthyAcks++; return System.Threading.Tasks.Task.FromResult("{\"acknowledged\":true,\"grantId\":\"grant-healthy\"}");
                }));
                API.Xsolla.Configure(new XsollaOptions { Enabled = true, Country = "US", CanOfferAlternativePayment = () => true });
                configured = true; seed = true;
                Assert.Throws<InvalidOperationException>(() => API.Xsolla.RecoverPurchases().GetAwaiter().GetResult());
                seed = false;
                var error = Assert.Throws<InvalidOperationException>(() => API.Xsolla.RecoverPurchases().GetAwaiter().GetResult());
                Assert.That(error.Message, Is.EqualTo("xsolla_recovery_incomplete"));
                var causes = ((AggregateException)error.InnerException).Flatten().InnerExceptions;
                Assert.That(causes.Count, Is.EqualTo(1)); Assert.That(causes[0].Message, Is.EqualTo("xsolla_reconciliation_required"));
                Assert.That(gets, Is.Zero); Assert.That(completions, Is.EqualTo(2)); Assert.That(healthyAcks, Is.EqualTo(1));
                Assert.That(reports, Is.EqualTo(1)); Assert.That(reportedOrder, Is.EqualTo("healthy"));
                var debts = (System.Collections.IDictionary)Adapter.GetField("deferredOrders", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(debts.Count, Is.EqualTo(1)); Assert.That(debts.Contains("known"), Is.True);
                var descriptors = (System.Collections.IDictionary)Adapter.GetField("deferredReports", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(descriptors.Contains(identity + ":grant-known"), Is.True);
            }
            finally
            {
                API.Xsolla.Configure(null); cmsReady.SetValue(null, oldCmsReady); Callbacks.OnHardPurchasedStoreItem = oldCallback;
                for (int i = 0; i < names.Length; i++) Set(names[i], originals[i]);
            }
        }
        [Serializable] private class RecoveryOrderFixture { public string orderId; }
        [TestCase("requestId", "RequestId")]
        [TestCase("productId", "ProductId")]
        [TestCase("requestedProductId", "RequestedProductId")]
        [TestCase("grantId", "GrantId")]
        public void DeferredReadbackValidatesEveryVerifiedIdentityField(string orderField, string deferredField)
        {
            var orderType = Adapter.GetNestedType("Order", BindingFlags.NonPublic);
            var deferredType = Adapter.GetNestedType("DeferredOrder", BindingFlags.NonPublic);
            var order = UnityEngine.JsonUtility.FromJson("{\"id\":\"order\",\"requestId\":\"request\",\"productId\":\"base-sku\",\"requestedProductId\":\"override-sku\",\"grantId\":\"grant\"}", orderType);
            var deferred = Activator.CreateInstance(deferredType);
            foreach (var pair in new[] { new[] { "OrderId", "order" }, new[] { "RequestId", "request" }, new[] { "ProductId", "base-sku" }, new[] { "RequestedProductId", "override-sku" }, new[] { "GrantId", "grant" } })
                deferredType.GetField(pair[0], BindingFlags.Instance | BindingFlags.NonPublic).SetValue(deferred, pair[1]);
            var validate = Adapter.GetMethod("ValidateDeferredOrder", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => validate.Invoke(null, new[] { order, deferred }));
            Assert.That(deferredType.GetField(deferredField, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(deferred), Is.Not.Null);
            orderType.GetField(orderField).SetValue(order, "foreign");
            var failure = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new[] { order, deferred }));
            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
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
