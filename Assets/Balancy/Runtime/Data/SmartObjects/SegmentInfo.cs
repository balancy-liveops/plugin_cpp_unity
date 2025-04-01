
namespace Balancy.Data.SmartObjects
{
    public class SegmentInfo : Balancy.Data.BaseData
    {
	    private string _unnyIdSegment;
        
	    public Balancy.Models.SmartObjects.SegmentOption Segment => GetModelByUnnyId<Balancy.Models.SmartObjects.SegmentOption>(_unnyIdSegment);
		private int _lastIn;
		private int _lastInSession;
		private int _lastOut;
		private bool _isIn;
        
        
		public int LastIn
		{
			get => _lastIn;
			// set => SetIntValue("lastIn", value);
		}
		public int LastInSession
		{
			get => _lastInSession;
			// set => SetIntValue("lastInSession", value);
		}
		public int LastOut
		{
			get => _lastOut;
			// set => SetIntValue("lastOut", value);
		}
		public bool IsIn
		{
			get => _isIn;
			// set => SetBoolValue("isIn", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdSegment", Update_unnyIdSegment);
			InitAndSubscribeForParamChange("lastIn", Update_lastIn);
			InitAndSubscribeForParamChange("lastInSession", Update_lastInSession);
			InitAndSubscribeForParamChange("lastOut", Update_lastOut);
			InitAndSubscribeForParamChange("isIn", Update_isIn);
        }
        
        private void Update_unnyIdSegment() { _unnyIdSegment = GetStringParam("unnyIdSegment"); }
		private void Update_lastIn() { _lastIn = GetIntParam("lastIn"); }
		private void Update_lastInSession() { _lastInSession = GetIntParam("lastInSession"); }
		private void Update_lastOut() { _lastOut = GetIntParam("lastOut"); }
		private void Update_isIn() { _isIn = GetBoolParam("isIn"); }
    }
}
