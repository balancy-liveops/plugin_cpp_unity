using System;

namespace Balancy.Data.SmartObjects
{
    public class TasksInfo : Balancy.Data.BaseData
    {
        private SmartList<TaskInfo> _tasks;

        public SmartList<TaskInfo> Tasks => _tasks;

        public override void InitData()
        {
            base.InitData();

            _tasks = GetListBaseDataParam<TaskInfo>("tasks");
        }

        public TaskInfo FindTaskInfo(IntPtr ptr) => FindElementInList(_tasks, ptr);

        /// <summary>
        /// Find task info by the BaseTask model's UnnyId
        /// </summary>
        public TaskInfo FindTaskInfoByTaskUnnyId(string taskUnnyId)
        {
            foreach (var taskInfo in _tasks)
            {
                if (taskInfo?.TaskUnnyId == taskUnnyId)
                    return taskInfo;
            }
            return null;
        }

        /// <summary>
        /// Get all tasks associated with a specific GameEvent
        /// </summary>
        public System.Collections.Generic.List<TaskInfo> GetTasksForEvent(Models.SmartObjects.GameEvent gameEvent)
        {
            return GetTasksForEvent(gameEvent?.UnnyId ?? "");
        }

        /// <summary>
        /// Get all tasks associated with a specific GameEvent by its UnnyId
        /// </summary>
        public System.Collections.Generic.List<TaskInfo> GetTasksForEvent(string gameEventUnnyId)
        {
            var result = new System.Collections.Generic.List<TaskInfo>();
            foreach (var taskInfo in _tasks)
            {
                if (taskInfo?.ParentUnnyId == gameEventUnnyId)
                    result.Add(taskInfo);
            }
            return result;
        }
    }
}
