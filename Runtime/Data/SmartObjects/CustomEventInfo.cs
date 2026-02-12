namespace Balancy.Data.SmartObjects
{
    public class CustomEventInfo : Balancy.Data.BaseData
    {
        private string _instanceId;
        private string _data;

        public string InstanceId => _instanceId;

        public string Data
        {
            get => _data;
            set => SetStringValue("data", value);
        }

        public override void InitData()
        {
            base.InitData();

            InitAndSubscribeForParamChange("instanceId", Update_instanceId);
            InitAndSubscribeForParamChange("data", Update_data);
        }

        internal void SetInstanceId(string value) => SetStringValue("instanceId", value);

        private void Update_instanceId() { _instanceId = GetStringParam("instanceId"); }
        private void Update_data() { _data = GetStringParam("data"); }
    }
}
