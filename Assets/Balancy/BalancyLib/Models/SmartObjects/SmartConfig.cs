namespace Balancy.Models.SmartObjects
{
    public class SmartConfig : GameStoreBase 
    {
	    private Localization.LocalizedString _name;
        public Localization.LocalizedString Name => _name;
        
        public override void InitData()
        {
            base.InitData();
            _name = GetLocalizedString("name");
        }
    }
}
