namespace Balancy.Models.SmartObjects
{
#pragma warning disable 649
    public abstract class ConditionalTemplate : BaseModel
    {
        private int _priority;
        
        public int Priority => _priority;
        
        public override void InitData()
        {
            base.InitData();
            _priority = GetIntParam("priority");
        }
    }
#pragma warning restore 649
}