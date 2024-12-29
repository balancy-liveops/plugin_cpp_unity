using System.Threading;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;

namespace Balancy.Cheats
{
    public class EventView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text timer;

        private GameEvent _gameEvent;
        private bool _isActive;
        private CancellationTokenSource _timerToken;

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

            if (_isActive)
                timer.text = TimeFormatter.FormatUnixTime(_gameEvent?.GetSecondsLeftBeforeDeactivation() ?? 0);
            else
                timer.text = TimeFormatter.FormatUnixTime(_gameEvent?.GetSecondsBeforeActivation() ?? 0);
        }

        public void Init(GameEvent gameEvent, bool isActive)
        {
            _gameEvent = gameEvent;
            _isActive = isActive;
            title.text = gameEvent.Name.Value;
        }
    }
}
