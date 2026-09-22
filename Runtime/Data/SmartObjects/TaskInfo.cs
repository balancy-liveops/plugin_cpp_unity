using Balancy.Models.SmartObjects;

namespace Balancy.Data.SmartObjects
{
    public class TaskInfo : Balancy.Data.BaseData
    {
        private string _unnyIdTask;
        private string _unnyIdParent;
        private int _progress;
        private string _runId;
        private string _numericProgress;
        private int _status;
        private int _startTime;
        private int _completeTime;

        public Balancy.Models.LiveOps.Tasks.BaseTask Task => GetModelByUnnyId<Balancy.Models.LiveOps.Tasks.BaseTask>(_unnyIdTask);
        public string TaskUnnyId => _unnyIdTask;

        public GameEvent Parent => GetModelByUnnyId<GameEvent>(_unnyIdParent);
        public string ParentUnnyId => _unnyIdParent;

        public int Progress => _progress;
        public string RunId => _runId;
        /// <summary>Observed numeric value for TaskFieldNumber, including fractional progress.</summary>
        public string NumericProgress => _numericProgress;
        public bool TryGetNumericProgress(out double value) => double.TryParse(_numericProgress,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        public CustomTaskContext CreateCustomContext() => new CustomTaskContext(TaskUnnyId, RunId);

        public Balancy.Models.LiveOps.TaskStatus Status => (Balancy.Models.LiveOps.TaskStatus)_status;

        public int StartTime => _startTime;

        public int CompleteTime => _completeTime;

        public override void InitData()
        {
            base.InitData();
            InitAndSubscribeForParamChange("runId", () => _runId = GetStringParam("runId"));
            InitAndSubscribeForParamChange("numericProgress", () => _numericProgress = GetStringParam("numericProgress"));

            InitAndSubscribeForParamChange("unnyIdTask", Update_unnyIdTask);
            InitAndSubscribeForParamChange("unnyIdParent", Update_unnyIdParent);
            InitAndSubscribeForParamChange("progress", Update_progress);
            InitAndSubscribeForParamChange("status", Update_status);
            InitAndSubscribeForParamChange("startTime", Update_startTime);
            InitAndSubscribeForParamChange("completeTime", Update_completeTime);
        }

        private void Update_unnyIdTask() { _unnyIdTask = GetStringParam("unnyIdTask"); }
        private void Update_unnyIdParent() { _unnyIdParent = GetStringParam("unnyIdParent"); }
        private void Update_progress() { _progress = GetIntParam("progress"); }
        private void Update_status() { _status = GetIntParam("status"); }
        private void Update_startTime() { _startTime = GetIntParam("startTime"); }
        private void Update_completeTime() { _completeTime = GetIntParam("completeTime"); }
    }
}
