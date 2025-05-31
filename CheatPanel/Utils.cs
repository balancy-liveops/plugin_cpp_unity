using Balancy.Models.SmartObjects;

namespace Balancy.Cheats
{
    public class Utils
    {
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
