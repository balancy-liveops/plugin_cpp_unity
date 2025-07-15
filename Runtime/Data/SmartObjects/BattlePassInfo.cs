namespace Balancy.Data.SmartObjects
{
    public class BattlePassInfo : Balancy.Data.BaseData 
    {
        private string _unnyIdGameEvent;
        private string _unnyIdConfig;
        private SmartList<Balancy.Data.SmartObjects.BPProgressInfo> _progressInfo;
        private int _scores;
        private int _level;
        private bool _finished;
        private int _finishedTime;
        
        public Balancy.Models.LiveOps.BattlePass.GameEvent GameEvent => GetModelByUnnyId<Balancy.Models.LiveOps.BattlePass.GameEvent>(_unnyIdGameEvent);
        public string GameEventUnnyId => _unnyIdGameEvent;
        
        public Balancy.Models.LiveOps.BattlePass.BattlePassConfig Config => GetModelByUnnyId<Balancy.Models.LiveOps.BattlePass.BattlePassConfig>(_unnyIdConfig);
        public string ConfigUnnyId => _unnyIdConfig;
        
        public SmartList<Balancy.Data.SmartObjects.BPProgressInfo> ProgressInfo => _progressInfo;
        
        public int Scores
        {
            get => _scores;
            // set => SetIntValue("scores", value);
        }
        
        public int Level
        {
            get => _level;
            // set => SetIntValue("level", value);
        }
        
        public bool Finished
        {
            get => _finished;
            // set => SetBoolValue("finished", value);
        }
        
        public int FinishedTime
        {
            get => _finishedTime;
            // set => SetIntValue("finishedTime", value);
        }
        
        public override void InitData()
        {
            base.InitData();
            
            InitAndSubscribeForParamChange("unnyIdGameEvent", Update_unnyIdGameEvent);
            InitAndSubscribeForParamChange("unnyIdConfig", Update_unnyIdConfig);
            InitAndSubscribeForParamChange("scores", Update_scores);
            InitAndSubscribeForParamChange("level", Update_level);
            InitAndSubscribeForParamChange("finished", Update_finished);
            InitAndSubscribeForParamChange("finishedTime", Update_finishedTime);
            
            _progressInfo = GetListBaseDataParam<Balancy.Data.SmartObjects.BPProgressInfo>("progressInfo");
        }
        
        private void Update_unnyIdGameEvent() { _unnyIdGameEvent = GetStringParam("unnyIdGameEvent"); }
        private void Update_unnyIdConfig() { _unnyIdConfig = GetStringParam("unnyIdConfig"); }
        private void Update_scores() { _scores = GetIntParam("scores"); }
        private void Update_level() { _level = GetIntParam("level"); }
        private void Update_finished() { _finished = GetBoolParam("finished"); }
        private void Update_finishedTime() { _finishedTime = GetIntParam("finishedTime"); }
        
        public void SetGameEvent(Balancy.Models.LiveOps.BattlePass.GameEvent gameEvent)
        {
            SetStringValue("unnyIdGameEvent", gameEvent?.UnnyId ?? "");
        }
        
        public void SetConfig(Balancy.Models.LiveOps.BattlePass.BattlePassConfig config)
        {
            SetStringValue("unnyIdConfig", config?.UnnyId ?? "");
        }
    }
}