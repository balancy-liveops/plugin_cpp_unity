
namespace Balancy.Data.SmartObjects
{
    public class AdInfo : Balancy.Data.BaseData 
    {
        
		private float _revenue;
		private int _count;
		private float _revenueToday;
		private int _countToday;
        
        
		public float Revenue
		{
			get => _revenue;
			set => SetFloatValue("revenue", value);
		}
		public int Count
		{
			get => _count;
			set => SetIntValue("count", value);
		}
		public float RevenueToday
		{
			get => _revenueToday;
			set => SetFloatValue("revenueToday", value);
		}
		public int CountToday
		{
			get => _countToday;
			set => SetIntValue("countToday", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("revenue", Update_revenue);
			InitAndSubscribeForParamChange("count", Update_count);
			InitAndSubscribeForParamChange("revenueToday", Update_revenueToday);
			InitAndSubscribeForParamChange("countToday", Update_countToday);
        }
        
		private void Update_revenue() { _revenue = GetFloatParam("revenue"); }
		private void Update_count() { _count = GetIntParam("count"); }
		private void Update_revenueToday() { _revenueToday = GetFloatParam("revenueToday"); }
		private void Update_countToday() { _countToday = GetIntParam("countToday"); }
    }
}
