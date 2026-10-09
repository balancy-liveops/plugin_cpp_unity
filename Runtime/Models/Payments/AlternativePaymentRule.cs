namespace Balancy.Models.Payments
{
    public enum AlternativePaymentProvider { Xsolla = 50 }
    public enum AlternativeProductSelection { All = 0, IncludeOnly = 1 }

    /// <summary>A condition-selected document. Export controls availability after deployment.</summary>
    public class AlternativePaymentRule : SmartObjects.ConditionalTemplate
    {
        private AlternativePaymentProvider provider;
        private float minPriceUsd;
        private AlternativeProductSelection productSelection;
        private UnnyProduct[] includedProducts, excludedProducts;
        private string unnyIdBonus;
        private UnnyObject choiceView;

        public AlternativePaymentProvider Provider => provider;
        public float MinPriceUsd => minPriceUsd;
        public AlternativeProductSelection ProductSelection => productSelection;
        public UnnyProduct[] IncludedProducts => includedProducts;
        public UnnyProduct[] ExcludedProducts => excludedProducts;
        public PurchaseBonus Bonus => GetModelByUnnyId<PurchaseBonus>(unnyIdBonus);
        public UnnyObject ChoiceView => choiceView;

        public override void InitData()
        {
            base.InitData();
            provider = (AlternativePaymentProvider)GetIntParam("provider");
            minPriceUsd = GetFloatParam("minPriceUsd");
            productSelection = (AlternativeProductSelection)GetIntParam("productSelection");
            includedProducts = GetObjectArrayParam<UnnyProduct>("includedProducts");
            excludedProducts = GetObjectArrayParam<UnnyProduct>("excludedProducts");
            unnyIdBonus = GetStringParam("unnyIdBonus");
            choiceView = GetObjectParam<UnnyObject>("choiceView");
        }
    }
}
