
namespace Balancy.Models
{
    public class MyCustomTemplate2 : Balancy.Models.MyCustomTemplate 
    {
        
		private Balancy.Models.MyGame.MyTestEnum _enum1;
		private Balancy.Models.MyMultiSelectEnum _enum2;
        
        
		public Balancy.Models.MyGame.MyTestEnum Enum1 => _enum1;
		public Balancy.Models.MyMultiSelectEnum Enum2 => _enum2;
        
        public override void InitData()
        {
            base.InitData();
            
			_enum1 = (Balancy.Models.MyGame.MyTestEnum)GetIntParam("enum1");
			_enum2 = (Balancy.Models.MyMultiSelectEnum)GetIntParam("enum2");
        }
    }
}
