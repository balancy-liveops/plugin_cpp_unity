namespace Balancy.Models.SmartObjects
{
    public class Item : BaseModel
    {
        private Localization.LocalizedString _name;

        private int _maxStack;
        
        public Localization.LocalizedString Name => _name;
        public int MaxStack => _maxStack;

        // public readonly ItemType Type;

        public override void InitData()
        {
            base.InitData();
            _name = GetLocalizedString("name");
            _maxStack = GetIntParam("maxStack");
        }
    }
}