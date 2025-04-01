
namespace Balancy.Models.LiveOps.Store
{
    public class SlotLogicPeriod : Balancy.Models.LiveOps.Store.SlotLogicBase 
    {
        
		private int _limit;
		private int _period;
        
        
		public int Limit => _limit;
		public int Period => _period;
        
        public override void InitData()
        {
            base.InitData();
            
			_limit = GetIntParam("limit");
			_period = GetIntParam("period");
        }
        
    }
}
