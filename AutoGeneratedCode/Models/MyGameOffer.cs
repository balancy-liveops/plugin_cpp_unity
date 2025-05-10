
namespace Balancy.Models
{
    public class MyGameOffer : Balancy.Models.SmartObjects.GameOffer 
    {
        
		private int _priority;
		private string _placement;
		private string _view;
        
        
		public int Priority => _priority;
		public string Placement => _placement;
		public string View => _view;
        
        public override void InitData()
        {
            base.InitData();
            
			_priority = GetIntParam("priority");
			_placement = GetStringParam("placement");
			_view = GetStringParam("view");
        }
        
    }
}
