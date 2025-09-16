namespace Balancy.Models.LiveOps.Tasks
{
    public class TaskCompleteLevels : Balancy.Models.LiveOps.Tasks.BaseTask 
    {
        public int Count => GetIntParam("count");
        
        public override Balancy.Models.LiveOps.TaskType GetTaskType() 
        { 
            return Balancy.Models.LiveOps.TaskType.CompleteLevel; 
        }
    }
}
