
namespace Balancy.Data.SmartObjects
{
    public class SmartInfo : Balancy.Data.BaseData 
    {
        
		private SmartList<Balancy.Data.SmartObjects.OfferInfo> _gameOffers;
        
        
		public SmartList<Balancy.Data.SmartObjects.OfferInfo> GameOffers => _gameOffers;
        
        public override void InitData()
        {
            base.InitData();
            
			_gameOffers = GetListBaseDataParam<Balancy.Data.SmartObjects.OfferInfo>("gameOffers");
        }
        
    }
}
