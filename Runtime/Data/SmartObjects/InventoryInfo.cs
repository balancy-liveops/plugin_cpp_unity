
using System;

namespace Balancy.Data.SmartObjects
{
    public class InventoryInfo : Balancy.Data.BaseData 
    {
		private Balancy.Data.SmartObjects.Inventory _currencies;
		private Balancy.Data.SmartObjects.Inventory _eventItems;
		private Balancy.Data.SmartObjects.Inventory _items;
        
		public Balancy.Data.SmartObjects.Inventory Currencies => _currencies;
		public Balancy.Data.SmartObjects.Inventory EventItems => _eventItems;
		public Balancy.Data.SmartObjects.Inventory Items => _items;
        
        public override void InitData()
        {
            base.InitData();
            
			_currencies = GetBaseDataParam<Balancy.Data.SmartObjects.Inventory>("currencies");
			_eventItems = GetBaseDataParam<Balancy.Data.SmartObjects.Inventory>("eventItems");
			_items = GetBaseDataParam<Balancy.Data.SmartObjects.Inventory>("items");
        }

        public Balancy.Data.SmartObjects.Inventory FindInventory(IntPtr ptr)
        {
	        if (_currencies.Equals(ptr)) return _currencies;
	        if (_eventItems.Equals(ptr)) return _eventItems;
	        if (_items.Equals(ptr)) return _items;
	        return null;
        }
    }
}
