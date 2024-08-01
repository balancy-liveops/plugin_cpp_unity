
namespace Balancy.Models
{
    public class MyItem : Balancy.Models.SmartObjects.Item 
    {
        
		private Balancy.Models.VectorType _position;
		private string _unnyIdItemLink;
        
        
		public Balancy.Models.VectorType Position => _position;
		public Balancy.Models.SmartObjects.Item ItemLink => GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(_unnyIdItemLink);
        
        public override void InitData()
        {
            base.InitData();
            
			_position = GetObjectParam<Balancy.Models.VectorType>("position");
			_unnyIdItemLink = GetStringParam("unnyIdItemLink");
        }
    }
}
