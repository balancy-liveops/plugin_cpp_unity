using System;
using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.Cheats
{
    public class OfferPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonText;
        [SerializeField] private Image imgIcon;
        [SerializeField] private Image imgBigSprite;
        [SerializeField] private TMP_Text timer;

        private OfferInfo _offerInfo;

        private void Awake()
        {
            buyButton.onClick.AddListener(TryToBuy);
        }

        private void TryToBuy()
        {
            //TODO implement purchase method here
            
            switch (_offerInfo.GameOffer?.StoreItem?.Price.Type)
            {
                case PriceType.Hard:
                    TryToBuyHard();
                    break;
                default:
                    Debug.LogError("This purchase type is not implemented");
                    break;
            }
        }

        private void TryToBuyHard()
        {
            var price = _offerInfo.GameOffer?.StoreItem?.Price;
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
            
            //Below is the testing receipt, it's not designer for the production
            // paymentInfo.Receipt = "{\"Payload\":\"{\\\"json\\\":\\\"{\\\\\\\"orderId\\\\\\\":\\\\\\\"" + paymentInfo.OrderId + "\\\\\\\",\\\\\\\"productId\\\\\\\":\\\\\\\"" + paymentInfo.ProductId + "\\\\\\\"}\\\",\\\"signature\\\":\\\"bypass\\\"}\"}";
            
            void PurchaseCompleted(Balancy.Core.Responses.PurchaseProductResponseData responseData) {
                Debug.Log("Purchase of " + responseData.ProductId + " success = " + responseData.Success);
                if (!responseData.Success)
                {
                    Debug.Log("ErrorCode = " + responseData.ErrorCode);
                    Debug.Log("ErrorMessage = " + responseData.ErrorMessage);
                }
            }
            
            Balancy.API.HardPurchaseGameOffer(_offerInfo, paymentInfo, PurchaseCompleted, false);
        }

        public void Init(OfferInfo offerInfo)
        {
            _offerInfo = offerInfo;
            var gameOffer = offerInfo.GameOffer;
            
            title.text = gameOffer.Name.Value;
            description.text = gameOffer.Description.Value;

            gameOffer.Icon?.LoadSprite(sprite =>
            {
                imgIcon.sprite = sprite;
            });
            
            gameOffer.Sprite?.LoadSprite(sprite =>
            {
                imgBigSprite.sprite = sprite;
            });

            ApplyPrice();
        }

        private void ApplyPrice()
        {
            switch (_offerInfo.GameOffer?.StoreItem?.Price.Type)
            {
                case PriceType.Hard:
                    buyButtonText.text = "USD " + _offerInfo.GameOffer.StoreItem.Price.Product.Price;
                    break;
                default:
                    buyButtonText.text = "Not implemented Price Type";
                    break;
            }
        }
        
        private void Update()
        {
            if (timer == null)
                return;
            
            //TODO timer update
        }
    }
}
