
namespace Balancy.Data.SmartObjects
{
    public class ShopsInfo : Balancy.Data.BaseData 
    {
        
		private SmartList<Balancy.Data.SmartObjects.GameShop> _gameShops;
        
        
		public SmartList<Balancy.Data.SmartObjects.GameShop> GameShops => _gameShops;
        
        public override void InitData()
        {
            base.InitData();
            
			_gameShops = GetListBaseDataParam<Balancy.Data.SmartObjects.GameShop>("gameShops");
        }
        
    }
}
