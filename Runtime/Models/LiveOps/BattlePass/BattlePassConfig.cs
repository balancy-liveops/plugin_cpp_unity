namespace Balancy.Models.LiveOps.BattlePass
{
    public class BattlePassConfig : Balancy.Models.BaseModel 
    {
        private Localization.LocalizedString _name;
        private string _unnyIdProgressionItem;
        private Balancy.Models.LiveOps.BattlePass.ProgressionType _type;
        private int[] _scores;
        private string _unnyIdBonusReward;
        private string[] _unnyIdRewards;
        
        public Localization.LocalizedString Name => _name;
        public Balancy.Models.SmartObjects.Item ProgressionItem => GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(_unnyIdProgressionItem);
        public string ProgressionItemUnnyId => _unnyIdProgressionItem;
        public Balancy.Models.LiveOps.BattlePass.ProgressionType Type => _type;
        public int[] Scores => _scores;
        public Balancy.Models.LiveOps.BattlePass.RewardLine BonusReward => GetModelByUnnyId<Balancy.Models.LiveOps.BattlePass.RewardLine>(_unnyIdBonusReward);
        public string BonusRewardUnnyId => _unnyIdBonusReward;
        public Balancy.Models.LiveOps.BattlePass.RewardLine[] Rewards => GetModelsByUnnyIds<Balancy.Models.LiveOps.BattlePass.RewardLine>(_unnyIdRewards);
        public string[] RewardsUnnyIds => _unnyIdRewards;
        
        public override void InitData()
        {
            base.InitData();
            
            _name = GetLocalizedString("name");
            _unnyIdProgressionItem = GetStringParam("unnyIdProgressionItem");
            _type = (Balancy.Models.LiveOps.BattlePass.ProgressionType)GetIntParam("type");
            _scores = GetIntArrayParam("scores");
            _unnyIdBonusReward = GetStringParam("unnyIdBonusReward");
            _unnyIdRewards = GetStringArrayParam("unnyIdRewards");
        }
    }
}