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
            buyButtonText.text = Utils.GetPriceText(_offerInfo.GameOffer?.StoreItem);
        }
    }
}
