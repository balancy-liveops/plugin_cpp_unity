
namespace Balancy.Data.SmartObjects
{
    public class ShopPage : Balancy.Data.BaseData 
    {
        private string _unnyIdPage;
        public Balancy.Models.LiveOps.Store.Page Page => GetModelByUnnyId<Balancy.Models.LiveOps.Store.Page>(_unnyIdPage);
        
		private SmartList<Balancy.Data.SmartObjects.ShopSlot> _activeSlots;
		public SmartList<Balancy.Data.SmartObjects.ShopSlot> ActiveSlots => _activeSlots;
        
        public override void InitData()
        {
            base.InitData();
            
			_activeSlots = GetListBaseDataParam<Balancy.Data.SmartObjects.ShopSlot>("activeSlots");
            InitAndSubscribeForParamChange("unnyIdPage", Update_unnyIdPage);
        }
        
        private void Update_unnyIdPage() { _unnyIdPage = GetStringParam("unnyIdPage"); }
    }
}
