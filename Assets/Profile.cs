namespace Balancy.Data
{
    public class Profile : ParentBaseData
    { 
        private Data.GeneralInfo _generalInfo;
        public Data.GeneralInfo GeneralInfo => _generalInfo;

        public override void InitData()
        {
            base.InitData();
            _generalInfo = GetBaseDataParam<Balancy.Data.GeneralInfo>("generalInfo");
        }
    }
}
