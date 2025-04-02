using Balancy.Cheats;
using Balancy.Data.SmartObjects;
using Balancy.Example;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class GameEventsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject eventPrefab;
        [SerializeField] private RectTransform content;
        
        [SerializeField] private GameObject activeHeaderPrefab;
        [SerializeField] private GameObject notActiveHeaderPrefab;
        
        
        private void OnEnable()
        {
            Balancy.Callbacks.OnNewEventActivated += OnNewEventActivated;
            Balancy.Callbacks.OnEventDeactivated += OnEventDeactivated;
            
            Refresh();
        }
        
        
        private void OnDisable()
        {
            Balancy.Callbacks.OnNewEventActivated -= OnNewEventActivated;
            Balancy.Callbacks.OnEventDeactivated -= OnEventDeactivated;
        }

        private void OnEventDeactivated(EventInfo eventInfo)
        {
            Refresh();
        }

        private void OnNewEventActivated(EventInfo eventInfo)
        {
            Refresh();
        }

        private void Refresh()
        {
            content.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var smartInfo = Profiles.System.SmartInfo;
            var activeEvents = smartInfo.GameEvents;
            if (activeEvents.Count > 0)
            {
                var headerItem = Instantiate(activeHeaderPrefab, content);
                headerItem.SetActive(true);
            }
            foreach (var eventInfo in activeEvents)
            {
                var newItem = Instantiate(eventPrefab, content);
                newItem.SetActive(true);
                var offerView = newItem.GetComponent<EventView>();
                offerView.Init(eventInfo.GameEvent, true);
            }

            var allEvents = CMS.GetModels<GameEvent>(true);
            if (allEvents.Length > activeEvents.Count)
            {
                var headerItem = Instantiate(notActiveHeaderPrefab, content);
                headerItem.SetActive(true);

                foreach (var gameEvent in allEvents)
                {
                    if (smartInfo.HasGameEvent(gameEvent))
                        continue;
                    
                    var newItem = Instantiate(eventPrefab, content);
                    newItem.SetActive(true);
                    var offerView = newItem.GetComponent<EventView>();
                    offerView.Init(gameEvent, false);
                }
            }
        }
    }
}