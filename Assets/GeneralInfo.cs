namespace Balancy.Data
{
	public class GeneralInfo : BaseData
	{
		private int _testInt;
		private long _testLong;
		private float _testFloat;
		private bool _testBool;
		private string _testString;

		private SmartList<Data.AnotherInfo> _testList;

		public int TestInt
		{
			get => _testInt;
			set => SetIntValue("testInt", value);
		}

		// [JsonIgnore]
		// public long TestLong
		// {
		// 	get => testLong;
		// 	set {
		// 		if (UpdateValue(ref testLong, value))
		// 			_cache?.UpdateStorageValue(_path + "TestLong", testLong);
		// 	}
		// }
		//
		// [JsonIgnore]
		// public float TestFloat
		// {
		// 	get => testFloat;
		// 	set {
		// 		if (UpdateValue(ref testFloat, value))
		// 			_cache?.UpdateStorageValue(_path + "TestFloat", testFloat);
		// 	}
		// }
		//
		// [JsonIgnore]
		// public bool TestBool
		// {
		// 	get => testBool;
		// 	set {
		// 		if (UpdateValue(ref testBool, value))
		// 			_cache?.UpdateStorageValue(_path + "TestBool", testBool);
		// 	}
		// }
		//
		// [JsonIgnore]
		// public string TestString
		// {
		// 	get => testString;
		// 	set {
		// 		if (UpdateValue(ref testString, value))
		// 			_cache?.UpdateStorageValue(_path + "TestString", testString);
		// 	}
		// }

		// [JsonIgnore]
		public SmartList<Data.AnotherInfo> TestList => _testList;

		public override void InitData()
		{
			base.InitData();
			InitAndSubscribeForParamChange("testInt", UpdateTestInt);
			_testList = GetListBaseDataParam<Data.AnotherInfo>("testList");
		}

		private void UpdateTestInt() { _testInt = GetIntParam("testInt"); }
	}
}