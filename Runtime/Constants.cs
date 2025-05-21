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
        public const string GameOfferGroupNull = "Game offer group cannot be null.";
        public const string OfferInfoNull = "Offer info cannot be null.";
        public const string OfferGroupInfoNull = "Offer group info cannot be null.";
    }

    public enum BalancyPlatform
    {
        Undefined = 0,
        Unknown = 1,
        //            Vkontakte = 3,
        Facebook = 4,
        //            Odnoklassniki = 5,
        FbInstant = 6,

        AndroidGooglePlay = 7,
        IosAppStore = 8,
        NutakuPCBrowser = 9,
        NutakuSPBrowser = 10,
        NutakuAndroid = 11,
        NutakuClientGames = 12,
        AmazonStore = 14,
        Yoomoney = 15,
        MicrosoftStore = 16,
        Erolabs = 20,
        Steam = 30
    }
    
    public enum DevicePlatform
    {
        Unknown = -1,
        // OSXEditor = 0,
        OSXPlayer = 1,
        WindowsPlayer = 2,
        // WindowsEditor = 7,
        IPhonePlayer = 8,
        Android = 11, // 0x0000000B
        LinuxPlayer = 13, // 0x0000000D
        // LinuxEditor = 16, // 0x00000010
        WebGLPlayer = 17, // 0x00000011
        WSAPlayerX86 = 18, // 0x00000012
        WSAPlayerX64 = 19, // 0x00000013
        WSAPlayerARM = 20, // 0x00000014
        PS4 = 25, // 0x00000019
        XboxOne = 27, // 0x0000001B
        tvOS = 31, // 0x0000001F
        Switch = 32, // 0x00000020
        Stadia = 34, // 0x00000022
        GameCoreXboxSeries = 36, // 0x00000024
        GameCoreXboxOne = 37, // 0x00000025
        PS5 = 38, // 0x00000026
        EmbeddedLinuxArm64 = 39, // 0x00000027
        EmbeddedLinuxArm32 = 40, // 0x00000028
        EmbeddedLinuxX64 = 41, // 0x00000029
        EmbeddedLinuxX86 = 42, // 0x0000002A
        LinuxServer = 43, // 0x0000002B
        WindowsServer = 44, // 0x0000002C
        OSXServer = 45, // 0x0000002D
        QNXArm32 = 46, // 0x0000002E
        QNXArm64 = 47, // 0x0000002F
        QNXX64 = 48, // 0x00000030
        QNXX86 = 49, // 0x00000031
        VisionOS = 50, // 0x00000032
    }
}