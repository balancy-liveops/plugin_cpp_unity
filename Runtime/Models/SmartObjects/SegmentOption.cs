
namespace Balancy.Models.SmartObjects
{
    public class SegmentOption : Balancy.Models.BaseModel 
    {
        
		private string _name;
		private string _unnyIdCondition;
		private Balancy.Models.SmartObjects.SegmentType _type;
		private string _description;
        
        
		public string Name => _name;
		// public Balancy.Models.SmartObjects.Conditions.Logic Condition => GetModelByUnnyId<Balancy.Models.SmartObjects.Conditions.Logic>(_unnyIdCondition);
		public Balancy.Models.SmartObjects.SegmentType Type => _type;
		public string Description => _description;
        
        public override void InitData()
        {
            base.InitData();
            
			_name = GetStringParam("name");
			_unnyIdCondition = GetStringParam("unnyIdCondition");
			_type = (Balancy.Models.SmartObjects.SegmentType)GetIntParam("type");
			_description = GetStringParam("description");
        }
        
    }
}
