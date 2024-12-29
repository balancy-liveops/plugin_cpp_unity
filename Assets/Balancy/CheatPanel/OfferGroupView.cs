using System;
using Balancy.Data.SmartObjects;
using Balancy.Example;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy.Cheats
{
    public class OfferGroupView : OfferBaseView
    {
        [SerializeField] private GameObject storeItemPrefab;
        [SerializeField] private RectTransform content;
        
        private OfferGroupInfo _offerInfo;
        
        public void Init(OfferGroupInfo offerInfo)
        {
            _offerInfo = offerInfo;
            SetOffer(offerInfo);
            var gameOffer = offerInfo.GameOfferGroup;
            
            title.text = gameOffer.Name.Value;

            // gameOffer.Icon?.LoadSprite(sprite =>
            // {
            //     Debug.Log("OfferGroupView::set sprite");
            //     imgIcon.sprite = sprite;
            // });

            Refresh();
        }
        
        private void Refresh()
        {
            content.RemoveChildren();
            
            var storeItems = _offerInfo.GameOfferGroup.StoreItems;
            foreach (var storeItem in storeItems)
            {
                var newItem = Instantiate(storeItemPrefab, content);
                newItem.SetActive(true);
                newItem.GetComponent<StoreItemView>().Init(storeItem, _offerInfo.CanPurchase(storeItem), TryToBuy);
            }
        }

        private void TryToBuy(StoreItem storeItem)
        {
            //TODO implement purchase method here
            
            switch (storeItem?.Price.Type)
            {
                case PriceType.Hard:
                    TryToBuyHard(storeItem);
                    break;
                default:
                    Debug.LogError("This purchase type is not implemented");
                    break;
            }
        }

        private void TryToBuyHard(StoreItem storeItem)
        {
            var price = storeItem?.Price;
            if (price?.Product == null)
                return;
            
            var paymentInfo = new Balancy.Core.PaymentInfo
            {
                Price = price.Product.Price,
                Currency = "USD",
                OrderId = Guid.NewGuid().ToString(),
                ProductId = price.Product.ProductId,
                Receipt = "<receipt>"
            };
            
            //Below is the testing receipt, it's not designed for the production
            paymentInfo.Receipt = "{\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"" + paymentInfo.OrderId + "\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"" + paymentInfo.ProductId + "\\\\\\\"}\\\",\\\"signature\\\":\\\"bypass\\\"}\"}";
            
            void PurchaseCompleted(Balancy.Core.Responses.PurchaseProductResponseData responseData) {
                Debug.Log("Purchase of " + responseData.ProductId + " success = " + responseData.Success);
                if (!responseData.Success)
                {
                    Debug.Log("ErrorCode = " + responseData.ErrorCode);
                    Debug.Log("ErrorMessage = " + responseData.ErrorMessage);
                }
                Refresh();
            }
            
            Balancy.API.HardPurchaseGameOfferGroup(_offerInfo, storeItem, paymentInfo, PurchaseCompleted, false);
        }
    }
}