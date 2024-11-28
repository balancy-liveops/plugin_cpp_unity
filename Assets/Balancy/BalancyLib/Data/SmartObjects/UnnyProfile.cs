
namespace Balancy.Data.SmartObjects
{
    public class UnnyProfile : Balancy.Data.ParentBaseData 
    {
        
		private Balancy.Data.SmartObjects.Payments _payments;
		private Balancy.Data.SmartObjects.ScriptsState _scriptsState;
		private Balancy.Data.SmartObjects.SmartInfo _smartInfo;
		private Balancy.Data.SmartObjects.SegmentsInfo _segmentsInfo;
		private Balancy.Data.SmartObjects.GeneralInfo _generalInfo;
		private Balancy.Data.SmartObjects.TestsInfo _testsInfo;
		private Balancy.Data.SmartObjects.AdsInfo _adsInfo;
		private Balancy.Data.SmartObjects.LiveOpsInfo _liveOpsInfo;
		private Balancy.Data.SmartObjects.InventoryInfo _inventories;
        
        
		public Balancy.Data.SmartObjects.Payments Payments => _payments;
		// public Balancy.Data.SmartObjects.ScriptsState ScriptsState => _scriptsState;
		public Balancy.Data.SmartObjects.SmartInfo SmartInfo => _smartInfo;
		public Balancy.Data.SmartObjects.SegmentsInfo SegmentsInfo => _segmentsInfo;
		public Balancy.Data.SmartObjects.GeneralInfo GeneralInfo => _generalInfo;
		public Balancy.Data.SmartObjects.TestsInfo TestsInfo => _testsInfo;
		public Balancy.Data.SmartObjects.AdsInfo AdsInfo => _adsInfo;
		public Balancy.Data.SmartObjects.LiveOpsInfo LiveOpsInfo => _liveOpsInfo;
		public Balancy.Data.SmartObjects.InventoryInfo Inventories => _inventories;
        
        public override void InitData()
        {
            base.InitData();
            
			_payments = GetBaseDataParam<Balancy.Data.SmartObjects.Payments>("payments");
			_scriptsState = GetBaseDataParam<Balancy.Data.SmartObjects.ScriptsState>("scriptsState");
			_smartInfo = GetBaseDataParam<Balancy.Data.SmartObjects.SmartInfo>("smartInfo");
			_segmentsInfo = GetBaseDataParam<Balancy.Data.SmartObjects.SegmentsInfo>("segmentsInfo");
			_generalInfo = GetBaseDataParam<Balancy.Data.SmartObjects.GeneralInfo>("generalInfo");
			_testsInfo = GetBaseDataParam<Balancy.Data.SmartObjects.TestsInfo>("testsInfo");
			_adsInfo = GetBaseDataParam<Balancy.Data.SmartObjects.AdsInfo>("adsInfo");
			_liveOpsInfo = GetBaseDataParam<Balancy.Data.SmartObjects.LiveOpsInfo>("liveOpsInfo");
			_inventories = GetBaseDataParam<Balancy.Data.SmartObjects.InventoryInfo>("inventories");
        }
        
    }
}
