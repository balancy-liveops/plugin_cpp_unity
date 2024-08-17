
namespace Balancy.Data.SmartObjects
{
    public class LiveOpsInfo : Balancy.Data.BaseData 
    {
        
		private int _dailyRewardCollected;
		private int _dailyRewardCollectTime;
        
        
		public int DailyRewardCollected
		{
			get => _dailyRewardCollected;
			set => SetIntValue("dailyRewardCollected", value);
		}
		public int DailyRewardCollectTime
		{
			get => _dailyRewardCollectTime;
			set => SetIntValue("dailyRewardCollectTime", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("dailyRewardCollected", Update_dailyRewardCollected);
			InitAndSubscribeForParamChange("dailyRewardCollectTime", Update_dailyRewardCollectTime);
        }
        
		private void Update_dailyRewardCollected() { _dailyRewardCollected = GetIntParam("dailyRewardCollected"); }
		private void Update_dailyRewardCollectTime() { _dailyRewardCollectTime = GetIntParam("dailyRewardCollectTime"); }
    }
}
