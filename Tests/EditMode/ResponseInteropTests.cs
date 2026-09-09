using System;
using System.Runtime.InteropServices;
using Balancy.Core;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class ResponseInteropTests
    {
        [Test]
        public void ResponseSuccessUsesOneAsTheOnlyTrueWireValue()
        {
            var response = new Responses.ResponseData { Success = true };
            Assert.That(response.Success, Is.True);
            response.Success = false;
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public void PackedResponseLayoutsKeepNativeFieldOffsets()
        {
            var responseType = typeof(Responses.ResponseData);
            Assert.That(Marshal.OffsetOf(responseType, "success").ToInt32(), Is.Zero);
            Assert.That(Marshal.OffsetOf(responseType, nameof(Responses.ResponseData.ErrorCode)).ToInt32(), Is.EqualTo(1));
            Assert.That(Marshal.OffsetOf(responseType, nameof(Responses.ResponseData.ErrorMessage)).ToInt32(), Is.EqualTo(5));
            Assert.That(Marshal.SizeOf(responseType), Is.EqualTo(5 + IntPtr.Size));

            var authType = typeof(Responses.AuthResponseData);
            Assert.That(Marshal.OffsetOf(authType, nameof(Responses.AuthResponseData.UserId)).ToInt32(),
                Is.EqualTo(Marshal.SizeOf(responseType)));
            Assert.That(Marshal.SizeOf(authType), Is.EqualTo(Marshal.SizeOf(responseType) + IntPtr.Size));
        }

        [Test]
        public void PaymentInfoRemainsPackedAcrossManagedNativeBoundary()
        {
            Assert.That(Marshal.OffsetOf<PaymentInfo>(nameof(PaymentInfo.Price)).ToInt32(), Is.Zero);
            Assert.That(Marshal.OffsetOf<PaymentInfo>(nameof(PaymentInfo.Receipt)).ToInt32(), Is.EqualTo(sizeof(float)));
            Assert.That(Marshal.OffsetOf<PaymentInfo>(nameof(PaymentInfo.ProductId)).ToInt32(), Is.EqualTo(sizeof(float) + IntPtr.Size));
            Assert.That(Marshal.OffsetOf<PaymentInfo>(nameof(PaymentInfo.Currency)).ToInt32(), Is.EqualTo(sizeof(float) + 2 * IntPtr.Size));
            Assert.That(Marshal.OffsetOf<PaymentInfo>(nameof(PaymentInfo.OrderId)).ToInt32(), Is.EqualTo(sizeof(float) + 3 * IntPtr.Size));
            Assert.That(Marshal.OffsetOf<PaymentInfo>(nameof(PaymentInfo.PriceUSD)).ToInt32(), Is.EqualTo(sizeof(float) + 4 * IntPtr.Size));
            Assert.That(Marshal.SizeOf<PaymentInfo>(), Is.EqualTo(2 * sizeof(float) + 4 * IntPtr.Size));
        }
    }
}
