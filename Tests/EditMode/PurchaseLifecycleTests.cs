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
        public void SuccessfulResultWithoutPaymentInfoFailsInsteadOfThrowing()
        {
            var callbackCalls = 0;
            Actions.Purchasing.SetHardPurchaseCallback(_ => { });
            var product = new Actions.BalancyProductInfo((Balancy.Models.SmartObjects.StoreItem)null);
            Register(product, (success, error) =>
            {
                Assert.That(success, Is.False);
                Assert.That(error, Does.Contain("Payment info"));
                callbackCalls++;
            });

            Assert.DoesNotThrow(() => API.FinalizedHardPurchase(
                Actions.PurchaseResult.Success, product, null, null));

            Assert.That(callbackCalls, Is.EqualTo(1));
            Assert.That(Pending.Count, Is.Zero);
        }

        private static void Register(Actions.BalancyProductInfo product, Action<bool, string> callback)
        {
            HardPurchase.Invoke(null, new object[] { product, callback });
        }
    }
}
