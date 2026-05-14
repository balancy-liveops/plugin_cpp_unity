namespace Balancy.Models.Core
{
    public class Language : BaseModel
    {
        private string _name;
        private string _localizedName;
        private string _code;

        public string Name => _name;
        public string LocalizedName => _localizedName;
        public string Code => _code;

        public override void InitData()
        {
            base.InitData();
            _name = GetStringParam("name");
            _localizedName = GetStringParam("localizedName");
            _code = GetStringParam("code");
        }
    }
}
