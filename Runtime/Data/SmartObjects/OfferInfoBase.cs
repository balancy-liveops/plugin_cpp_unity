
using Balancy.Models.SmartObjects;

namespace Balancy.Data.SmartObjects
{
    public class OfferInfoBase : Balancy.Data.BaseData, IOwnerWithTimer
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
		
		public int GetSecondsLeftBeforeDeactivation()
		{
			// The native object may have been destroyed (profile recreation /
			// reset). _pointer is nulled via JsonBasedObject.InvalidateByPointer
			// when that happens. Guard here so a background timer delegate still
			// holding this wrapper does not P/Invoke a dangling pointer.
			var ptr = GetRawPointer();
			if (ptr == System.IntPtr.Zero)
				return 0;
			return LibraryMethods.Extra.balancyOfferInfo_GetSecondsLeftBeforeDeactivation(ptr);
		}
		
		public void Activate()
		{
			var ptr = GetRawPointer();
			if (ptr == System.IntPtr.Zero)
				return;
			LibraryMethods.Extra.balancyOfferInfo_Activate(ptr);
		}

		public bool DeactivateOffer()
		{
			var ptr = GetRawPointer();
			if (ptr == System.IntPtr.Zero)
				return false;
			return LibraryMethods.Extra.balancyOfferInfo_DeactivateOffer(ptr);
		}
    }
}
