namespace Balancy.Data.SmartObjects
{
    public class ShopSlot : Balancy.Data.BaseData 
    {
        private string _unnyIdSlot;
        public Balancy.Models.LiveOps.Store.Slot Slot => GetModelByUnnyId<Balancy.Models.LiveOps.Store.Slot>(_unnyIdSlot);
        
        public override void InitData()
        {
            base.InitData();
            InitAndSubscribeForParamChange("unnyIdSlot", Update_unnyIdSlot);
        }
        
        private void Update_unnyIdSlot() { _unnyIdSlot = GetStringParam("unnyIdSlot"); }
    }
}
