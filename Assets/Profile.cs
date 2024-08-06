
namespace Balancy.Data
{
    public class Profile : Balancy.Data.ParentBaseData 
    {
        
		private Balancy.Data.GeneralInfo _generalInfo;
		private Balancy.Data.AnotherInfo _anotherInfo;
        
        
		public Balancy.Data.GeneralInfo GeneralInfo => _generalInfo;
		public Balancy.Data.AnotherInfo AnotherInfo => _anotherInfo;
        
        public override void InitData()
        {
            base.InitData();
            
			_generalInfo = GetBaseDataParam<Balancy.Data.GeneralInfo>("generalInfo");
			_anotherInfo = GetBaseDataParam<Balancy.Data.AnotherInfo>("anotherInfo");
        }
        
    }
}
