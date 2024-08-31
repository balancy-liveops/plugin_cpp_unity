
namespace Balancy.Models.SmartObjects
{
    public class GameEvent : Balancy.Models.BaseModel 
    {
        
		private string _unnyIdCondition;
		private string _description;
		private Balancy.Models.SmartObjects.EventFinishType _finishType;
		private Localization.LocalizedString _name;
		private int _duration;
		private string _unnyIdScript;
        
        
		// public Balancy.Models.SmartObjects.Conditions.Logic Condition => GetModelByUnnyId<Balancy.Models.SmartObjects.Conditions.Logic>(_unnyIdCondition);
		public string Description => _description;
		public Balancy.Models.SmartObjects.EventFinishType FinishType => _finishType;
		public Localization.LocalizedString Name => _name;
		public int Duration => _duration;
		// public string Script => GetModelByUnnyId<string>(_unnyIdScript);
        
        public override void InitData()
        {
            base.InitData();
            
			_unnyIdCondition = GetStringParam("unnyIdCondition");
			_description = GetStringParam("description");
			_finishType = (Balancy.Models.SmartObjects.EventFinishType)GetIntParam("finishType");
			_name = GetLocalizedString("name");
			_duration = GetIntParam("duration");
			_unnyIdScript = GetStringParam("unnyIdScript");
        }
        
    }
}
