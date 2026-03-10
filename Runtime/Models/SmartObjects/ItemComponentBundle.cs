namespace Balancy.Models.SmartObjects
{
    public class ItemComponentBundle : ItemComponentBase
    {
        private Reward _reward;

        public Reward Reward => _reward;
        public bool AutoOpen => GetBoolParam("autoOpen");

        public override void InitData()
        {
            base.InitData();
            _reward = GetObjectParam<Reward>("reward");
        }
    }
}
