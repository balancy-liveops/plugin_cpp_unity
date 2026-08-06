
using System;

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
        public bool HasDynamicReward() => DynamicReward?.HasValue == true;

        public void GetDynamicReward(Action<Reward> callBack)
        {
            var dynamicReward = DynamicReward;
            if (dynamicReward?.HasValue != true)
            {
                callBack?.Invoke(null);
                return;
            }

            var input = ToJsonString(2, false);
            var inputJson = "{\"Input\":" + (string.IsNullOrEmpty(input) ? "null" : input) + "}";
            var instanceId = dynamicReward.Launch(null, inputJson, (exitPort, outputsJson) =>
            {
                var reward = API.VisualScripting.CreateTempModelFromScriptOutput<Reward>(outputsJson, "Output");
                callBack?.Invoke(reward);
            });

            if (string.IsNullOrEmpty(instanceId))
                callBack?.Invoke(null);
        }

        public void GetReward(Action<Reward> callBack)
        {
            GetDynamicReward(reward => callBack?.Invoke(reward ?? Reward));
        }

        public int GetAdsWatched() => LibraryMethods.Extra.balancyStoreItem_GetAdsWatched(GetRawPointer());
        public void AdWasWatched() => LibraryMethods.Extra.balancyStoreItem_AdWasWatched(GetRawPointer());
        public bool HaveEnoughResources() => LibraryMethods.Extra.balancyStoreItem_HaveEnoughResources(GetRawPointer());
    }
}
