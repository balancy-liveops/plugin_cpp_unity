
namespace Balancy.Data.SmartObjects
{
    public class InventorySlot : Balancy.Data.BaseData 
    {
        
		private Balancy.Data.SmartObjects.ItemInstance _item;
        
        
		public Balancy.Data.SmartObjects.ItemInstance Item => _item;
        
        public override void InitData()
        {
            base.InitData();
            
			_item = GetBaseDataParam<Balancy.Data.SmartObjects.ItemInstance>("item");
        }
        
    }
}
