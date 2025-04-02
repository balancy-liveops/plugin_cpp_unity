
namespace Balancy.Data.SmartObjects
{
    public class Payments : Balancy.Data.BaseData 
    {
        
		private float _daysForPurchase;
		private SmartList<Balancy.Data.SmartObjects.Purchase> _purchases;
		private float _totalSpend;
		private int _lastPaymentTime;
		private float _maxPayment;
		private int _paymentsCount;
		private float _resourcesMultiplier;
		private int _levelVIP;
		private float _comfortablePayment;
        
        
		public float DaysForPurchase
		{
			get => _daysForPurchase;
			set => SetFloatValue("daysForPurchase", value);
		}
		public SmartList<Balancy.Data.SmartObjects.Purchase> Purchases => _purchases;
		public float TotalSpend
		{
			get => _totalSpend;
			set => SetFloatValue("totalSpend", value);
		}
		public int LastPaymentTime
		{
			get => _lastPaymentTime;
			set => SetIntValue("lastPaymentTime", value);
		}
		public float MaxPayment
		{
			get => _maxPayment;
			set => SetFloatValue("maxPayment", value);
		}
		public int PaymentsCount
		{
			get => _paymentsCount;
			set => SetIntValue("paymentsCount", value);
		}
		public float ResourcesMultiplier
		{
			get => _resourcesMultiplier;
			set => SetFloatValue("resourcesMultiplier", value);
		}
		public int LevelVIP
		{
			get => _levelVIP;
			set => SetIntValue("levelVIP", value);
		}
		public float ComfortablePayment
		{
			get => _comfortablePayment;
			set => SetFloatValue("comfortablePayment", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("daysForPurchase", Update_daysForPurchase);
			_purchases = GetListBaseDataParam<Balancy.Data.SmartObjects.Purchase>("purchases");
			InitAndSubscribeForParamChange("totalSpend", Update_totalSpend);
			InitAndSubscribeForParamChange("lastPaymentTime", Update_lastPaymentTime);
			InitAndSubscribeForParamChange("maxPayment", Update_maxPayment);
			InitAndSubscribeForParamChange("paymentsCount", Update_paymentsCount);
			InitAndSubscribeForParamChange("resourcesMultiplier", Update_resourcesMultiplier);
			InitAndSubscribeForParamChange("levelVIP", Update_levelVIP);
			InitAndSubscribeForParamChange("comfortablePayment", Update_comfortablePayment);
        }
        
		private void Update_daysForPurchase() { _daysForPurchase = GetFloatParam("daysForPurchase"); }
		private void Update_totalSpend() { _totalSpend = GetFloatParam("totalSpend"); }
		private void Update_lastPaymentTime() { _lastPaymentTime = GetIntParam("lastPaymentTime"); }
		private void Update_maxPayment() { _maxPayment = GetFloatParam("maxPayment"); }
		private void Update_paymentsCount() { _paymentsCount = GetIntParam("paymentsCount"); }
		private void Update_resourcesMultiplier() { _resourcesMultiplier = GetFloatParam("resourcesMultiplier"); }
		private void Update_levelVIP() { _levelVIP = GetIntParam("levelVIP"); }
		private void Update_comfortablePayment() { _comfortablePayment = GetFloatParam("comfortablePayment"); }
    }
}
