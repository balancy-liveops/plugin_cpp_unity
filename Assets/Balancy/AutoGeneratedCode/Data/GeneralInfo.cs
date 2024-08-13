
namespace Balancy.Data
{
    public class GeneralInfo : Balancy.Data.BaseData 
    {
        
		private int _testInt;
		private long _testLong;
		private float _testFloat;
		private bool _testBool;
		private string _testString;
		private SmartList<Balancy.Data.AnotherInfo> _testList;
        
        
		public int TestInt
		{
			get => _testInt;
			set => SetIntValue("testInt", value);
		}
		public long TestLong
		{
			get => _testLong;
			set => SetLongValue("testLong", value);
		}
		public float TestFloat
		{
			get => _testFloat;
			set => SetFloatValue("testFloat", value);
		}
		public bool TestBool
		{
			get => _testBool;
			set => SetBoolValue("testBool", value);
		}
		public string TestString
		{
			get => _testString;
			set => SetStringValue("testString", value);
		}
		public SmartList<Balancy.Data.AnotherInfo> TestList => _testList;
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("testInt", Update_testInt);
			InitAndSubscribeForParamChange("testLong", Update_testLong);
			InitAndSubscribeForParamChange("testFloat", Update_testFloat);
			InitAndSubscribeForParamChange("testBool", Update_testBool);
			InitAndSubscribeForParamChange("testString", Update_testString);
			_testList = GetListBaseDataParam<Balancy.Data.AnotherInfo>("testList");
        }
        
		private void Update_testInt() { _testInt = GetIntParam("testInt"); }
		private void Update_testLong() { _testLong = GetLongParam("testLong"); }
		private void Update_testFloat() { _testFloat = GetFloatParam("testFloat"); }
		private void Update_testBool() { _testBool = GetBoolParam("testBool"); }
		private void Update_testString() { _testString = GetStringParam("testString"); }
    }
}
