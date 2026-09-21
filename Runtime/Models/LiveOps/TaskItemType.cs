namespace Balancy.Models.LiveOps 
{
    public enum TaskItemType 
    {
        Collect = 0,
        Spend = 1,
        Own = 2,
    }

    public enum TaskType
    {
        None = 0,
        Item = 1,
        CompleteLevel = 2,
        CompleteLevelStreak = 3,
        Custom = 4,
        FieldNumber = 5,
        Condition = 6,
    }

    public enum TaskStatus
    {
        None = 0,
        InProgress = 1,
        Completed = 2,
        Claimed = 3,
        Failed = 4,
    }
}
