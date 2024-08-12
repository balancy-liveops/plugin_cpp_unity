namespace Balancy.LiveOps
{
    public class Ads
    {
        public enum AdType
        {
            None = 0,
            Rewarded,
            Interstitial,
            Custom
        }
        
        public static void TrackRevenue(AdType type, double revenue, string placement) => 
            LibraryMethods.Profile.balancySystemProfileTrackRevenue(type, revenue, placement);
    }
}
