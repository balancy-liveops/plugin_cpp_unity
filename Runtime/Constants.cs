public class Constants
{
    public enum Environment
    {
        Development,
        Stage,
        Production
    }
    
    public class Errors
    {
        public const string PurchaseNotEnoughItems = "Not enough items in the inventory";
        public const string PurchaseNotAds = "You need to watch ads first";
        public const string PurchaseCancelled = "Purchase was cancelled by the user.";
        public const string PurchaseFailed = "Purchase failed.";
        public const string PurchaseInvalidPriceType = "Invalid price type for purchase.";
        public const string StoreItemNull = "Store item cannot be null.";
        public const string GameOfferNull = "Game offer cannot be null.";
        public const string OfferInfoNull = "Offer info cannot be null.";
        public const string OfferGroupInfoNull = "Offer group info cannot be null.";
    }

    public enum Platform
    {
        Undefined = 0,
        Unknown = 1,
        //            Vkontakte = 3,
        Facebook = 4,
        //            Odnoklassniki = 5,
        FbInstant = 6,

        AndroidGooglePlay = 7,
        IosAppStore = 8,
        AmazonStore = 14,
        Yoomoney = 15
    }
}