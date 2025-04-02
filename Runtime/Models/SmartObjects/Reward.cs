
namespace Balancy.Models.SmartObjects
{
    public class Reward : Balancy.Models.BaseModel 
    {
        
		private Balancy.Models.SmartObjects.ItemWithAmount[] _items;
        
        
		public Balancy.Models.SmartObjects.ItemWithAmount[] Items => _items;
        
        public override void InitData()
        {
            base.InitData();
            
			_items = GetObjectArrayParam<Balancy.Models.SmartObjects.ItemWithAmount>("items");
        }
        
    }
}
