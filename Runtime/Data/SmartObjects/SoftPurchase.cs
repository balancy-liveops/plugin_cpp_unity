
namespace Balancy.Data.SmartObjects
{
    public class SoftPurchase : Balancy.Data.BaseData 
    {
        
		private string _price;
		private string _item;
		private int _time;
		private SmartListSimple<string> _attachments;
        
        
		public string Price
		{
			get => _price;
			set => SetStringValue("price", value);
		}
		public string Item
		{
			get => _item;
			set => SetStringValue("item", value);
		}
		public int Time
		{
			get => _time;
			set => SetIntValue("time", value);
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
            
			InitAndSubscribeForParamChange("price", Update_price);
			InitAndSubscribeForParamChange("item", Update_item);
			InitAndSubscribeForParamChange("time", Update_time);
			_attachments = GetListSimpleParam<string>("a");
        }
        
		private void Update_price() { _price = GetStringParam("price"); }
		private void Update_item() { _item = GetStringParam("item"); }
		private void Update_time() { _time = GetIntParam("time"); }
    }
}
