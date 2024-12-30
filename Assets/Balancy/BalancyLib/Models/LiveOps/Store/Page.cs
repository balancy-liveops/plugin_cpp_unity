
namespace Balancy.Models.LiveOps.Store
{
    public class Page : Balancy.Models.BaseModel 
    {
		private Localization.LocalizedString _name;
		private string[] _unnyIdContent;
        
        
		public Localization.LocalizedString Name => _name;
		public Balancy.Models.LiveOps.Store.Slot[] Content => GetModelsByUnnyIds<Balancy.Models.LiveOps.Store.Slot>(_unnyIdContent);
		
        public override void InitData()
        {
            base.InitData();
            
			_name = GetLocalizedString("name");
			_unnyIdContent = GetStringArrayParam("unnyIdContent");
        }
    }
}
