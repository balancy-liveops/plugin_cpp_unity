using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Balancy.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Balancy.Tests
{
    public class PurchaseLifecycleTests
    {
        private static readonly MethodInfo HardPurchase = typeof(API)
            .GetMethod("HardPurchase", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo Cleanup = typeof(API)
            .GetMethod("CleanUpPendingCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly IList Pending = (IList)typeof(API)
            .GetField("_callbacks", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);

        [SetUp]
        public void SetUp()
        {
            Actions.Purchasing.ResetHardPurchaseCallback();
            Cleanup.Invoke(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Actions.Purchasing.ResetHardPurchaseCallback();
            Cleanup.Invoke(null, null);
        }

        [Test]
        public void EqualConcurrentPurchasesCompleteTheirExactCallbacks()
        {
            var requested = new List<Actions.BalancyProductInfo>();
            var completions = new List<string>();
            Actions.Purchasing.SetHardPurchaseCallback(requested.Add);
            var first = new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null);
            var second = new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null);

            Register(first, (_, __) => completions.Add("first"));
            Register(second, (_, __) => completions.Add("second"));
            API.FinalizedHardPurchase(Actions.PurchaseResult.Failed, requested[0], null, null);
            API.FinalizedHardPurchase(Actions.PurchaseResult.Failed, requested[1], null, null);

            Assert.That(completions, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void ThrowingPurchaseProviderFailsOnceAndDoesNotLeakPendingState()
        {
            var calls = 0;
            Actions.Purchasing.SetHardPurchaseCallback(_ => throw new InvalidOperationException("provider failed"));
            LogAssert.Expect(LogType.Exception, new Regex("provider failed"));

            Assert.DoesNotThrow(() => Register(
                new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null),
                (success, error) => { Assert.That(success, Is.False); calls++; }));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void ProviderThatFinalizesThenThrowsDoesNotCompletePurchaseTwice()
        {
            var errors = new List<string>();
            Actions.Purchasing.SetHardPurchaseCallback(product =>
            {
                API.FinalizedHardPurchase(Actions.PurchaseResult.Failed, product, null, null);
                throw new InvalidOperationException("provider threw after finalization");
            });
            LogAssert.Expect(LogType.Exception, new Regex("provider threw after finalization"));

            Assert.DoesNotThrow(() => Register(
                new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null),
                (success, error) => errors.Add(error)));

            Assert.That(errors, Is.EqualTo(new[] { "" }));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void ProviderThatCleansUpThenThrowsDoesNotCompletePurchaseTwice()
        {
            var errors = new List<string>();
            Actions.Purchasing.SetHardPurchaseCallback(_ =>
            {
                Cleanup.Invoke(null, null);
                throw new InvalidOperationException("provider threw after cleanup");
            });
            LogAssert.Expect(LogType.Exception, new Regex("provider threw after cleanup"));

            Assert.DoesNotThrow(() => Register(
                new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null),
                (success, error) => errors.Add(error)));

            Assert.That(errors, Is.EqualTo(new[] { "SDK stopped" }));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void ThrowingPurchaseCallbackDoesNotPreventCleanupOfOtherPurchases()
        {
            var calls = new List<string>();
            Actions.Purchasing.SetHardPurchaseCallback(_ => { });
            Register(new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null),
                (success, error) =>
                {
                    calls.Add("throwing");
                    throw new InvalidOperationException("user callback failed");
                });
            Register(new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null),
                (success, error) => calls.Add("remaining"));
            LogAssert.Expect(LogType.Exception, new Regex("user callback failed"));

            Assert.DoesNotThrow(() => Cleanup.Invoke(null, null));
            Cleanup.Invoke(null, null);

            Assert.That(calls, Is.EqualTo(new[] { "throwing", "remaining" }));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void CleanupFailsPendingPurchasesOnceAndIsIdempotent()
        {
            var calls = 0;
            Actions.Purchasing.SetHardPurchaseCallback(_ => { });
            Register(new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null),
                (success, error) => { Assert.That(success, Is.False); calls++; });

            Cleanup.Invoke(null, null);
            Cleanup.Invoke(null, null);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void NullProductFailsImmediatelyWithoutCallingProviderOrLeakingState()
        {
            var providerCalls = 0;
            var callbackCalls = 0;
            Actions.Purchasing.SetHardPurchaseCallback(_ => providerCalls++);

            Register(null, (success, error) =>
            {
                Assert.That(success, Is.False);
                Assert.That(error, Does.Contain("null"));
                callbackCalls++;
            });

            Assert.That(providerCalls, Is.Zero);
            Assert.That(callbackCalls, Is.EqualTo(1));
            Assert.That(Pending.Count, Is.Zero);
        }

        [Test]
        public void SuccessfulResultWithoutPaymentInfoAllowsNullValidationCallback()
        {
            var calls = 0;
            bool? purchaseSuccess = null;
            Actions.Purchasing.SetHardPurchaseCallback(_ => { });
            var product = new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null);
            Register(product, (success, error) => { calls++; purchaseSuccess = success; });

            Assert.DoesNotThrow(() => API.FinalizedHardPurchase(
                Actions.PurchaseResult.Success, product, null, null));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(purchaseSuccess, Is.False);
            Assert.That(Pending.Count, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void SuccessfulResultWithoutPaymentInfoFailsBothCallbacksEvenIfTheyThrow(
            bool throwValidation, bool throwPurchase)
        {
            var callbackCalls = 0;
            var validationCalls = 0;
            bool? purchaseSuccess = null;
            string purchaseError = null;
            bool? validationSuccess = null;
            bool? removeFromPending = null;
            Actions.Purchasing.SetHardPurchaseCallback(_ => { });
            var product = new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null);
            Register(product, (success, error) =>
            {
                purchaseSuccess = success;
                purchaseError = error;
                callbackCalls++;
                if (throwPurchase)
                    throw new InvalidOperationException("purchase callback failed");
            });
            if (throwValidation)
                LogAssert.Expect(LogType.Exception, new Regex("validation callback failed"));
            if (throwPurchase)
                LogAssert.Expect(LogType.Exception, new Regex("purchase callback failed"));

            Assert.DoesNotThrow(() => API.FinalizedHardPurchase(
                Actions.PurchaseResult.Success, product, null, (success, remove) =>
                {
                    validationSuccess = success;
                    removeFromPending = remove;
                    validationCalls++;
                    if (throwValidation)
                        throw new InvalidOperationException("validation callback failed");
                }));

            Assert.That(validationCalls, Is.EqualTo(1));
            Assert.That(validationSuccess, Is.False);
            Assert.That(removeFromPending, Is.False);
            Assert.That(callbackCalls, Is.EqualTo(1));
            Assert.That(purchaseSuccess, Is.False);
            Assert.That(purchaseError, Does.Contain("Payment info"));
            Assert.That(Pending.Count, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ValidationDispatchThrowCompletesOnceAndIgnoresLateResponse(bool respondBeforeThrow)
        {
            var responses = new List<Responses.PurchaseProductResponseData>();
            ResponseCallback<Responses.PurchaseProductResponseData> lateResponse = null;
            var successResponse = new Responses.PurchaseProductResponseData { Success = true, PriceUSD = 7 };
            Action<ResponseCallback<Responses.PurchaseProductResponseData>> dispatch = response =>
            {
                lateResponse = response;
                if (respondBeforeThrow) response(successResponse);
                throw new InvalidOperationException("dispatch failed");
            };
            var execute = typeof(API).GetMethod("ExecutePurchaseValidation", BindingFlags.Static | BindingFlags.NonPublic);
            LogAssert.Expect(LogType.Exception, new Regex("dispatch failed"));

            Assert.DoesNotThrow(() => execute.Invoke(null, new object[]
                { dispatch, new ResponseCallback<Responses.PurchaseProductResponseData>(responses.Add) }));
            lateResponse(successResponse);

            Assert.That(responses.Count, Is.EqualTo(1));
            Assert.That(responses[0].Success, Is.EqualTo(respondBeforeThrow));
            if (respondBeforeThrow)
                Assert.That(responses[0], Is.SameAs(successResponse));
            else
            {
                Assert.That(responses[0].ErrorMessage, Is.EqualTo("dispatch failed"));
                Assert.That(responses[0].RemoveFromPending, Is.False);
            }
        }

        [Test]
        public void NullValidationResponseCompletesOnceWithRetryableFailure()
        {
            var responses = new List<Responses.PurchaseProductResponseData>();
            Action<ResponseCallback<Responses.PurchaseProductResponseData>> dispatch = response =>
            {
                response(null);
                response(new Responses.PurchaseProductResponseData { Success = true });
            };
            var execute = typeof(API).GetMethod("ExecutePurchaseValidation", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.DoesNotThrow(() => execute.Invoke(null, new object[]
                { dispatch, new ResponseCallback<Responses.PurchaseProductResponseData>(responses.Add) }));

            Assert.That(responses.Count, Is.EqualTo(1));
            Assert.That(responses[0].Success, Is.False);
            Assert.That(responses[0].RemoveFromPending, Is.False);
            Assert.That(responses[0].ErrorMessage, Does.Contain("null"));
        }

        private static void Register(Actions.BalancyProductInfo product, Action<bool, string> callback)
        {
            HardPurchase.Invoke(null, new object[] { product, callback });
        }

        [TestCase(Actions.BalancyProductInfo.PurchaseType.ShopSlot)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.Offer)]
        [TestCase(Actions.BalancyProductInfo.PurchaseType.OfferGroup)]
        public void MissingProfileCompletesBothCallbacksInsteadOfThrowing(
            Actions.BalancyProductInfo.PurchaseType type)
        {
            var profiles = (IDictionary)typeof(Profiles)
                .GetField("_cachedProfiles", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var hadProfile = profiles.Contains("UnnyProfile");
            var previousProfile = profiles["UnnyProfile"];
            profiles["UnnyProfile"] = null;
            try
            {
                var completions = new List<bool>();
                var validations = new List<bool>();
                Actions.Purchasing.SetHardPurchaseCallback(_ => { });
                var product = new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null) { Type = type };
                Register(product, (success, error) => completions.Add(success));
                LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

                Assert.DoesNotThrow(() => API.FinalizedHardPurchase(Actions.PurchaseResult.Success,
                    product, new PaymentInfo(), (success, remove) => validations.Add(success || remove)));

                Assert.That(completions, Is.EqualTo(new[] { false }));
                Assert.That(validations, Is.EqualTo(new[] { false }));
                Assert.That(Pending.Count, Is.Zero);
            }
            finally
            {
                if (hadProfile) profiles["UnnyProfile"] = previousProfile;
                else profiles.Remove("UnnyProfile");
            }
        }
    }
}
