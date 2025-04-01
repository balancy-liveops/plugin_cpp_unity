
namespace Balancy.Models.SmartObjects
{
    public class GameOfferGroup : Balancy.Models.BaseModel 
    {
        
		private Balancy.Models.SmartObjects.OfferGroupType _type;
		private string[] _unnyIdStoreItems;
		private UnnyObject _icon;
		private Localization.LocalizedString _name;
		private int _duration;
		private bool _wait;
        
        
		public Balancy.Models.SmartObjects.OfferGroupType Type => _type;
		public Balancy.Models.SmartObjects.StoreItem[] StoreItems => GetModelsByUnnyIds<Balancy.Models.SmartObjects.StoreItem>(_unnyIdStoreItems);
		public UnnyObject Icon => _icon;
		public Localization.LocalizedString Name => _name;
		public int Duration => _duration;
		public bool Wait => _wait;
        
        public override void InitData()
        {
            base.InitData();
            
			_type = (Balancy.Models.SmartObjects.OfferGroupType)GetIntParam("type");
			_unnyIdStoreItems = GetStringArrayParam("unnyIdStoreItems");
			_icon = GetObjectParam<UnnyObject>("icon");
			_name = GetLocalizedString("name");
			_duration = GetIntParam("duration");
			_wait = GetBoolParam("wait");
        }
        
    }
}
