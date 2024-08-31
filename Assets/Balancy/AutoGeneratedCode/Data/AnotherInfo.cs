
namespace Balancy.Data
{
    public class AnotherInfo : Balancy.Data.BaseData 
    {
        
		private string _name;
        
        
		public string Name
		{
			get => _name;
			set => SetStringValue("name", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("name", Update_name);
        }

        private void Update_name()
        {
	        _name = GetStringParam("name");
        }
    }
}
