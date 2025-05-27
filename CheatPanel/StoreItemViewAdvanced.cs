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

        private Balancy.Models.LiveOps.Store.Slot _storeSlot;
        private bool _originalCanBuy;
        private CancellationTokenSource _timer;

        public void Init(Balancy.Models.LiveOps.Store.Slot storeSlot, bool canBuy,
            Action<StoreItem> onBuy)
        {
            _storeSlot = storeSlot;
            _originalCanBuy = canBuy;
            var storeItem = _storeSlot.StoreItem;
            
            DisableTimers();
            if (storeSlot.IsAvailable())
                Init(storeItem, canBuy, onBuy);
            else
            {
                Init(storeItem, false, onBuy);
                UpdateTimers();
                _timer = Tasks.Periodic(1, UpdateTimers);
            }
            
            if (_storeSlot.HasLimits())
            {
                limitText.text =
                    $" Purchased {_storeSlot.GetPurchasesDoneDuringTheLastCycle()}/{_storeSlot.GetPurchasesLimitForCycle()}";
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
            if (_storeSlot.IsAvailable())
            {
                Tasks.StopTaskRemotely(_timer);
                Init(_storeSlot, _originalCanBuy, _onBuy);
                return;
            }
            
            resetText.gameObject.SetActive(true);
            resetText.text = $"Available in {_storeSlot.GetSecondsLeftUntilAvailable()}";
        }
    }
}