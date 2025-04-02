
namespace Balancy.Models.SmartObjects
{
    public class GameOffer : Balancy.Models.BaseModel 
    {
        
		private UnnyObject _sprite;
		private string _unnyIdStoreItem;
		private Localization.LocalizedString _description;
		private Localization.LocalizedString _name;
		private int _duration;
		private UnnyObject _icon;
		private int _limit;
		private bool _wait;
        
        
		public UnnyObject Sprite => _sprite;
		public Balancy.Models.SmartObjects.StoreItem StoreItem => GetModelByUnnyId<Balancy.Models.SmartObjects.StoreItem>(_unnyIdStoreItem);
		public Localization.LocalizedString Description => _description;
		public Localization.LocalizedString Name => _name;
		public int Duration => _duration;
		public UnnyObject Icon => _icon;
		public int Limit => _limit;
		public bool Wait => _wait;
        
        public override void InitData()
        {
            base.InitData();
            
			_sprite = GetObjectParam<UnnyObject>("sprite");
			_unnyIdStoreItem = GetStringParam("unnyIdStoreItem");
			_description = GetLocalizedString("description");
			_name = GetLocalizedString("name");
			_duration = GetIntParam("duration");
			_icon = GetObjectParam<UnnyObject>("icon");
			_limit = GetIntParam("limit");
			_wait = GetBoolParam("wait");
        }
        
    }
}
