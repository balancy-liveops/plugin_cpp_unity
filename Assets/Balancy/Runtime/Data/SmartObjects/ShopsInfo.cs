
namespace Balancy.Data.SmartObjects
{
    public class ShopsInfo : Balancy.Data.BaseData 
    {
        private string _unnyIdActiveShop;
        private Balancy.Models.SmartObjects.GameStoreBase ActiveShop => GetModelByUnnyId<Balancy.Models.SmartObjects.GameStoreBase>(_unnyIdActiveShop);
        
		private SmartList<Balancy.Data.SmartObjects.GameShop> _gameShops;
        public SmartList<Balancy.Data.SmartObjects.GameShop> GameShops => _gameShops;

        public GameShop ActiveShopInfo
        {
            get
            {
                for (int i = 0; i < _gameShops.Count; i++)
                {
                    if (_gameShops[i].ShopUnnyId == _unnyIdActiveShop)
                        return _gameShops[i];
                }

                return null;
            }
        }
        
        public override void InitData()
        {
            base.InitData();
            
			_gameShops = GetListBaseDataParam<Balancy.Data.SmartObjects.GameShop>("gameShops");
            InitAndSubscribeForParamChange("unnyIdActiveShop", Update_unnyIdActiveShop);
        }
        
        private void Update_unnyIdActiveShop() { _unnyIdActiveShop = GetStringParam("unnyIdActiveShop"); }
    }
}
