using System;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.CheatPanel
{
    public class DailyBonusSlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _header;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _itemName;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private ButtonWithText _btnCollect;

        public void Init(int day, Reward reward, bool claimed, bool canClaim, Action onTryToClaim)
        {
            _header.text = $"Day {day}";

            if (reward.Items.Length > 0)
                ApplyFirstReward(reward.Items[0]);
            else
            {
                _itemName.text = "No Item";
                _count.text = "";
            }

            _btnCollect.Init(claimed ? "Claimed" : canClaim ? "Collect" : "Locked", onTryToClaim);
            _btnCollect.SetInteractable(canClaim);
        }
        
        private void ApplyFirstReward(ItemWithAmount itemWithAmount)
        {
            _itemName.text = itemWithAmount.Item?.Name?.Value;
            _count.text = $"x{itemWithAmount.Count}";
        }
    }
}
