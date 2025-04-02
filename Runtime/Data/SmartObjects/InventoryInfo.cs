
namespace Balancy.Data.SmartObjects
{
    public class InventoryInfo : Balancy.Data.BaseData 
    {
        
		private Balancy.Data.SmartObjects.Inventory _currencies;
		private Balancy.Data.SmartObjects.Inventory _items;
        
        
		public Balancy.Data.SmartObjects.Inventory Currencies => _currencies;
		public Balancy.Data.SmartObjects.Inventory Items => _items;
        
        public override void InitData()
        {
            base.InitData();
            
			_currencies = GetBaseDataParam<Balancy.Data.SmartObjects.Inventory>("currencies");
			_items = GetBaseDataParam<Balancy.Data.SmartObjects.Inventory>("items");
        }
        
    }
}
