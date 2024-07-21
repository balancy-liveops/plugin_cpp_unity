using System;
using Balancy.Localization;
using Unity.VisualScripting;
using UnityEngine;

namespace Balancy.Models
{
	public class MyCustomTemplate : BaseModel
	{
		private string unnyIdTestLink;
		private string[] unnyIdTestLinkArr;
		
		private int testDuration;
		public int TestDuration => testDuration;
		
		private float[] testFloatArr;
		public float[] TestFloatArr => testFloatArr;
		private int[] testIntArr;
		public int[] TestIntArr => testIntArr;
		private long[] testLongArr;
		public long[] TestLongArr => testLongArr;
		private string[] testStringArr;
		public string[] TestStringArr => testStringArr;
		private bool[] testBoolArr;
		public bool[] TestBoolArr => testBoolArr;
		
		private int testInt;
		public int TestInt => testInt;
		private float testFloat;
		public float TestFloat => testFloat;
		private string testString;
		public string TestString => testString;
		private bool testBool;
		public bool TestBool => testBool;
		private long testLong;
		public long TestLong => testLong;
		
		private int[] testDurationArr;
		public int[] TestDurationArr => testDurationArr;
		
		private UnnyDate testDate;
		private UnnyProduct testProduct;
		public UnnyDate TestDate => testDate;
		public UnnyProduct TestProduct => testProduct;
		
		private UnnyObject sprite;
		public UnnyObject Sprite => sprite;
		private VectorType testInj;
		public VectorType TestInj => testInj;
		
		private UnnyColor color;
		public UnnyColor Color => color;
		private LocalizedString loc;
		public LocalizedString Loc => loc;
		
		private UnnyDate[] testDateArr;
		public UnnyDate[] TestDateArr => testDateArr;
		
		private VectorType[] testInjArr;
		public VectorType[] TestInjArr => testInjArr;
		
		private UnnyObject[] spriteArr;
		public UnnyObject[] SpriteArr => spriteArr;
		private UnnyProduct[] productArr;
		public UnnyProduct[] ProductArr => productArr;
		
		private LocalizedString[] locArr;
		public LocalizedString[] LocArr => locArr;
		
		private UnnyColor[] colors;
		public UnnyColor[] Colors => colors;

		private string unnyIdSelfLink;
		public MyCustomTemplate SelfLink => GetModelByUnnyId<MyCustomTemplate>(unnyIdSelfLink);
		
		private string[] unnyIdSelfLinkArray;
		public MyCustomTemplate[] SelfLinkArray => GetModelsByUnnyIds<MyCustomTemplate>(unnyIdSelfLinkArray);
		
		public override void InitData()
		{
			base.InitData();
			// unnyIdTestLinkArr = GetStringArrayParam("unnyIdTestLinkArr");
			//
			testFloatArr = GetFloatArrayParam("testFloatArr");
			testIntArr = GetIntArrayParam("testIntArr");
			testLongArr = GetLongArrayParam("testLongArr");
			testStringArr = GetStringArrayParam("testStringArr");
			testBoolArr = GetBoolArrayParam("testBoolArr");
			testDurationArr = GetIntArrayParam("testDurationArr");
			
			testInt = GetIntParam("testInt");
			testFloat = GetFloatParam("testFloat");
			testString = GetStringParam("testString");
			testBool = GetBoolParam("testBool");
			testLong = GetLongParam("testLong");
			testDuration = GetIntParam("testDuration");

			testDate = GetObjectParam<UnnyDate>("testDate");
			testProduct = GetObjectParam<UnnyProduct>("testProduct");
			sprite = GetObjectParam<UnnyObject>("sprite");
			testInj = GetObjectParam<VectorType>("testInj");
			color = GetColor("color");
			loc = GetLocalizedString("loc");

			testDateArr = GetObjectArrayParam<UnnyDate>("testDateArr");
			testInjArr = GetObjectArrayParam<VectorType>("testInjArr");
			
			spriteArr = GetObjectArrayParam<UnnyObject>("spriteArr");
			productArr = GetObjectArrayParam<UnnyProduct>("productArr");
			
			locArr = GetLocalizedStrings("locArr");
			colors = GetColors("unnyIdTestLinkArr");

			unnyIdSelfLink = GetStringParam("unnyIdSelfLink");
			unnyIdSelfLinkArray = GetStringArrayParam("unnyIdSelfLinkArray");
		}
		
		
		//
		// [JsonIgnore]
		// public Models.SmartObjects.Item TestLink => DataEditor.GetModelByUnnyId<Models.SmartObjects.Item>(unnyIdTestLink);
		//
		// [JsonIgnore]
		// public Models.SmartObjects.Item[] TestLinkArr
		// {
		// 	get
		// 	{
		// 		if (unnyIdTestLinkArr == null)
		// 			return new Models.SmartObjects.Item[0];
		// 		var testLinkArr = new Models.SmartObjects.Item[unnyIdTestLinkArr.Length];
		// 		for (int i = 0;i < unnyIdTestLinkArr.Length;i++)
		// 			testLinkArr[i] = DataEditor.GetModelByUnnyId<Models.SmartObjects.Item>(unnyIdTestLinkArr[i]);
		// 		return testLinkArr;
		// 	}
		// }
		//
		
		
	}
#pragma warning restore 649
}