namespace Balancy.Models.LiveOps.Tasks
{
    public class TaskItem : Balancy.Models.LiveOps.Tasks.BaseTask 
    {
        private string _unnyIdItem;
        
        public Balancy.Models.LiveOps.TaskItemType Type => (Balancy.Models.LiveOps.TaskItemType)GetIntParam("type");
        public Balancy.Models.SmartObjects.Item Item => GetModelByUnnyId<Balancy.Models.SmartObjects.Item>(_unnyIdItem);
        public int Count => GetIntParam("count");
        
        public override void InitData()
        {
            base.InitData();
            
            _unnyIdItem = GetStringParam("unnyIdItem");
        }
        
        public override Balancy.Models.LiveOps.TaskType GetTaskType() 
        { 
            return Balancy.Models.LiveOps.TaskType.Item; 
        }
    }
}
