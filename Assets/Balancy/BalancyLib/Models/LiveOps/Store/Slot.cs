
namespace Balancy.Models.LiveOps.Store
{
    public class Slot : Balancy.Models.BaseModel 
    {
        
		private string[] _unnyIdLogic;
		private string _unnyIdStoreItem;
		private Balancy.Models.LiveOps.Store.SlotType _type;
        
        
		public Balancy.Models.LiveOps.Store.SlotLogicBase[] Logic => GetModelsByUnnyIds<Balancy.Models.LiveOps.Store.SlotLogicBase>(_unnyIdLogic);
		public Balancy.Models.SmartObjects.StoreItem StoreItem => GetModelByUnnyId<Balancy.Models.SmartObjects.StoreItem>(_unnyIdStoreItem);
		public Balancy.Models.LiveOps.Store.SlotType Type => _type;
        
        public override void InitData()
        {
            base.InitData();
            
			_unnyIdLogic = GetStringArrayParam("unnyIdLogic");
			_unnyIdStoreItem = GetStringParam("unnyIdStoreItem");
			_type = (Balancy.Models.LiveOps.Store.SlotType)GetIntParam("type");
        }
        
    }
}
