
namespace Balancy.Models.SmartObjects.Analytics
{
    public class ABTestVariant : Balancy.Models.BaseModel 
    {
        
		private string _name;
		private int _weight;
        
        
		public string Name => _name;
		public int Weight => _weight;
        
        public override void InitData()
        {
            base.InitData();
            
			_name = GetStringParam("name");
			_weight = GetIntParam("weight");
        }
        
    }
}
