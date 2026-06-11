
namespace Balancy.Data.SmartObjects
{
    public class Purchase : Balancy.Data.BaseData 
    {
        
		private float _priceUSD;
		private string _productId;
		private int _time;
		private string _item;
		private SmartListSimple<string> _attachments;
        
        
		public float PriceUSD
		{
			get => _priceUSD;
			set => SetFloatValue("priceUSD", value);
		}
		public string ProductId
		{
			get => _productId;
			set => SetStringValue("productId", value);
		}
		public int Time
		{
			get => _time;
			set => SetIntValue("time", value);
		}
		public string Item
		{
			get => _item;
			set => SetStringValue("item", value);
		}
		public SmartListSimple<string> Attachments => _attachments;

		public bool HasAttachment(string attachment)
		{
			if (_attachments == null) return false;
			for (int i = 0; i < _attachments.Count; i++)
			{
				if (_attachments[i] == attachment) return true;
			}
			return false;
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("priceUSD", Update_priceUSD);
			InitAndSubscribeForParamChange("productId", Update_productId);
			InitAndSubscribeForParamChange("time", Update_time);
			InitAndSubscribeForParamChange("item", Update_item);
			_attachments = GetListSimpleParam<string>("a");
        }
        
		private void Update_priceUSD() { _priceUSD = GetFloatParam("priceUSD"); }
		private void Update_productId() { _productId = GetStringParam("productId"); }
		private void Update_time() { _time = GetIntParam("time"); }
		private void Update_item() { _item = GetStringParam("item"); }
    }
}
