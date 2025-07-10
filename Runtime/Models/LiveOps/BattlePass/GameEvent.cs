namespace Balancy.Models.LiveOps.BattlePass
{
    public class GameEvent : Balancy.Models.SmartObjects.GameEvent 
    {
        private string _unnyIdConfig;
        
        public Balancy.Models.LiveOps.BattlePass.BattlePassConfig Config => GetModelByUnnyId<Balancy.Models.LiveOps.BattlePass.BattlePassConfig>(_unnyIdConfig);
        public string ConfigUnnyId => _unnyIdConfig;
        
        public override void InitData()
        {
            base.InitData();
            
            _unnyIdConfig = GetStringParam("unnyIdConfig");
        }
    }
}