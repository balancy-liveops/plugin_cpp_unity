
namespace Balancy.Data.SmartObjects
{
    public class OfferInfoBase : Balancy.Data.BaseData 
    {
	    private string _unnyIdGameEvent;
		private string _instanceId;
		private int _startTime;
		private int _session;
        
		public Balancy.Models.SmartObjects.GameEvent GameEvent => GetModelByUnnyId<Balancy.Models.SmartObjects.GameEvent>(_unnyIdGameEvent);
		
		public string InstanceId
		{
			get => _instanceId;
			// set => SetStringValue("instanceId", value);
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
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdGameEvent", Update_unnyIdGameEvent);
			InitAndSubscribeForParamChange("instanceId", Update_instanceId);
			InitAndSubscribeForParamChange("startTime", Update_startTime);
			InitAndSubscribeForParamChange("session", Update_session);
        }
        
        private void Update_unnyIdGameEvent() { _unnyIdGameEvent = GetStringParam("unnyIdGameEvent"); }
		private void Update_instanceId() { _instanceId = GetStringParam("instanceId"); }
		private void Update_startTime() { _startTime = GetIntParam("startTime"); }
		private void Update_session() { _session = GetIntParam("session"); }
		
		public int GetSecondsLeftBeforeDeactivation() => LibraryMethods.Extra.balancyOfferInfo_GetSecondsLeftBeforeDeactivation(GetRawPointer());
    }
}
