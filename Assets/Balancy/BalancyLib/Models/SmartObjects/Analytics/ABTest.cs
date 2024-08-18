
namespace Balancy.Models.SmartObjects.Analytics
{
    public class ABTest : Balancy.Models.BaseModel 
    {
        
		private string _name;
		private string[] _unnyIdVariants;
		private Balancy.Models.SmartObjects.Analytics.AbTestStatus _status;
		private string _unnyIdBestVariant;
		private string _unnyIdCondition;
		private Balancy.Models.SmartObjects.TargetUsers _targetUsers;
		private Balancy.Models.SmartObjects.GroupType _usersType;
		private UnnyDate _finishTime;
		private string _comments;
		private bool _concurrentTest;
		private float _alphaGlobal;
		private float _betta;
		private float _uplift;
		private string _metric;
		private float _usersPerDay;
		private int _preparationTime;
		private int _totalUsers;
		private bool _preInitLaunch;
        
        
		public string Name => _name;
		public Balancy.Models.SmartObjects.Analytics.ABTestVariant[] Variants => GetModelsByUnnyIds<Balancy.Models.SmartObjects.Analytics.ABTestVariant>(_unnyIdVariants);
		public Balancy.Models.SmartObjects.Analytics.AbTestStatus Status => _status;
		public Balancy.Models.SmartObjects.Analytics.ABTestVariant BestVariant => GetModelByUnnyId<Balancy.Models.SmartObjects.Analytics.ABTestVariant>(_unnyIdBestVariant);
		// public Balancy.Models.SmartObjects.Conditions.Logic Condition => GetModelByUnnyId<Balancy.Models.SmartObjects.Conditions.Logic>(_unnyIdCondition);
		public Balancy.Models.SmartObjects.TargetUsers TargetUsers => _targetUsers;
		public Balancy.Models.SmartObjects.GroupType UsersType => _usersType;
		public UnnyDate FinishTime => _finishTime;
		public string Comments => _comments;
		public bool ConcurrentTest => _concurrentTest;
		public float AlphaGlobal => _alphaGlobal;
		public float Betta => _betta;
		public float Uplift => _uplift;
		public string Metric => _metric;
		public float UsersPerDay => _usersPerDay;
		public int PreparationTime => _preparationTime;
		public int TotalUsers => _totalUsers;
		public bool PreInitLaunch => _preInitLaunch;
        
        public override void InitData()
        {
            base.InitData();
            
			_name = GetStringParam("name");
			_unnyIdVariants = GetStringArrayParam("unnyIdVariants");
			_status = (Balancy.Models.SmartObjects.Analytics.AbTestStatus)GetIntParam("status");
			_unnyIdBestVariant = GetStringParam("unnyIdBestVariant");
			_unnyIdCondition = GetStringParam("unnyIdCondition");
			_targetUsers = (Balancy.Models.SmartObjects.TargetUsers)GetIntParam("targetUsers");
			_usersType = (Balancy.Models.SmartObjects.GroupType)GetIntParam("usersType");
			_finishTime = GetObjectParam<UnnyDate>("finishTime");
			_comments = GetStringParam("comments");
			_concurrentTest = GetBoolParam("concurrentTest");
			_alphaGlobal = GetFloatParam("alphaGlobal");
			_betta = GetFloatParam("betta");
			_uplift = GetFloatParam("uplift");
			_metric = GetStringParam("metric");
			_usersPerDay = GetFloatParam("usersPerDay");
			_preparationTime = GetIntParam("preparationTime");
			_totalUsers = GetIntParam("totalUsers");
			_preInitLaunch = GetBoolParam("preInitLaunch");
        }
        
    }
}
