namespace Balancy.Data.SmartObjects
{
    public class CustomDocumentInfo : Balancy.Data.BaseData
    {
        private string _unnyId;
        private string _data;

        public string UnnyId => _unnyId;

        public string Data
        {
            get => _data;
            set => SetStringValue("data", value);
        }

        public override void InitData()
        {
            base.InitData();

            InitAndSubscribeForParamChange("unnyId", Update_unnyId);
            InitAndSubscribeForParamChange("data", Update_data);
        }

        internal void SetUnnyId(string value) => SetStringValue("unnyId", value);

        private void Update_unnyId() { _unnyId = GetStringParam("unnyId"); }
        private void Update_data() { _data = GetStringParam("data"); }
    }
}
