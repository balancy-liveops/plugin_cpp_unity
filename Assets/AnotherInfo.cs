namespace Balancy.Data
{
    public class AnotherInfo : BaseData
    {
        private string _name;
        
        public string Name
        {
            get => _name;
            set => SetStringValue("name", value);
        }
        
        public override void InitData()
        {
            base.InitData();
            InitAndSubscribeForParamChange("name", UpdateName);
        }

        private void UpdateName() { _name = GetStringParam("name"); }
    }
}
