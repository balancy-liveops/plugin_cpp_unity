namespace Balancy.Models.LiveOps.Tasks
{
    public class TaskFieldNumber : BaseTask
    {
        public SmartObjects.ProfileFullPath FullPath { get; private set; }
        public SmartObjects.ComparisonType Comparison => (SmartObjects.ComparisonType)GetIntParam("comparison");
        public float Value => GetFloatParam("value");
        public override TaskType GetTaskType() => TaskType.FieldNumber;
        public override void InitData()
        {
            base.InitData();
            FullPath = GetObjectParam<SmartObjects.ProfileFullPath>("fullPath");
        }
    }
}
