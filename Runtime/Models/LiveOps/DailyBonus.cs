
namespace Balancy.Models.LiveOps
{
    public class DailyBonus : Balancy.Models.BaseModel 
    {
		private Balancy.Models.SmartObjects.Reward[] _rewards;
		private Balancy.Models.SmartObjects.Reward  _bonusReward;
		private Balancy.Models.LiveOps.DailyBonusType _type;
		private string _name;

		public Balancy.Models.SmartObjects.Reward[] Rewards => _rewards;
		public Balancy.Models.SmartObjects.Reward BonusReward => _bonusReward;
		public Balancy.Models.LiveOps.DailyBonusType Type => _type;
		public string Name => _name;
        
        public override void InitData()
        {
            base.InitData();
            
            _rewards = GetObjectArrayParam<Balancy.Models.SmartObjects.Reward>("rewards");
            _bonusReward = GetObjectParam<Balancy.Models.SmartObjects.Reward>("bonusReward");
			_type = (Balancy.Models.LiveOps.DailyBonusType)GetIntParam("type");
			_name = GetStringParam("name");
        }
    }
}
