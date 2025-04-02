namespace Balancy.Data.SmartObjects
{
    public class AdsInfo : Balancy.Data.BaseData 
    {
        
		private float _revenueTotal;
		private Balancy.Data.SmartObjects.AdInfo _adRewarded;
		private Balancy.Data.SmartObjects.AdInfo _adInterstitial;
		private Balancy.Data.SmartObjects.AdInfo _adCustom;
		private float _interstitialAdsPeriod;
		private float _revenueToday;
        
        
		public float RevenueTotal
		{
			get => _revenueTotal;
			set => SetFloatValue("revenueTotal", value);
		}
		public Balancy.Data.SmartObjects.AdInfo AdRewarded => _adRewarded;
		public Balancy.Data.SmartObjects.AdInfo AdInterstitial => _adInterstitial;
		public Balancy.Data.SmartObjects.AdInfo AdCustom => _adCustom;
		public float InterstitialAdsPeriod
		{
			get => _interstitialAdsPeriod;
			set => SetFloatValue("interstitialAdsPeriod", value);
		}
		public float RevenueToday
		{
			get => _revenueToday;
			set => SetFloatValue("revenueToday", value);
		}
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("revenueTotal", Update_revenueTotal);
			_adRewarded = GetBaseDataParam<Balancy.Data.SmartObjects.AdInfo>("adRewarded");
			_adInterstitial = GetBaseDataParam<Balancy.Data.SmartObjects.AdInfo>("adInterstitial");
			_adCustom = GetBaseDataParam<Balancy.Data.SmartObjects.AdInfo>("adCustom");
			InitAndSubscribeForParamChange("interstitialAdsPeriod", Update_interstitialAdsPeriod);
			InitAndSubscribeForParamChange("revenueToday", Update_revenueToday);
        }
        
		private void Update_revenueTotal() { _revenueTotal = GetFloatParam("revenueTotal"); }
		private void Update_interstitialAdsPeriod() { _interstitialAdsPeriod = GetFloatParam("interstitialAdsPeriod"); }
		private void Update_revenueToday() { _revenueToday = GetFloatParam("revenueToday"); }

		public Balancy.Data.SmartObjects.AdInfo GetAdInfo(API.AdType type)
		{
			switch (type)
			{
				case API.AdType.Rewarded: return AdRewarded;
				case API.AdType.Interstitial: return AdInterstitial;
				case API.AdType.Custom: return AdCustom;
			}

			return null;
		}
    }
}
