
namespace Balancy.Models.SmartObjects
{
    public class StoreItem : Balancy.Models.BaseModel 
    {
		private string _unnyIdDynamicReward;
		private Balancy.Models.SmartObjects.Price _price;
		private Balancy.Models.SmartObjects.Reward _reward;
		private Localization.LocalizedString _name;
		private UnnyObject _sprite;
		private UnnyObject _unnyView;
        
        
		public Balancy.ScriptRef DynamicReward => GetScriptRef(_unnyIdDynamicReward);
		public Balancy.Models.SmartObjects.Price Price => _price;
		public Balancy.Models.SmartObjects.Reward Reward => _reward;
		public Localization.LocalizedString Name => _name;
		public UnnyObject Sprite => _sprite;
		public UnnyObject UnnyView => _unnyView;
        
        public override void InitData()
        {
            base.InitData();
            
			_unnyIdDynamicReward = GetStringParam("unnyIdDynamicReward");
			_price = GetObjectParam<Balancy.Models.SmartObjects.Price>("price");
			_reward = GetObjectParam<Balancy.Models.SmartObjects.Reward>("reward");
			_name = GetLocalizedString("name");
			_sprite = GetObjectParam<UnnyObject>("sprite");
			_unnyView = GetObjectParam<UnnyObject>("unnyView");
        }
        
        public bool IsFree() => Price.IsFree();
        public bool IsInApp() => Price.IsInApp();
        public bool IsAdsWatching() => Price.IsAdsWatching();

        public int GetAdsWatched() => LibraryMethods.Extra.balancyStoreItem_GetAdsWatched(GetRawPointer());
        public void AdWasWatched() => LibraryMethods.Extra.balancyStoreItem_AdWasWatched(GetRawPointer());
        public bool HaveEnoughResources() => LibraryMethods.Extra.balancyStoreItem_HaveEnoughResources(GetRawPointer());
        public float Multiplier => LibraryMethods.Extra.balancyStoreItem_GetMultiplier(GetRawPointer());
        public bool HasMultiplier => LibraryMethods.Extra.balancyStoreItem_HasMultiplier(GetRawPointer());
    }
}
