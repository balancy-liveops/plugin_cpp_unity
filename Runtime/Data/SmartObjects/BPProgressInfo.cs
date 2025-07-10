namespace Balancy.Data.SmartObjects
{
    public class BPProgressInfo : Balancy.Data.BaseData 
    {
        private string _unnyIdReward;
        private bool _available;
        // private int[] _progress;
        
        public Balancy.Models.LiveOps.BattlePass.RewardLine Reward => GetModelByUnnyId<Balancy.Models.LiveOps.BattlePass.RewardLine>(_unnyIdReward);
        public string RewardUnnyId => _unnyIdReward;
        
        public bool Available
        {
            get => _available;
            // set => SetBoolValue("available", value);
        }
        
        // public int[] Progress => _progress;
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdReward", Update_unnyIdReward);
            InitAndSubscribeForParamChange("available", Update_available);
            // InitAndSubscribeForParamChange("progress", Update_progress);
        }
        
        private void Update_unnyIdReward() { _unnyIdReward = GetStringParam("unnyIdReward"); }
        private void Update_available() { _available = GetBoolParam("available"); }
        // private void Update_progress() { _progress = GetIntArrayParam("progress"); }
        
        public BattlePassRewardStatus GetRewardStatus(int index)
        {
            var status = LibraryMethods.API.balancyBattlePass_getRewardStatus(GetRawPointer(), index);
            return (BattlePassRewardStatus)status;
        }
        
        public bool ClaimReward(int index)
        {
            return LibraryMethods.API.balancyBattlePass_claimReward(GetRawPointer(), index);
        }
        
        public void SetReward(Balancy.Models.LiveOps.BattlePass.RewardLine reward)
        {
            SetStringValue("unnyIdReward", reward?.UnnyId ?? "");
        }
    }
}