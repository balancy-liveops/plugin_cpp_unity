using Balancy.Data.SmartObjects;
namespace Balancy
{
    /// <summary>Immutable activation handle. Keep this in asynchronous handlers, not a mutable TaskInfo.</summary>
    public sealed class CustomTaskContext
    {
        private bool _valid = true;
        internal void Invalidate() { _valid = false; }
        public string TaskId { get; }
        public string RunId { get; }
        internal CustomTaskContext(string taskId, string runId) { TaskId = taskId; RunId = runId; }
        public TaskInfo Info {
            get {
                var info = Profiles.System?.TasksInfo?.FindTaskInfoByTaskUnnyId(TaskId);
                return info != null && info.RunId == RunId ? info : null;
            }
        }
        public bool SetProgress(int value) => Update(0, value);
        public bool AddProgress(int amount = 1) => Update(1, amount);
        public bool Complete() => Update(2, 0);
        public bool Fail() => Update(3, 0);
        private bool Update(int operation, int value)
        {
            if (!CustomTasks.IsAvailable || !Controller.IsNativeInitialized || string.IsNullOrEmpty(TaskId) || string.IsNullOrEmpty(RunId) || value < 0)
                return false;
            return UpdateCore(operation, value, LibraryMethods.API.balancyTasks_Update);
        }
        private bool UpdateCore(int operation, int value, System.Func<string, string, int, int, bool> send)
        {
            if (!_valid || string.IsNullOrEmpty(TaskId) || string.IsNullOrEmpty(RunId) || value < 0 || operation < 0 || operation > 3)
                return false;
            return send(TaskId, RunId, operation, value);
        }
    }
}
