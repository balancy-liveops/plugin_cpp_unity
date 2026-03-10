namespace Balancy.Models.SmartObjects
{
    public class Item : BaseModel
    {
        private Localization.LocalizedString _name;
        private int _maxStack;
        private UnnyObject _icon;
        private ItemType _type;
        private string[] _unnyIdComponents;

        public Localization.LocalizedString Name => _name;
        public int MaxStack => _maxStack;
        public UnnyObject Icon => _icon;
        public ItemType Type => _type;
        public ItemComponentBase[] Components => GetModelsByUnnyIds<ItemComponentBase>(_unnyIdComponents);

        public T GetComponent<T>() where T : ItemComponentBase
        {
            var components = Components;
            if (components == null)
                return null;
            foreach (var component in components)
            {
                if (component is T casted)
                    return casted;
            }
            return null;
        }

        public override void InitData()
        {
            base.InitData();
            _name = GetLocalizedString("name");
            _maxStack = GetIntParam("maxStack");
            _icon = GetObjectParam<UnnyObject>("unnyIcon");
            _type = (ItemType)GetIntParam("type");
            _unnyIdComponents = GetStringArrayParam("unnyIdComponents");
        }
    }
}