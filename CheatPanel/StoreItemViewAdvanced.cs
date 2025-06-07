using System;
using System.Threading;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;

namespace Balancy.Cheats
{
    public class StoreItemViewAdvanced : StoreItemView
    {
        [SerializeField] private TMP_Text resetText;
        [SerializeField] private TMP_Text limitText;

        private Balancy.Data.SmartObjects.ShopSlot _shopSlot;
        private bool _originalCanBuy;
        private CancellationTokenSource _timer; 

        public void Init(Balancy.Data.SmartObjects.ShopSlot shopSlot, bool canBuy,
            Action<StoreItem> onBuy)
        {
            _shopSlot = shopSlot;
            _originalCanBuy = canBuy;
            var storeItem = _shopSlot.Slot.StoreItem;
            
            DisableTimers();
            if (_shopSlot.IsAvailable())
                Init(storeItem, canBuy, onBuy);
            else
            {
                Init(storeItem, false, onBuy);
                UpdateTimers();
                _timer = Tasks.Periodic(1, UpdateTimers);
            }
            
            if (_shopSlot.HasLimits())
            {
                limitText.text =
                    $" Purchased {_shopSlot.GetPurchasesDoneDuringTheLastCycle()}/{_shopSlot.GetPurchasesLimitForCycle()}";
            } else
                limitText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Tasks.StopTaskRemotely(_timer);
        }

        private void DisableTimers()
        {
            resetText.gameObject.SetActive(false);
        }

        private void UpdateTimers(float time)
        {
            UpdateTimers();
        }

        private void UpdateTimers()
        {
            if (_shopSlot.IsAvailable())
            {
                Tasks.StopTaskRemotely(_timer);
                Init(_shopSlot, _originalCanBuy, _onBuy);
                return;
            }
            
            resetText.gameObject.SetActive(true);
            resetText.text = $"Available in {_shopSlot.GetSecondsLeftUntilAvailable()}";
        }
    }
}