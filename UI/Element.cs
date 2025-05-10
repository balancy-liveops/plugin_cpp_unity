using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Balancy.UI
{
    public class Element : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text timerText;

        private Func<int> _getSecondsLeft;
        private Action _onClick;
        
        private CancellationTokenSource _cancellationTokenSource;

        private void Awake()
        {
            button.onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            _onClick?.Invoke();
        }

        public void Init(Sprite sprite, Func<int> getSecondsLeft)
        {
            icon.sprite = sprite;
            _getSecondsLeft = getSecondsLeft;

            _cancellationTokenSource = Tasks.Periodic(1, UpdateTimer);
        }

        public void SetOnClick(Action callback)
        {
            _onClick = callback;
        }

        private void OnDestroy()
        {
            Tasks.StopTaskRemotely(_cancellationTokenSource);
        }

        private void UpdateTimer(float time)
        {
            var secondsLeft = _getSecondsLeft?.Invoke() ?? 0;
            timerText.SetText(TimeFormatter.FormatUnixTime(secondsLeft));
        }
    }
}
