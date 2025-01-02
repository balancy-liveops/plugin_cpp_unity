namespace Balancy.Models.SmartObjects
{
    public class GameStoreBase : Balancy.Models.SmartObjects.ConditionalTemplate 
    {
	    private Localization.LocalizedString _name;
	    public Localization.LocalizedString Name => _name;
	    
	    private string[] _unnyIdStore;
	    public Balancy.Models.LiveOps.Store.Page[] StoreItems => GetModelsByUnnyIds<Balancy.Models.LiveOps.Store.Page>(_unnyIdStore);
		        
        public override void InitData()
        {
            base.InitData();
            _name = GetLocalizedString("name");
			_unnyIdStore = GetStringArrayParam("unnyIdStore");
        }
    }
}
