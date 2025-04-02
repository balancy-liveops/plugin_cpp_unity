using System.Threading;
using Balancy.Data.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.Cheats
{
    public class OfferBaseView : MonoBehaviour
    {
        [SerializeField] protected TMP_Text title;
        [SerializeField] protected Image imgIcon;
        [SerializeField] private TMP_Text timer;

        private OfferInfoBase _offerInfoBase;
        private CancellationTokenSource _timerToken;

        protected void SetOffer(OfferInfoBase offerInfoBase)
        {
            _offerInfoBase = offerInfoBase;
        }

        private void OnEnable()
        {
            _timerToken = Tasks.Periodic(1, UpdateTimer);
            UpdateTimer(0);
        }
        
        private void OnDisable()
        {
            Tasks.StopTaskRemotely(_timerToken);
        }

        private void UpdateTimer(float time)
        {
            if (timer == null)
                return;

            timer.text = TimeFormatter.FormatUnixTime(_offerInfoBase?.GetSecondsLeftBeforeDeactivation() ?? 0);
        }
    }
}
