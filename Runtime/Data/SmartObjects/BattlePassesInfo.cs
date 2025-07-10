using System;

namespace Balancy.Data.SmartObjects
{
    public class BattlePassesInfo : Balancy.Data.BaseData 
    {
        private SmartList<Balancy.Data.SmartObjects.BattlePassInfo> _battlePasses;
        
        public SmartList<Balancy.Data.SmartObjects.BattlePassInfo> BattlePasses => _battlePasses;
        
        public override void InitData()
        {
            base.InitData();
            
            _battlePasses = GetListBaseDataParam<Balancy.Data.SmartObjects.BattlePassInfo>("battlePasses");
        }
        
        public Balancy.Data.SmartObjects.BattlePassInfo FindBattlePassInfo(IntPtr ptr) => FindElementInList(_battlePasses, ptr);
        
        public Balancy.Data.SmartObjects.BattlePassInfo FindBattlePassInfo(Balancy.Models.LiveOps.BattlePass.GameEvent gameEvent)
        {
            if (gameEvent == null) return null;
            
            foreach (var battlePassInfo in _battlePasses)
            {
                if (battlePassInfo.GameEventUnnyId == gameEvent.UnnyId)
                    return battlePassInfo;
            }
            return null;
        }
    }
}