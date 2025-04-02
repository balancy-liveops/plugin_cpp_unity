using System.Collections.Generic;
using Balancy.Data.SmartObjects;
using Balancy.Example;
using Balancy.Models.LiveOps;
using TMPro;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class DailyBonusPanel : MonoBehaviour
    {
        [SerializeField] private GameObject bonusBtnPrefab;
        [SerializeField] private RectTransform bonusesContent;
        
        [SerializeField] private GameObject bonusRewardPrefab;
        [SerializeField] private RectTransform rewardsContent;

        [SerializeField] private TMP_Text resetType;
        [SerializeField] private TMP_Text nextRewardTime;
        [SerializeField] private DailyBonusSlotView bonusReward;
        
        private List<DailyBonusSlotView> _slots = new List<DailyBonusSlotView>();
        
        private void OnEnable()
        {
            Balancy.Callbacks.OnDailyBonusUpdated += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Balancy.Callbacks.OnDailyBonusUpdated -= Refresh;
        }

        private void Refresh(DailyBonusInfo dailyBonusInfo)
        {
            Refresh();
        }

        private void Refresh()
        {
            bonusesContent.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var liveOps = Profiles.System.LiveOpsInfo;

            foreach (var bonusInfo in liveOps.DailyBonusInfos)
            {
                var newItem = Instantiate(bonusBtnPrefab, bonusesContent);
                newItem.SetActive(true);
                var btnWithText = newItem.GetComponent<ButtonWithText>();
                var bonusInfoBuf = bonusInfo;
                btnWithText.Init(bonusInfoBuf.DailyBonus?.Name, () =>
                {
                    Debug.LogError("Bonus selected " + bonusInfoBuf.DailyBonus?.Name);
                    ShowBonus(bonusInfoBuf);
                });
            }
            
            ShowBonus(liveOps.DailyBonusInfos.Count > 0 ? liveOps.DailyBonusInfos[0] : null);
        }

        private void ShowBonus(DailyBonusInfo bonusInfo)
        {
            if (bonusInfo?.DailyBonus == null)
            {
                SyncList(0);
                return;
            }

            resetType.text = bonusInfo.DailyBonus.Type.ToString();
            

            bool canClaim = bonusInfo.CanClaimNextReward();
            
            nextRewardTime.text = canClaim ? "Available" : TimeFormatter.FormatUnixTime(bonusInfo.GetSecondsTillTheNextReward());

            if (bonusInfo.DailyBonus.Type == DailyBonusType.CalendarReset && bonusInfo.DailyBonus.BonusReward != null)
            {
                bonusReward.gameObject.SetActive(true);
                bool isNextBonus = bonusInfo.IsNextRewardBonus();
                bonusReward.Init(0, bonusInfo.DailyBonus.BonusReward, false, isNextBonus && canClaim,
                    () => TryToClaimNextReward(bonusInfo));
            }
            else
                bonusReward.gameObject.SetActive(false);

            SyncList(bonusInfo.DailyBonus.Rewards.Length);

            var rewards = bonusInfo.DailyBonus.Rewards;
            for (int day = 1; day <= rewards.Length; day++)
            {
                var storeItemView = _slots[day - 1];
                storeItemView.Init(day, rewards[day - 1], day < bonusInfo.GetNextRewardNumber(),
                    day == bonusInfo.GetNextRewardNumber() && canClaim,
                    () => TryToClaimNextReward(bonusInfo));
            }
        }

        private void SyncList(int newSize)
        {
            if (newSize == _slots.Count)
                return;

            if (newSize > _slots.Count)
            {
                for (int i = _slots.Count; i < newSize; i++)
                {
                    var newItem = Instantiate(bonusRewardPrefab, rewardsContent);
                    newItem.SetActive(true);
                    var storeItemView = newItem.GetComponent<DailyBonusSlotView>();
                    _slots.Add(storeItemView);
                }
            }
            else
            {
                for (int i = _slots.Count - 1; i >= newSize; i--)
                {
                    Destroy(_slots[i].gameObject);
                    _slots.RemoveAt(i);
                }
            }
        }

        private void TryToClaimNextReward(DailyBonusInfo bonusInfo)
        {
           var result = bonusInfo.ClaimNextReward();
           Debug.Log("Claimed reward " + result);
           ShowBonus(bonusInfo);
        }
    }
}