namespace Balancy.Models.LiveOps.BattlePass
{
    public class RewardLine : Balancy.Models.BaseModel 
    {
        private Localization.LocalizedString _name;
        private string _unnyIdAccessItem;
        private bool _removeAccessItemOnStart;
        private Balancy.Models.SmartObjects.ItemWithAmount[] _rewards;
        
        public Localization.LocalizedString Name => _name;
        public Balancy.Models.SmartObjects.Item AccessItem => GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(_unnyIdAccessItem);
        public string AccessItemUnnyId => _unnyIdAccessItem;
        public bool RemoveAccessItemOnStart => _removeAccessItemOnStart;
        public Balancy.Models.SmartObjects.ItemWithAmount[] Rewards => _rewards;
        
        public override void InitData()
        {
            base.InitData();
            
            _name = GetLocalizedString("name");
            _unnyIdAccessItem = GetStringParam("unnyIdAccessItem");
            _removeAccessItemOnStart = GetBoolParam("removeAccessItemOnStart");
            _rewards = GetObjectArrayParam<Balancy.Models.SmartObjects.ItemWithAmount>("rewards");
        }
    }
}