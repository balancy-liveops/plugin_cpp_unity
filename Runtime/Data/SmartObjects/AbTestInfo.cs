namespace Balancy.Data.SmartObjects
{
    public class AbTestInfo : Balancy.Data.BaseData 
    {
        
        private string _unnyIdTest;
        private string _unnyIdVariant;
        private bool _finished;
        
        
        public Balancy.Models.SmartObjects.Analytics.ABTest Test => GetModelByUnnyId<Balancy.Models.SmartObjects.Analytics.ABTest>(_unnyIdTest);
        public Balancy.Models.SmartObjects.Analytics.ABTestVariant Variant => GetModelByUnnyId<Balancy.Models.SmartObjects.Analytics.ABTestVariant>(_unnyIdVariant);
        public bool Finished
        {
            get => _finished;
            set => SetBoolValue("finished", value);
        }
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdTest", Update_unnyIdTest);
            InitAndSubscribeForParamChange("unnyIdVariant", Update_unnyIdVariant);
            InitAndSubscribeForParamChange("finished", Update_finished);
        }
        
        private void Update_unnyIdTest() { _unnyIdTest = GetStringParam("unnyIdTest"); }
        private void Update_unnyIdVariant() { _unnyIdVariant = GetStringParam("unnyIdVariant"); }
        private void Update_finished() { _finished = GetBoolParam("finished"); }
    }
}