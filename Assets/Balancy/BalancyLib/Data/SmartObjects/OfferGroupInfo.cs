
namespace Balancy.Data.SmartObjects
{
	public class OfferGroupStoreItemPurchase : Balancy.Data.BaseData
	{
		private string _unnyIdStoreItem;
		public Balancy.Models.SmartObjects.StoreItem StoreItem => GetModelByUnnyId<Balancy.Models.SmartObjects.StoreItem>(_unnyIdStoreItem);

		public override void InitData()
		{
			base.InitData();

			InitAndSubscribeForParamChange("unnyIdStoreItem", Update_unnyIdStoreItem);
		}
		
		private void Update_unnyIdStoreItem() { _unnyIdStoreItem = GetStringParam("unnyIdStoreItem"); }
	}
	
    public class OfferGroupInfo : Balancy.Data.SmartObjects.OfferInfoBase 
    {
		private string _unnyIdGameOfferGroup;
		private SmartList<Balancy.Data.SmartObjects.OfferGroupStoreItemPurchase> _purchasedItems;
		
		public Balancy.Models.SmartObjects.GameOfferGroup GameOfferGroup => GetModelByUnnyId<Balancy.Models.SmartObjects.GameOfferGroup>(_unnyIdGameOfferGroup);
		public SmartList<Balancy.Data.SmartObjects.OfferGroupStoreItemPurchase> PurchasedItems => _purchasedItems;
        
        public override void InitData()
        {
            base.InitData();
            
			InitAndSubscribeForParamChange("unnyIdGameOfferGroup", Update_unnyIdGameOfferGroup);
			_purchasedItems = GetListBaseDataParam<Balancy.Data.SmartObjects.OfferGroupStoreItemPurchase>("purchasedItems");
        }
        
		private void Update_unnyIdGameOfferGroup() { _unnyIdGameOfferGroup = GetStringParam("unnyIdGameOfferGroup"); }
    }
}
