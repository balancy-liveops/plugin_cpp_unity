using System;
using Balancy.Data.SmartObjects;
using TMPro;
using UnityEngine;

namespace Balancy.Cheats
{
    public class AdsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text header;
        [SerializeField] private AdPanel[] allPanels;

        private void Awake()
        {
            foreach (var panel in allPanels)
                panel.ParentRefresh += Refresh;
        }

        private void OnDestroy()
        {
            foreach (var panel in allPanels)
                panel.ParentRefresh -= Refresh;
        }

        private void OnEnable()
        {
            if (!Balancy.Main.IsReadyToUse)
                return;

            Refresh();
        }

        private void Refresh()
        {
            UnnyProfile profile = Profiles.System;
            var info = profile.AdsInfo;
            
            header.SetText($"[$$$]: {info.RevenueTotal:F3} - Today: {info.RevenueToday:F3}");
            
            foreach (var panel in allPanels)
                panel.Refresh();
        }
    }
}
