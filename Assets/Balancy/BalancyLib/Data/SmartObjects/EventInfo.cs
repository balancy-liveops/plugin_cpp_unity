
namespace Balancy.Data.SmartObjects
{
    public class EventInfo : Balancy.Data.BaseData 
    {
	    private string _unnyIdGameEvent;
		private string _offerInstanceId;
		private string _scriptInstance;
		private int _startTime;
		private int _session;
        
		public Balancy.Models.SmartObjects.GameEvent GameEvent => GetModelByUnnyId<Balancy.Models.SmartObjects.GameEvent>(_unnyIdGameEvent);
		
		public string OfferInstanceId
		{
			get => _offerInstanceId;
			set => SetStringValue("offerInstanceId", value);
		}
		public string ScriptInstance
		{
			get => _scriptInstance;
			set => SetStringValue("scriptInstance", value);
		}
		public int StartTime
		{
			get => _startTime;
			set => SetIntValue("startTime", value);
		}
		public int Session
		{
			get => _session;
			set => SetIntValue("session", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdGameEvent", Update_unnyIdGameEvent);
			InitAndSubscribeForParamChange("offerInstanceId", Update_offerInstanceId);
			InitAndSubscribeForParamChange("scriptInstance", Update_scriptInstance);
			InitAndSubscribeForParamChange("startTime", Update_startTime);
			InitAndSubscribeForParamChange("session", Update_session);
        }
        
        private void Update_unnyIdGameEvent() { _unnyIdGameEvent = GetStringParam("unnyIdGameEvent"); }
		private void Update_offerInstanceId() { _offerInstanceId = GetStringParam("offerInstanceId"); }
		private void Update_scriptInstance() { _scriptInstance = GetStringParam("scriptInstance"); }
		private void Update_startTime() { _startTime = GetIntParam("startTime"); }
		private void Update_session() { _session = GetIntParam("session"); }
    }
}
