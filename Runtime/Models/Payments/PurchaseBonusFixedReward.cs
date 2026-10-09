namespace Balancy.Models.Payments
{
    public class PurchaseBonusFixedReward : PurchaseBonus
    {
        private SmartObjects.Reward reward;
        public SmartObjects.Reward Reward => reward;
        public override void InitData()
        {
            base.InitData();
            reward = GetObjectParam<SmartObjects.Reward>("reward");
        }
    }
}
