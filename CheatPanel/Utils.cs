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

        public static string GetPriceText(StoreItem storeItem)
        {
            if (storeItem?.Price == null || storeItem.Price.IsFree())
                return "FREE";

            var price = storeItem.Price;
            
            switch (price.Type)
            {
                case PriceType.Hard:
                    return "USD " + price.Product.Price;
                case PriceType.Soft:
                    return price.Items.Length > 0 ? price.Items[0].Count + " " + price.Items[0].Item.Name.Value : "Unknown";
                case PriceType.Ads:
                    return $"▶ {storeItem.GetAdsWatched()} / {storeItem.Price?.Ads}";
                default:
                    return "Opa";
            }
        }
    }
}
