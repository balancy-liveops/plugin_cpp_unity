
namespace Balancy.Models
{
    public class MyCustomTemplate : Balancy.Models.BaseModel 
    {
        
		private UnnyDate _testDate;
		private int _testDuration;
		private UnnyProduct _testProduct;
		private UnnyColor _color;
		private Localization.LocalizedString _loc;
		private Localization.LocalizedString[] _locArr;
		private float[] _testFloatArr;
		private int[] _testIntArr;
		private long[] _testLongArr;
		private string[] _testStringArr;
		private bool[] _testBoolArr;
		private string _unnyIdSelfLink;
		private UnnyDate[] _testDateArr;
		private int[] _testDurationArr;
		private string[] _unnyIdSelfLinkArray;
		private Balancy.Models.VectorType[] _testInjArr;
		private UnnyObject _sprite;
		private UnnyObject[] _spriteArr;
		private UnnyProduct[] _productArr;
		private Balancy.Models.VectorType _testInj;
		private int _testInt;
		private float _testFloat;
		private string _testString;
		private bool _testBool;
		private long _testLong;
        
        
		public UnnyDate TestDate => _testDate;
		public int TestDuration => _testDuration;
		public UnnyProduct TestProduct => _testProduct;
		public UnnyColor Color => _color;
		public Localization.LocalizedString Loc => _loc;
		public Localization.LocalizedString[] LocArr => _locArr;
		public float[] TestFloatArr => _testFloatArr;
		public int[] TestIntArr => _testIntArr;
		public long[] TestLongArr => _testLongArr;
		public string[] TestStringArr => _testStringArr;
		public bool[] TestBoolArr => _testBoolArr;
		public Balancy.Models.MyCustomTemplate SelfLink => GetModelByUnnyId<Balancy.Models.MyCustomTemplate>(_unnyIdSelfLink);
		public UnnyDate[] TestDateArr => _testDateArr;
		public int[] TestDurationArr => _testDurationArr;
		public Balancy.Models.MyCustomTemplate[] SelfLinkArray => GetModelsByUnnyIds<Balancy.Models.MyCustomTemplate>(_unnyIdSelfLinkArray);
		public Balancy.Models.VectorType[] TestInjArr => _testInjArr;
		public UnnyObject Sprite => _sprite;
		public UnnyObject[] SpriteArr => _spriteArr;
		public UnnyProduct[] ProductArr => _productArr;
		public Balancy.Models.VectorType TestInj => _testInj;
		public int TestInt => _testInt;
		public float TestFloat => _testFloat;
		public string TestString => _testString;
		public bool TestBool => _testBool;
		public long TestLong => _testLong;
        
        public override void InitData()
        {
            base.InitData();
            
			_testDate = GetObjectParam<UnnyDate>("testDate");
			_testDuration = GetIntParam("testDuration");
			_testProduct = GetObjectParam<UnnyProduct>("testProduct");
			_color = GetColor("color");
			_loc = GetLocalizedString("loc");
			_locArr = GetLocalizedStrings("locArr");
			_testFloatArr = GetFloatArrayParam("testFloatArr");
			_testIntArr = GetIntArrayParam("testIntArr");
			_testLongArr = GetLongArrayParam("testLongArr");
			_testStringArr = GetStringArrayParam("testStringArr");
			_testBoolArr = GetBoolArrayParam("testBoolArr");
			_unnyIdSelfLink = GetStringParam("unnyIdSelfLink");
			_testDateArr = GetObjectArrayParam<UnnyDate>("testDateArr");
			_testDurationArr = GetIntArrayParam("testDurationArr");
			_unnyIdSelfLinkArray = GetStringArrayParam("unnyIdSelfLinkArray");
			_testInjArr = GetObjectArrayParam<Balancy.Models.VectorType>("testInjArr");
			_sprite = GetObjectParam<UnnyObject>("sprite");
			_spriteArr = GetObjectArrayParam<UnnyObject>("spriteArr");
			_productArr = GetObjectArrayParam<UnnyProduct>("productArr");
			_testInj = GetObjectParam<Balancy.Models.VectorType>("testInj");
			_testInt = GetIntParam("testInt");
			_testFloat = GetFloatParam("testFloat");
			_testString = GetStringParam("testString");
			_testBool = GetBoolParam("testBool");
			_testLong = GetLongParam("testLong");
        }
    }
}
