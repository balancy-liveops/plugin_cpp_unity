namespace Balancy.Models.LiveOps.Tasks
{
    public enum TaskNumberType { Number = 0, Long = 1 }
    public class TaskFieldNumber : BaseTask
    {
        public SmartObjects.ProfileFullPath FullPath { get; private set; }
        public TaskNumberType NumberType => (TaskNumberType)GetIntParam("numberType");
        public SmartObjects.ComparisonType Comparison => (SmartObjects.ComparisonType)GetIntParam("comparison");
        public float Value => GetFloatParam("value");
        public long LongValue => GetLongParam("longValue");
        public override TaskType GetTaskType() => TaskType.FieldNumber;
        public override void InitData()
        {
            base.InitData();
            FullPath = GetObjectParam<SmartObjects.ProfileFullPath>("fullPath");
        }
    }
}
