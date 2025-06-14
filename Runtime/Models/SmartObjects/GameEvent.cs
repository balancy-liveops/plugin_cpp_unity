
namespace Balancy.Models.SmartObjects
{
    public class GameEvent : Balancy.Models.BaseModel, IViewModel
    {
		private string _unnyIdCondition;
		private string _description;
		private Balancy.Models.SmartObjects.EventFinishType _finishType;
		private Localization.LocalizedString _name;
		private int _duration;
		private string _unnyIdScript;
		private UnnyObject _icon;
		
		private int _unnyPriority;
		private UnnyObject _unnyView;
		private Balancy.Models.SmartObjects.ViewPlacement _unnyPlacement;
        
		// public Balancy.Models.SmartObjects.Conditions.Logic Condition => GetModelByUnnyId<Balancy.Models.SmartObjects.Conditions.Logic>(_unnyIdCondition);
		public string Description => _description;
		public Balancy.Models.SmartObjects.EventFinishType FinishType => _finishType;
		public Localization.LocalizedString Name => _name;
		public int Duration => _duration;
		public UnnyObject Icon => _icon;
		// public string Script => GetModelByUnnyId<string>(_unnyIdScript);
		
		public int UnnyPriority => _unnyPriority;
		public UnnyObject UnnyView => _unnyView;
		public Balancy.Models.SmartObjects.ViewPlacement UnnyPlacement => _unnyPlacement;
        
        public override void InitData()
        {
            base.InitData();
            
			_unnyIdCondition = GetStringParam("unnyIdCondition");
			_description = GetStringParam("description");
			_finishType = (Balancy.Models.SmartObjects.EventFinishType)GetIntParam("finishType");
			_name = GetLocalizedString("name");
			_duration = GetIntParam("duration");
			_unnyIdScript = GetStringParam("unnyIdScript");
			_icon = GetObjectParam<UnnyObject>("unnyIcon");
			
			_unnyPriority = GetIntParam("unnyPriority");
			_unnyView = GetObjectParam<UnnyObject>("unnyView");
			_unnyPlacement = (Balancy.Models.SmartObjects.ViewPlacement)GetIntParam("unnyPlacement");
        }
        
        public int GetSecondsLeftBeforeDeactivation() => LibraryMethods.Extra.balancyGameEvent_GetSecondsLeftBeforeDeactivation(GetRawPointer());
        public int GetSecondsBeforeActivation(bool ignoreTriggers = true) => LibraryMethods.Extra.balancyGameEvent_GetSecondsBeforeActivation(GetRawPointer(), ignoreTriggers);
    }
}
