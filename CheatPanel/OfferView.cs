using System;
using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.Cheats
{
    public class OfferView : OfferBaseView
    {
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonText;
        [SerializeField] private Image imgBigSprite;
        
        private OfferInfo _offerInfo;

        private void Awake()
        {
            buyButton.onClick.AddListener(TryToBuy);
        }

        private void TryToBuy()
        {
            Balancy.API.InitPurchaseOffer(_offerInfo, (success, error) => Debug.Log("Purchase complete: " + success + " error = " + error));
        }

        private void TryToBuyHard()
        {
            var price = _offerInfo.GameOffer?.StoreItem?.Price;
            if (price?.Product == null)
                return;

            var paymentInfo = Utils.CreateTestPaymentInfo(price);
            
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
            SetOffer(offerInfo);
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
            if (_offerInfo.GameOffer?.StoreItem?.Price.IsFree() ?? false)
            {
                buyButtonText.text = "FREE";
                return;
            }
            
            switch (_offerInfo.GameOffer?.StoreItem?.Price.Type)
            {
                case PriceType.Hard:
                    buyButtonText.text = "USD " + _offerInfo.GameOffer.StoreItem.Price.Product.Price;
                    break;
                default:
                    buyButtonText.text = "N/A";
                    break;
            }
        }
    }
}
