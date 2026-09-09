using System.Reflection;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class CallbacksTests
    {
        private static MethodInfo ClearAll => typeof(Callbacks).GetMethod("ClearAll", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => ClearAll.Invoke(null, null);

        [TearDown]
        public void TearDown() => ClearAll.Invoke(null, null);

        [Test]
        public void PaymentReadyNotifiesExistingAndLateSubscribersExactlyOnce()
        {
            var firstCalls = 0;
            var lateCalls = 0;
            Callbacks.OnPaymentIsReady += () => firstCalls++;

            Callbacks.SetPaymentIsReady();
            Callbacks.OnPaymentIsReady += () => lateCalls++;

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(lateCalls, Is.EqualTo(1));
        }

        [Test]
        public void ClearAllResetsDelegatesAndPaymentReadyLatch()
        {
            var calls = 0;
            Callbacks.OnDataUpdated += _ => calls++;
            Callbacks.OnPaymentIsReady += () => calls++;
            Callbacks.SetPaymentIsReady();

            ClearAll.Invoke(null, null);
            Callbacks.OnDataUpdated?.Invoke(new Callbacks.DataUpdatedStatus(true, true, true));
            Callbacks.OnPaymentIsReady += () => calls++;

            Assert.That(calls, Is.EqualTo(1), "late payment subscriber must not observe readiness from a stopped session");
        }

        [Test]
        public void StatusValueObjectsPreserveAllFieldsAndNormalizeShopId()
        {
            var data = new Callbacks.DataUpdatedStatus(true, false, true);
            var download = new Callbacks.NetworkDownloadCompletedInfo(
                "url", "path", "domain", true, 12.5f, 7.25f, 123456789L, false, 503, "offline", 3);
            var shop = new Callbacks.ShopUpdatedInfo(Core.ShopChangeType.ShopChanged, -1, -1, null);

            Assert.That(data.IsCloudSynced, Is.True);
            Assert.That(data.IsCMSUpdated, Is.False);
            Assert.That(data.IsProfileUpdated, Is.True);
            Assert.That(download.DownloadedBytes, Is.EqualTo(123456789L));
            Assert.That(download.ErrorCode, Is.EqualTo(503));
            Assert.That(download.Attempts, Is.EqualTo(3));
            Assert.That(shop.ShopUnnyId, Is.Empty);
        }
    }
}
