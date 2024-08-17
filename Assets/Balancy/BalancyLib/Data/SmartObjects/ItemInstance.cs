
namespace Balancy.Data.SmartObjects
{
    public class ItemInstance : Balancy.Data.BaseData 
    {
        
		private int _a;
		private int _c;
		private int _u;
        
        
		public int A
		{
			get => _a;
			set => SetIntValue("a", value);
		}
		public int C
		{
			get => _c;
			set => SetIntValue("c", value);
		}
		public int U
		{
			get => _u;
			set => SetIntValue("u", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("a", Update_a);
			InitAndSubscribeForParamChange("c", Update_c);
			InitAndSubscribeForParamChange("u", Update_u);
        }
        
		private void Update_a() { _a = GetIntParam("a"); }
		private void Update_c() { _c = GetIntParam("c"); }
		private void Update_u() { _u = GetIntParam("u"); }
    }
}
