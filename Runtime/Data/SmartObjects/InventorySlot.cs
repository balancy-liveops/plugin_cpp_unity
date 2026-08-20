
namespace Balancy.Data.SmartObjects
{
    public class InventorySlot : Balancy.Data.BaseData 
    {
        
		private Balancy.Data.SmartObjects.ItemInstance _item;
        
        
		public Balancy.Data.SmartObjects.ItemInstance Item => _item;

		/// <summary>
		/// True when this slot actually holds an item. <see cref="Item"/> is never null —
		/// it is a wrapper that re-binds as the slot is filled and emptied — so use this
		/// (or <c>Item.IsValid</c>) instead of a null check.
		/// </summary>
		public bool HasItem => _item != null && _item.IsValid;
        
        public override void InitData()
        {
            base.InitData();
            
			_item = GetBaseDataParam<Balancy.Data.SmartObjects.ItemInstance>("i");
        }
        
    }
}
