namespace Balancy.Models.LiveOps.Tasks
{
    public class TaskCompleteLevelsStreak : Balancy.Models.LiveOps.Tasks.TaskCompleteLevels 
    {
        public bool CanLose => GetBoolParam("canLose");
        
        public override Balancy.Models.LiveOps.TaskType GetTaskType() 
        { 
            return Balancy.Models.LiveOps.TaskType.CompleteLevelStreak; 
        }
    }
}
