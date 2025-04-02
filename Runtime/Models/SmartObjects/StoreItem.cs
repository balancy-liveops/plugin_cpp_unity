
namespace Balancy.Models.SmartObjects
{
    public class StoreItem : Balancy.Models.BaseModel 
    {
        
		private string _unnyIdDynamicReward;
		private Balancy.Models.SmartObjects.Price _price;
		private Balancy.Models.SmartObjects.Reward _reward;
		private Localization.LocalizedString _name;
		private UnnyObject _sprite;
        
        
		// public Balancy.Models.VisualScripting.ScriptNode DynamicReward => GetModelByUnnyId<Balancy.Models.VisualScripting.ScriptNode>(_unnyIdDynamicReward);
		public Balancy.Models.SmartObjects.Price Price => _price;
		public Balancy.Models.SmartObjects.Reward Reward => _reward;
		public Localization.LocalizedString Name => _name;
		public UnnyObject Sprite => _sprite;
        
        public override void InitData()
        {
            base.InitData();
            
			_unnyIdDynamicReward = GetStringParam("unnyIdDynamicReward");
			_price = GetObjectParam<Balancy.Models.SmartObjects.Price>("price");
			_reward = GetObjectParam<Balancy.Models.SmartObjects.Reward>("reward");
			_name = GetLocalizedString("name");
			_sprite = GetObjectParam<UnnyObject>("sprite");
        }
        
    }
}
