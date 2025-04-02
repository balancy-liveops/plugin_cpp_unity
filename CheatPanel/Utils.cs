using System;
using Balancy.Models.SmartObjects;

namespace Balancy.Cheats
{
    public class Utils
    {
        public static Balancy.Core.PaymentInfo CreateTestPaymentInfo(Price price)
        {
            var paymentInfo = new Balancy.Core.PaymentInfo
            {
                Price = price.Product.Price,
                Currency = "USD",
                OrderId = Guid.NewGuid().ToString(),
                ProductId = price.Product.ProductId,
                Receipt = "<receipt>"
            };

            //Below is the testing receipt, it's not designed for the production
            paymentInfo.Receipt = "{\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"" +
                                  paymentInfo.OrderId + "\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"" +
                                  paymentInfo.ProductId + "\\\\\\\"}\\\",\\\"signature\\\":\\\"bypass\\\"}\"}";
            return paymentInfo;
        }
    }
}
