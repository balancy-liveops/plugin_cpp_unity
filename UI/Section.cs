using System;
using System.Collections.Generic;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy.UI
{
    public class Section : MonoBehaviour
    {
        [SerializeField] private GameObject elementPrefab;
        [SerializeField] private RectTransform content;
        [SerializeField] private Balancy.Models.SmartObjects.ViewPlacement placement;

        class ElementInfo
        {
            public Element Element;
            public int Priority;
        }
        private Dictionary<string, ElementInfo> _activeElements = new Dictionary<string, ElementInfo>();

        private void Awake()
        {
            Balancy.Callbacks.OnNewEventActivated += OnNewEventActivated;
            Balancy.Callbacks.OnEventDeactivated += OnEventDeactivated;
            Balancy.Callbacks.OnNewOfferActivated += OnNewOfferActivated;
            Balancy.Callbacks.OnNewOfferGroupActivated += OnNewOfferGroupActivated;
            Balancy.Callbacks.OnOfferDeactivated += OnOfferDeactivated;
            Balancy.Callbacks.OnOfferGroupDeactivated += OnOfferGroupDeactivated;
            Balancy.Callbacks.OnDataUpdated += OnDataUpdated;
            Balancy.Callbacks.OnProfileResetStart += CleanUp;
            Balancy.Callbacks.OnGameRefreshed += OnGameRefreshed;
            // Balancy.Callbacks.OnProfileResetFinish += RefreshAll;
            
            if (Main.IsReadyToUse)
                RefreshAll();
        }

        private void OnDestroy()
        {
            Balancy.Callbacks.OnNewEventActivated -= OnNewEventActivated;
            Balancy.Callbacks.OnEventDeactivated -= OnEventDeactivated;
            Balancy.Callbacks.OnNewOfferActivated -= OnNewOfferActivated;
            Balancy.Callbacks.OnNewOfferGroupActivated -= OnNewOfferGroupActivated;
            Balancy.Callbacks.OnOfferDeactivated -= OnOfferDeactivated;
            Balancy.Callbacks.OnOfferGroupDeactivated -= OnOfferGroupDeactivated;
            Balancy.Callbacks.OnDataUpdated -= OnDataUpdated;
            Balancy.Callbacks.OnProfileResetStart -= CleanUp;
            Balancy.Callbacks.OnGameRefreshed -= OnGameRefreshed;
            // Balancy.Callbacks.OnProfileResetFinish -= RefreshAll;
        }
        
        private void OnEventDeactivated(EventInfo eventInfo)
        {
            if (_activeElements.TryGetValue(eventInfo.GameEventUnnyId, out var elementInfo))
            {
                if (elementInfo.Element != null)
                    Destroy(elementInfo.Element.gameObject);
                _activeElements.Remove(eventInfo.GameEventUnnyId);
            }
        }

        private void OnOfferDeactivated(OfferInfo offerInfo, bool wasPurchased)
        {
            if (_activeElements.TryGetValue(offerInfo.InstanceId, out var elementInfo))
            {
                if (elementInfo.Element != null)
                    Destroy(elementInfo.Element.gameObject);
                _activeElements.Remove(offerInfo.InstanceId);
            }
        }

        private void OnOfferGroupDeactivated(OfferGroupInfo offerGroupInfo)
        {
            if (_activeElements.TryGetValue(offerGroupInfo.InstanceId, out var elementInfo))
            {
                if (elementInfo.Element != null)
                    Destroy(elementInfo.Element.gameObject);
                _activeElements.Remove(offerGroupInfo.InstanceId);
            }
        }

        private void CleanUp()
        {
            RemoveChildren();
            _activeElements.Clear();
        }

        private void RefreshAll()
        {
            CleanUp();
            
            var allEvents = Profiles.System.SmartInfo.GameEvents;
            var allOffers = Profiles.System.SmartInfo.GameOffers;
            var allOfferGroups = Profiles.System.SmartInfo.GameOfferGroups;

            foreach (var eventInfo in allEvents)
                TryToAddEvent(eventInfo);
            
            foreach (var offerInfo in allOffers)
                TryToAddOffer(offerInfo);
            
            foreach (var offerInfo in allOfferGroups)
                TryToAddOffer(offerInfo);
        }

        private void TryToAddEvent(EventInfo info)
        {
            if (info.GameEvent?.UnnyPlacement != placement)
                return;
            
            _activeElements.Add(info.GameEventUnnyId, new ElementInfo
            {
                Priority = info.GameEvent?.UnnyPriority ?? 0
            });

            void OnIconLoaded(Sprite sprite)
            {
                AddIconDisplay(info.GameEventUnnyId, info.GameEvent, info, info.GetSecondsLeftBeforeDeactivation,
                    sprite);
            }

            if (info.GameEvent?.Icon == null)
                OnIconLoaded(null);
            else
                info.GameEvent?.Icon?.LoadSprite(OnIconLoaded);
        }
        
        private void TryToAddOffer(OfferInfo info)
        {
            if (info.GameOffer?.UnnyPlacement != placement)
                return;
            
            _activeElements.Add(info.InstanceId, new ElementInfo
            {
                Priority = info.GameOffer?.UnnyPriority ?? 0
            });
            
            void OnIconLoaded(Sprite sprite)
            {
                AddIconDisplay(info.InstanceId, info.GameOffer, info, info.GetSecondsLeftBeforeDeactivation, sprite);
            }

            if (info.GameOffer?.Icon == null)
                OnIconLoaded(null);
            else
                info.GameOffer?.Icon?.LoadSprite(OnIconLoaded);
        }
        
        private void TryToAddOffer(OfferGroupInfo info)
        {
            if (info.GameOfferGroup?.UnnyPlacement != placement)
                return;
            
            _activeElements.Add(info.InstanceId, new ElementInfo
            {
                Priority = info.GameOfferGroup?.UnnyPriority ?? 0
            });
            
            void OnIconLoaded(Sprite sprite)
            {
                AddIconDisplay(info.InstanceId, info.GameOfferGroup, info, info.GetSecondsLeftBeforeDeactivation, sprite);
            }

            if (info.GameOfferGroup?.Icon == null)
                OnIconLoaded(null);
            else
                info.GameOfferGroup?.Icon?.LoadSprite(OnIconLoaded);
        }

        private void AddIconDisplay(string id, IViewModel info, JsonBasedObject owner, Func<int> getSecondsLeft, Sprite sprite)
        {
            if (!_activeElements.ContainsKey(id) || info == null)
                return;
                
            var elementGameObject = GameObject.Instantiate(elementPrefab, content);
            var element = elementGameObject.GetComponent<Element>();
            element.Init(sprite, getSecondsLeft);
            elementGameObject.SetActive(true);

            element.SetOnClick(() =>
            {
                if (info.UnnyView != null)
                    info.UnnyView.OpenView(null, owner);
                else
                    MainUI.ShowMessage("Error", "This element doesn't have a View associated with it.", "OK", null);
            });

            _activeElements[id].Element = element;

            SortElements();
        }

        
        private void OnDataUpdated(Callbacks.DataUpdatedStatus status)
        {
            RefreshAll();
        }
        
        private void OnGameRefreshed()
        {
            RefreshAll();
        }
        
        private void OnNewOfferGroupActivated(OfferGroupInfo offerGroupInfo)
        {
            TryToAddOffer(offerGroupInfo);
        }

        private void OnNewOfferActivated(OfferInfo offerInfo)
        {
            TryToAddOffer(offerInfo);
        }

        private void OnNewEventActivated(EventInfo eventInfo)
        {
            TryToAddEvent(eventInfo);
        }
        
        private void SortElements()
        {
            var elements = new List<ElementInfo>(_activeElements.Values);
            elements.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            RemoveChildren(false);

            foreach (var elementInfo in elements)
            {
                if (elementInfo.Element == null) continue;
                RectTransform transform1;
                (transform1 = elementInfo.Element.transform as RectTransform).SetParent(content, false);
                transform1.localScale = Vector3.one;
            }
        }
        
        private void RemoveChildren(bool destroy = true)
        {
            int n = content.childCount - 1;

            for (int i = n; i >= 0; --i)
            {
                var child = content.GetChild(i);
                if (child == null) continue;
                if (destroy)
                    Destroy(child.gameObject);
                else
                    child.SetParent(null);
            }
        }
    }
}
