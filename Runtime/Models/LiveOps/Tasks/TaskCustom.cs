namespace Balancy.Models.LiveOps.Tasks
{
    /// <summary>Inherit in the CMS and register the generated C# type through CMS.OnTypeRequested.</summary>
    public class TaskCustom : BaseTask
    {
        /// <summary>Positive count completes automatically. Zero means explicit Complete().</summary>
        public int Count => GetIntParam("count");
        public override TaskType GetTaskType() => TaskType.Custom;
        /// <summary>Called on the Unity main thread on activation, restore and profile reload.</summary>
        public virtual void OnStart(Balancy.CustomTaskContext context) { }
        /// <summary>Release subscriptions. The context can no longer mutate this run.</summary>
        public virtual void OnStop(Balancy.CustomTaskContext context) { }
    }
}
