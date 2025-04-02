
namespace Balancy.Models.LiveOps.Store
{
    public class SlotLogicMultiplier : Balancy.Models.LiveOps.Store.SlotLogicBase 
    {
        
		private float _multiplier;
        
        
		public float Multiplier => _multiplier;
        
        public override void InitData()
        {
            base.InitData();
            
			_multiplier = GetFloatParam("multiplier");
        }
        
    }
}
