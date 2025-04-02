
namespace Balancy.Data.SmartObjects
{
    public class OfferInfo : Balancy.Data.SmartObjects.OfferInfoBase 
    {
        
		private int _discount;
		private float _priceBalancy;
		private string _productId;
		private string _price;
		private int _purchasesCount;
		private string _unnyIdGameOffer;
		private float _priceUSD;
        
        
		public int Discount
		{
			get => _discount;
			// set => SetIntValue("discount", value);
		}
		public float PriceBalancy
		{
			get => _priceBalancy;
			// set => SetFloatValue("priceBalancy", value);
		}
		public string ProductId
		{
			get => _productId;
			// set => SetStringValue("productId", value);
		}
		public string Price
		{
			get => _price;
			// set => SetStringValue("price", value);
		}
		public int PurchasesCount
		{
			get => _purchasesCount;
			// set => SetIntValue("purchasesCount", value);
		}
		
		public Balancy.Models.SmartObjects.GameOffer GameOffer => GetModelByUnnyId<Balancy.Models.SmartObjects.GameOffer>(_unnyIdGameOffer);
		
		public float PriceUSD
		{
			get => _priceUSD;
			// set => SetFloatValue("priceUSD", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("discount", Update_discount);
			InitAndSubscribeForParamChange("priceBalancy", Update_priceBalancy);
			InitAndSubscribeForParamChange("productId", Update_productId);
			InitAndSubscribeForParamChange("price", Update_price);
			InitAndSubscribeForParamChange("purchasesCount", Update_purchasesCount);
			InitAndSubscribeForParamChange("unnyIdGameOffer", Update_unnyIdGameOffer);
			InitAndSubscribeForParamChange("priceUSD", Update_priceUSD);
        }
        
		private void Update_discount() { _discount = GetIntParam("discount"); }
		private void Update_priceBalancy() { _priceBalancy = GetFloatParam("priceBalancy"); }
		private void Update_productId() { _productId = GetStringParam("productId"); }
		private void Update_price() { _price = GetStringParam("price"); }
		private void Update_purchasesCount() { _purchasesCount = GetIntParam("purchasesCount"); }
		private void Update_unnyIdGameOffer() { _unnyIdGameOffer = GetStringParam("unnyIdGameOffer"); }
		private void Update_priceUSD() { _priceUSD = GetFloatParam("priceUSD"); }
    }
}
