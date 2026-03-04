
using Balancy.Models.SmartObjects;

namespace Balancy.Data.SmartObjects
{
    public class EventInfo : Balancy.Data.BaseData, IOwnerWithTimer
    {
	    private string _instanceId;
	    private string _unnyIdGameEvent;
		private string _offerInstanceId;
		private string _scriptInstance;
		private int _startTime;
		private int _session;
		private bool _isFinished;
        
		public string InstanceId => _instanceId;
		public Balancy.Models.SmartObjects.GameEvent GameEvent => GetModelByUnnyId<Balancy.Models.SmartObjects.GameEvent>(_unnyIdGameEvent);
		public string GameEventUnnyId => _unnyIdGameEvent;

		public string OfferInstanceId
		{
			get => _offerInstanceId;
			// set => SetStringValue("offerInstanceId", value);
		}
		public string ScriptInstance
		{
			get => _scriptInstance;
			// set => SetStringValue("scriptInstance", value);
		}
		public int StartTime
		{
			get => _startTime;
			// set => SetIntValue("startTime", value);
		}
		public int Session
		{
			get => _session;
			// set => SetIntValue("session", value);
		}

		public bool IsFinished
		{
			get => _isFinished;
		}
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("instanceId", Update_instanceId);
            InitAndSubscribeForParamChange("unnyIdGameEvent", Update_unnyIdGameEvent);
			InitAndSubscribeForParamChange("offerInstanceId", Update_offerInstanceId);
			InitAndSubscribeForParamChange("scriptInstance", Update_scriptInstance);
			InitAndSubscribeForParamChange("startTime", Update_startTime);
			InitAndSubscribeForParamChange("session", Update_session);
			InitAndSubscribeForParamChange("isFinished", Update_isFinished);
        }
        
        private void Update_instanceId() { _instanceId = GetStringParam("instanceId"); }
        private void Update_unnyIdGameEvent() { _unnyIdGameEvent = GetStringParam("unnyIdGameEvent"); }
		private void Update_offerInstanceId() { _offerInstanceId = GetStringParam("offerInstanceId"); }
		private void Update_scriptInstance() { _scriptInstance = GetStringParam("scriptInstance"); }
		private void Update_startTime() { _startTime = GetIntParam("startTime"); }
		private void Update_session() { _session = GetIntParam("session"); }
		private void Update_isFinished() { _isFinished = GetBoolParam("isFinished"); }
		
		public int GetSecondsLeftBeforeDeactivation() => LibraryMethods.Extra.balancyEventInfo_GetSecondsLeftBeforeDeactivation(GetRawPointer());
		public void StopEventManually(int cooldown) => GameEvent?.StopEventManually(cooldown);
    }
}
