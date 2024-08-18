using System;
using Balancy.Data.SmartObjects;
using Balancy.LiveOps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Balancy.Cheats
{
    public class AdPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button addButton;
        [SerializeField] private Ads.AdType adType;
        public event Action ParentRefresh;

        private void Awake()
        {
            addButton.onClick.AddListener(AddAdRevenue);
            header.SetText(adType.ToString());
        }

        public void Refresh()
        {
            UnnyProfile profile = Profiles.System;
            var info = profile.AdsInfo;
            var adInfo = info.GetAdInfo(adType);
            if (adInfo == null)
                return;
            
            description.SetText(
                $"[$$$]: {adInfo.Revenue:F3} - Today: {adInfo.RevenueToday:F3}\n" +
                    $"[Watched]: {adInfo.Count} - Today: {adInfo.CountToday}"
                );
        }

        private void AddAdRevenue()
        {
            double revenue = Random.Range(0.001f, 0.01f);
            Balancy.LiveOps.Ads.TrackRevenue(adType, revenue, "cheat_panel");
            ParentRefresh?.Invoke();
        }
    }
}