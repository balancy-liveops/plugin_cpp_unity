namespace Balancy.Models.LiveOps.Tasks
{
    public class TaskCondition : BaseTask
    {
        public SmartObjects.Conditions.Base Condition => GetModelByUnnyId<SmartObjects.Conditions.Base>(GetStringParam("unnyIdCondition"));
        public override TaskType GetTaskType() => TaskType.Condition;
    }
}
