namespace Balancy.Models.SmartObjects
{
#pragma warning disable 649
    public abstract class ConditionalTemplate : BaseModel
    {
        private int _priority;
        
        public int Priority => _priority;
        
        private string _unnyIdCondition;
        public Balancy.Models.SmartObjects.Conditions.Logic Condition => GetModelByUnnyId<Balancy.Models.SmartObjects.Conditions.Logic>(_unnyIdCondition);
        
        public override void InitData()
        {
            base.InitData();
            _unnyIdCondition = GetStringParam("unnyIdCondition");
            _priority = GetIntParam("priority");
        }
    }
#pragma warning restore 649
}