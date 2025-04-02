
namespace Balancy.Data.SmartObjects
{
    public class ItemInstance : Balancy.Data.BaseData 
    {
        
		private int _a;
		private int _c;
		private int _u;
		private string _i;
        
		public int Amount
		{
			get => _a;
			set => SetIntValue("a", value);
		}
		public int Created
		{
			get => _c;
			set => SetIntValue("c", value);
		}
		public int Updated
		{
			get => _u;
			set => SetIntValue("u", value);
		}
		
		public Balancy.Models.SmartObjects.Item Item => GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(_i);
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("a", Update_a);
			InitAndSubscribeForParamChange("c", Update_c);
			InitAndSubscribeForParamChange("u", Update_u);
			InitAndSubscribeForParamChange("i", Update_i);
        }
        
		private void Update_a() { _a = GetIntParam("a"); }
		private void Update_c() { _c = GetIntParam("c"); }
		private void Update_u() { _u = GetIntParam("u"); }
		private void Update_i() { _i = GetStringParam("i"); }
    }
}
