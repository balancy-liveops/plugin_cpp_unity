
namespace Balancy.Data.SmartObjects
{
    public class Inventory : Balancy.Data.BaseData 
    {
        
		private SmartList<Balancy.Data.SmartObjects.InventorySlot> _slots;
        
        
		public SmartList<Balancy.Data.SmartObjects.InventorySlot> Slots => _slots;
        
        public override void InitData()
        {
            base.InitData();
            
			_slots = GetListBaseDataParam<Balancy.Data.SmartObjects.InventorySlot>("slots");
        }
        
    }
}
