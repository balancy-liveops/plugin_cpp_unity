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
            public AsyncLoadHandler IconLoad;
        }
        private Dictionary<string, ElementInfo> _activeElements = new Dictionary<string, ElementInfo>();

        private void Awake()
        {
            Balancy.Callbacks.OnNewEventActivated += OnNewEventActivated;
            Balancy.Callbacks.OnEventRemoved += OnEventRemoved;
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
            Balancy.Callbacks.OnEventRemoved -= OnEventRemoved;
            Balancy.Callbacks.OnNewOfferActivated -= OnNewOfferActivated;
            Balancy.Callbacks.OnNewOfferGroupActivated -= OnNewOfferGroupActivated;
            Balancy.Callbacks.OnOfferDeactivated -= OnOfferDeactivated;
            Balancy.Callbacks.OnOfferGroupDeactivated -= OnOfferGroupDeactivated;
            Balancy.Callbacks.OnDataUpdated -= OnDataUpdated;
            Balancy.Callbacks.OnProfileResetStart -= CleanUp;
            Balancy.Callbacks.OnGameRefreshed -= OnGameRefreshed;
            // Balancy.Callbacks.OnProfileResetFinish -= RefreshAll;
            CleanUp();
        }
        
        private void OnEventRemoved(EventInfo eventInfo) => RemoveElement(eventInfo.InstanceId);

        private void OnOfferDeactivated(OfferInfo offerInfo, bool wasPurchased) => RemoveElement(offerInfo.InstanceId);

        private void OnOfferGroupDeactivated(OfferGroupInfo offerGroupInfo) => RemoveElement(offerGroupInfo.InstanceId);

        private void RemoveElement(string id)
        {
            if (!_activeElements.TryGetValue(id, out var entry)) return;
            _activeElements.Remove(id);
            entry.IconLoad?.Cancel();
            if (entry.Element != null)
            {
                entry.Element.gameObject.SetActive(false);
                Destroy(entry.Element.gameObject);
            }
        }

        private void CleanUp()
        {
            foreach (var entry in _activeElements.Values) entry.IconLoad?.Cancel();
            _activeElements.Clear();
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
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
            AddView(info.InstanceId, info.GameEvent, info, info.GetSecondsLeftBeforeDeactivation);
        }

        private void TryToAddOffer(OfferInfo info)
        {
            AddView(info.InstanceId, info.GameOffer, info, info.GetSecondsLeftBeforeDeactivation);
        }

        private void TryToAddOffer(OfferGroupInfo info)
        {
            AddView(info.InstanceId, info.GameOfferGroup, info, info.GetSecondsLeftBeforeDeactivation);
        }

        private void AddView(string id, IViewModel info, JsonBasedObject owner, Func<int> getSecondsLeft)
        {
            if (info == null || info.UnnyPlacement != placement) return;
            var icon = info.Icon;
            AddIconDisplay(id, info, owner, getSecondsLeft,
                icon == null ? null : new Func<Action<Sprite>, AsyncLoadHandler>(icon.LoadSprite));
        }

        private void AddIconDisplay(string id, IViewModel info, JsonBasedObject owner,
            Func<int> getSecondsLeft, Func<Action<Sprite>, AsyncLoadHandler> loadIcon)
        {
            RemoveElement(id);
            var elementGameObject = Instantiate(elementPrefab, content);
            var element = elementGameObject.GetComponent<Element>();
            var entry = new ElementInfo { Element = element, Priority = info.UnnyPriority };
            _activeElements.Add(id, entry);

            // Keep the prefab's placeholder and make the button usable before I/O.
            element.Init(null, getSecondsLeft, owner is EventInfo eventInfo ? () => eventInfo.IsFinished : (Func<bool>)null);
            element.SetOnClick(() =>
            {
                if (info.UnnyView != null)
                    info.UnnyView.OpenView(null, owner);
                else
                    MainUI.ShowMessage("Error", "This element doesn't have a View associated with it.", "OK", null);
            });
            elementGameObject.SetActive(true);
            SortElements();

            if (loadIcon == null) return;
            entry.IconLoad = loadIcon(sprite =>
            {
                // The same ID may now belong to a different activation or refresh.
                if (this == null || !_activeElements.TryGetValue(id, out var current) ||
                    !ReferenceEquals(current, entry) || entry.Element == null) return;
                if (sprite != null) entry.Element.SetIcon(sprite);
            });
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
            for (int i = 0; i < elements.Count; i++)
                if (elements[i].Element != null) elements[i].Element.transform.SetSiblingIndex(i);
        }
    }
}
