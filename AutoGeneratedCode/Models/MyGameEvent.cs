
namespace Balancy.Models
{
    public class MyGameEvent : Balancy.Models.SmartObjects.GameEvent 
    {
        
		private UnnyObject _icon;
		private int _priority;
		private string _view;
		private string _placement;
        
        
		public UnnyObject Icon => _icon;
		public int Priority => _priority;
		public string View => _view;
		public string Placement => _placement;
        
        public override void InitData()
        {
            base.InitData();
            
			_icon = GetObjectParam<UnnyObject>("icon");
			_priority = GetIntParam("priority");
			_view = GetStringParam("view");
			_placement = GetStringParam("placement");
        }
        
    }
}
