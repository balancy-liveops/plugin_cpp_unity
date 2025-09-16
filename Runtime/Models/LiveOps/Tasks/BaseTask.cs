namespace Balancy.Models.LiveOps.Tasks
{
    public class BaseTask : Balancy.Models.BaseModel 
    {
        private Balancy.Models.SmartObjects.Reward _reward;
        private Localization.LocalizedString _description;
        private UnnyObject _icon;
        
        public Balancy.Models.SmartObjects.Reward Reward => _reward;
        public Localization.LocalizedString Description => _description;
        public UnnyObject Icon => _icon;
        
        public override void InitData()
        {
            base.InitData();
            
            _reward = GetObjectParam<Balancy.Models.SmartObjects.Reward>("reward");
            _description = GetLocalizedString("description");
            _icon = GetObjectParam<UnnyObject>("icon");
        }
        
        public virtual Balancy.Models.LiveOps.TaskType GetTaskType() 
        { 
            return Balancy.Models.LiveOps.TaskType.None; 
        }
    }
}
