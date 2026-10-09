namespace Balancy.Models.Payments
{
    public class PurchaseBonusPerSpend : PurchaseBonus
    {
        private string unnyIdItem;
        private float itemsPerUsd;
        public SmartObjects.Item Item => GetModelByUnnyId<SmartObjects.Item>(unnyIdItem);
        public string ItemUnnyId => unnyIdItem;
        public float ItemsPerUsd => itemsPerUsd;
        public override void InitData()
        {
            base.InitData();
            unnyIdItem = GetStringParam("unnyIdItem");
            itemsPerUsd = GetFloatParam("itemsPerUsd");
        }
    }
}
