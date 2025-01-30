using System;

namespace Balancy.Data.SmartObjects
{
    public class LiveOpsInfo : Balancy.Data.BaseData 
    {
	    private SmartList<Balancy.Data.SmartObjects.DailyBonusInfo> _dailyBonusInfos;
        
        public SmartList<Balancy.Data.SmartObjects.DailyBonusInfo> DailyBonusInfos => _dailyBonusInfos;
        
        public override void InitData()
        {
            base.InitData();
            
            _dailyBonusInfos = GetListBaseDataParam<Balancy.Data.SmartObjects.DailyBonusInfo>("dailyBonusInfos");
        }
        
        public Balancy.Data.SmartObjects.DailyBonusInfo FindDailyBonusInfo(IntPtr ptr) => FindElementInList(_dailyBonusInfos, ptr);
    }
}
