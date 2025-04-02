
namespace Balancy.Models.SmartObjects
{
    public class Price : Balancy.Models.BaseModel 
    {
        
		private Balancy.Models.SmartObjects.ItemWithAmount[] _items;
		private UnnyProduct _product;
		private Balancy.Models.SmartObjects.PriceType _type;
		private int _ads;
		private bool _isVirtual;
        
        
		public Balancy.Models.SmartObjects.ItemWithAmount[] Items => _items;
		public UnnyProduct Product => _product;
		public Balancy.Models.SmartObjects.PriceType Type => _type;
		public int Ads => _ads;
		public bool IsVirtual => _isVirtual;
        
        public override void InitData()
        {
            base.InitData();
            
			_items = GetObjectArrayParam<Balancy.Models.SmartObjects.ItemWithAmount>("items");
			_product = GetObjectParam<UnnyProduct>("product");
			_type = (Balancy.Models.SmartObjects.PriceType)GetIntParam("type");
			_ads = GetIntParam("ads");
			_isVirtual = GetBoolParam("isVirtual");
        }
        
    }
}
