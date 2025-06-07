namespace Balancy.Data.SmartObjects
{
    public class GameShop : Balancy.Data.BaseData 
    {
        private string _unnyIdShop;
        public Balancy.Models.SmartObjects.GameStoreBase Shop => GetModelByUnnyId<Balancy.Models.SmartObjects.GameStoreBase>(_unnyIdShop);
        
		private SmartList<Balancy.Data.SmartObjects.ShopPage> _activePages;
		public SmartList<Balancy.Data.SmartObjects.ShopPage> ActivePages => _activePages;

        internal string ShopUnnyId => _unnyIdShop;
        
        public override void InitData()
        {
            base.InitData();
            
			_activePages = GetListBaseDataParam<Balancy.Data.SmartObjects.ShopPage>("activePages");
            InitAndSubscribeForParamChange("unnyIdShop", Update_unnyIdShop);
        }
        
        private void Update_unnyIdShop() { _unnyIdShop = GetStringParam("unnyIdShop"); }
        
        public Data.SmartObjects.ShopSlot FindShopSlot(string slotId)
        {
            foreach (var page in ActivePages)
            {
                var shopSlot = page.FindShopSlot(slotId);
                if (shopSlot != null)
                    return shopSlot;
            }

            return null;
        }
    }
}
