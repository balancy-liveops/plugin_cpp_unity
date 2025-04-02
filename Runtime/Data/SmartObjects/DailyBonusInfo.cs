
using Balancy.Models.SmartObjects;

namespace Balancy.Data.SmartObjects
{
    public class DailyBonusInfo : Balancy.Data.BaseData 
    {
	    private string _unnyIdDailyBonus;
		private int _dailyRewardCollected;
		private int _dailyRewardCollectTime;
        
		public Balancy.Models.LiveOps.DailyBonus DailyBonus => GetModelByUnnyId<Balancy.Models.LiveOps.DailyBonus>(_unnyIdDailyBonus);
		
		public int DailyRewardCollected
		{
			get => _dailyRewardCollected;
			// set => SetIntValue("dailyRewardCollected", value);
		}
		public int DailyRewardCollectTime
		{
			get => _dailyRewardCollectTime;
			// set => SetIntValue("dailyRewardCollectTime", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdDailyBonus", Update_unnyIdDailyBonus);
			InitAndSubscribeForParamChange("dailyRewardCollected", Update_dailyRewardCollected);
			InitAndSubscribeForParamChange("dailyRewardCollectTime", Update_dailyRewardCollectTime);
        }
        
        private void Update_unnyIdDailyBonus() { _unnyIdDailyBonus = GetStringParam("unnyIdDailyBonus"); }
		private void Update_dailyRewardCollected() { _dailyRewardCollected = GetIntParam("dailyRewardCollected"); }
		private void Update_dailyRewardCollectTime() { _dailyRewardCollectTime = GetIntParam("dailyRewardCollectTime"); }

		public Reward[] GetAllRewards() => DailyBonus?.Rewards;
		
		public bool IsNextRewardBonus()
		{
			var allRewards = GetAllRewards();
			return (GetNextRewardNumber() > allRewards.Length);
		}
		
		public Reward GetNextReward()
		{
			if (IsNextRewardBonus())
				return DailyBonus?.BonusReward;
			
			var allRewards = GetAllRewards();
			return allRewards[GetNextRewardNumber() - 1];
		}
		
		public int GetNextRewardNumber() => DailyRewardCollected + 1;
		
		public Reward ClaimNextReward()
		{
			var reward = GetNextReward();
			if (LibraryMethods.API.balancyDailyBonus_claimNextReward(GetRawPointer()))
				return reward;
			return null;
		}
		
		public int GetSecondsTillTheNextReward() => LibraryMethods.API.balancyDailyBonus_getSecondsTillTheNextReward(GetRawPointer());
		public bool CanClaimNextReward() => LibraryMethods.API.balancyDailyBonus_canClaimNextReward(GetRawPointer());
    }
}
