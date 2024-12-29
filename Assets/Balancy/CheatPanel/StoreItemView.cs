using System;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.Cheats
{
    public class StoreItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private Image imgIcon;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonText;

        private StoreItem _storeItem;
        private Action<StoreItem> _onBuy;
        
        public void Init(StoreItem storeItem, bool canBuy, Action<StoreItem> onBuy)
        {
            _storeItem = storeItem;
            _onBuy = onBuy;
            
            title.text = storeItem.Name.Value;
            storeItem.Sprite?.LoadSprite(sprite =>
            {
                imgIcon.sprite = sprite;
            });
            ApplyPrice();
            
            buyButton.interactable = canBuy;
        }

        private void Awake()
        {
            buyButton.onClick.AddListener(OnTryToBuy);
        }

        private void OnTryToBuy()
        {
            _onBuy?.Invoke(_storeItem);
        }

        private void ApplyPrice()
        {
            switch (_storeItem?.Price.Type)
            {
                case PriceType.Hard:
                    buyButtonText.text = "USD " + _storeItem.Price.Product.Price;
                    break;
                default:
                    buyButtonText.text = "Not implemented Price Type";
                    break;
            }
        }
    }
}