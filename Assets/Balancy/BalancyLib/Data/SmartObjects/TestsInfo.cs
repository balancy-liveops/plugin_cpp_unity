
namespace Balancy.Data.SmartObjects
{
    public class TestsInfo : Balancy.Data.BaseData 
    {
        
		private SmartList<Balancy.Data.SmartObjects.AbTestInfo> _tests;
		private string[] _avoidedTests;
        
        
		public SmartList<Balancy.Data.SmartObjects.AbTestInfo> Tests => _tests;
		public string[] AvoidedTests => _avoidedTests;
        
        public override void InitData()
        {
            base.InitData();
            
			_tests = GetListBaseDataParam<Balancy.Data.SmartObjects.AbTestInfo>("tests");
			_avoidedTests = GetStringArrayParam("avoidedTests");
        }
        
    }
}
