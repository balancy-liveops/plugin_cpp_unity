
namespace Balancy.Models.LiveOps.Store
{
    public class SlotLogicCooldown : Balancy.Models.LiveOps.Store.SlotLogicBase 
    {
        
		private int _cooldown;
        
        
		public int Cooldown => _cooldown;
        
        public override void InitData()
        {
            base.InitData();
            
			_cooldown = GetIntParam("cooldown");
        }
        
    }
}
