namespace Balancy.Data.SmartObjects
{
    public class ShopSlot : Balancy.Data.BaseData 
    {
        private string _unnyIdSlot;
        public Balancy.Models.LiveOps.Store.Slot Slot => GetModelByUnnyId<Balancy.Models.LiveOps.Store.Slot>(_unnyIdSlot);
        
        public string UnnyIdSlot => _unnyIdSlot;
        
        public override void InitData()
        {
            base.InitData();
            InitAndSubscribeForParamChange("unnyIdSlot", Update_unnyIdSlot);
        }
        
        private void Update_unnyIdSlot() { _unnyIdSlot = GetStringParam("unnyIdSlot"); }
        
        public bool IsAvailable() => LibraryMethods.Extra.balancyShopSlot_IsAvailable(GetRawPointer());
        public int GetSecondsLeftUntilAvailable() => LibraryMethods.Extra.balancyShopSlot_GetSecondsLeftUntilAvailable(GetRawPointer());
        public bool HasLimits() => LibraryMethods.Extra.balancyShopSlot_HasLimits(GetRawPointer());
        public int GetPurchasesLimitForCycle() => LibraryMethods.Extra.balancyShopSlot_GetPurchasesLimitForCycle(GetRawPointer());
        public int GetPurchasesDoneDuringTheLastCycle() => LibraryMethods.Extra.balancyShopSlot_GetPurchasesDoneDuringTheLastCycle(GetRawPointer());
        public float Multiplier => LibraryMethods.Extra.balancyShopSlot_GetMultiplier(GetRawPointer());
        public bool HasMultiplier => LibraryMethods.Extra.balancyShopSlot_HasMultiplier(GetRawPointer());
    }
}
