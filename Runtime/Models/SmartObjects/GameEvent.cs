
namespace Balancy.Models.SmartObjects
{
    public class GameEvent : Balancy.Models.SmartObjects.ConditionalTemplate, IViewModel
    {
		private string _description;
		private Balancy.Models.SmartObjects.EventFinishType _finishType;
		private Localization.LocalizedString _name;
		private int _duration;
		private string _unnyIdScript;
		private UnnyObject _icon;
		
		private int _unnyPriority;
		private UnnyObject _unnyView;
		private Balancy.Models.SmartObjects.ViewPlacement _unnyPlacement;
		private bool _manualRemove;
        
		public string Description => _description;
		public Balancy.Models.SmartObjects.EventFinishType FinishType => _finishType;
		public Localization.LocalizedString Name => _name;
		public int Duration => _duration;
		public UnnyObject Icon => _icon;
		// public string Script => GetModelByUnnyId<string>(_unnyIdScript);
		
		public int UnnyPriority => _unnyPriority;
		public UnnyObject UnnyView => _unnyView;
		public Balancy.Models.SmartObjects.ViewPlacement UnnyPlacement => _unnyPlacement;

		public bool ManualRemove => _manualRemove;
        
        public override void InitData()
        {
            base.InitData();
            
			_description = GetStringParam("description");
			_finishType = (Balancy.Models.SmartObjects.EventFinishType)GetIntParam("finishType");
			_name = GetLocalizedString("name");
			_duration = GetIntParam("duration");
			_unnyIdScript = GetStringParam("unnyIdScript");
			_icon = GetObjectParam<UnnyObject>("unnyIcon");
			
			_unnyPriority = GetIntParam("unnyPriority");
			_unnyView = GetObjectParam<UnnyObject>("unnyView");
			_unnyPlacement = (Balancy.Models.SmartObjects.ViewPlacement)GetIntParam("unnyPlacement");
			_manualRemove = GetBoolParam("manualRemove");
        }
        
        public int GetSecondsLeftBeforeDeactivation()
        {
            // Guard against a dangling pointer: the native GameEvent can be
            // destroyed on profile recreation/reset while a background UI timer
            // delegate still holds this wrapper. _pointer is nulled via
            // JsonBasedObject.InvalidateByPointer in that case.
            var ptr = GetRawPointer();
            if (ptr == System.IntPtr.Zero)
                return 0;
            return LibraryMethods.Extra.balancyGameEvent_GetSecondsLeftBeforeDeactivation(ptr);
        }

        public int GetSecondsBeforeActivation(bool ignoreTriggers = true)
        {
            var ptr = GetRawPointer();
            if (ptr == System.IntPtr.Zero)
                return 0;
            return LibraryMethods.Extra.balancyGameEvent_GetSecondsBeforeActivation(ptr, ignoreTriggers);
        }

        public void StopEventManually(int cooldown)
        {
            var ptr = GetRawPointer();
            if (ptr == System.IntPtr.Zero)
                return;
            LibraryMethods.Extra.balancyGameEvent_StopEventManually(ptr, cooldown);
        }
    }
}
