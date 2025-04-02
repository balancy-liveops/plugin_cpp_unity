
namespace Balancy.Models.SmartObjects
{
    public class ItemWithAmount : Balancy.Models.BaseModel 
    {
        
		private string _unnyIdItem;
		private int _count;
        
        
		public Balancy.Models.SmartObjects.Item Item => GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(_unnyIdItem);
		public int Count => _count;
        
        public override void InitData()
        {
            base.InitData();
            
			_unnyIdItem = GetStringParam("unnyIdItem");
			_count = GetIntParam("count");
        }
        
    }
}
