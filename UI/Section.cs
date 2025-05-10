using System.Collections.Generic;
using Balancy.Data.SmartObjects;
using Balancy.Models;
using UnityEngine;

namespace Balancy.UI
{
    public class Section : MonoBehaviour
    {
        [SerializeField] private GameObject elementPrefab;
        [SerializeField] private RectTransform content;
        [SerializeField] private string placement;

        class ElementInfo
        {
            public Element Element;
            public int Priority;
        }
        private Dictionary<string, ElementInfo> _activeElements = new Dictionary<string, ElementInfo>();

        private void Awake()
        {
            Balancy.Callbacks.OnNewEventActivated += OnNewEventActivated;
            Balancy.Callbacks.OnNewOfferActivated += OnNewOfferActivated;
            Balancy.Callbacks.OnNewOfferGroupActivated += OnNewOfferGroupActivated;
            Balancy.Callbacks.OnDataUpdated += OnDataUpdated;
            
            if (Main.IsReadyToUse)
                RefreshAll();
        }

        private void OnDestroy()
        {
            Balancy.Callbacks.OnNewEventActivated -= OnNewEventActivated;
            Balancy.Callbacks.OnNewOfferActivated -= OnNewOfferActivated;
            Balancy.Callbacks.OnNewOfferGroupActivated -= OnNewOfferGroupActivated;
            Balancy.Callbacks.OnDataUpdated -= OnDataUpdated;
        }

        private void RefreshAll()
        {
            RemoveChildren();
            _activeElements.Clear();
            
            var allEvents = Profiles.System.SmartInfo.GameEvents;
            var allOffers = Profiles.System.SmartInfo.GameOffers;

            foreach (var offerInfo in allOffers)
                TryToAddOffer(offerInfo);
        }

        private void TryToAddOffer(OfferInfo offerInfo)
        {
            if (offerInfo.GameOffer == null)
                return;

            if (offerInfo.GameOffer is MyGameOffer myGameOffer)
            {
                if (!string.Equals(myGameOffer.Placement, placement))
                    return;

                AddOfferDisplay(offerInfo);
            }
        }

        private void AddOfferDisplay(OfferInfo info)
        {
            var myOffer = info.GameOffer as MyGameOffer;
            _activeElements.Add(info.InstanceId, new ElementInfo
            {
                Priority = myOffer?.Priority ?? 0
            });
            
            info.GameOffer?.Icon.LoadSprite(sprite =>
            {
                if (!_activeElements.ContainsKey(info.InstanceId))
                    return;
                
                var elementGameObject = GameObject.Instantiate(elementPrefab, content);
                var element = elementGameObject.GetComponent<Element>();
                element.Init(sprite, info.GetSecondsLeftBeforeDeactivation);
                elementGameObject.SetActive(true);

                if (myOffer != null)
                {
                    element.SetOnClick(() =>
                    {
                        if (string.IsNullOrEmpty(myOffer.View))
                        {
                            Debug.LogError("No webpage found");
                            return;
                        }
                        
                        Balancy.RenderViewsManager.OpenView(myOffer.View);
                    });
                }

                _activeElements[info.InstanceId].Element = element;
            });
        }

        private void OnDataUpdated(Callbacks.DataUpdatedStatus status)
        {
            RefreshAll();
        }
        
        private void OnNewOfferGroupActivated(OfferGroupInfo offerGroupInfo)
        {
            // Refresh();
        }

        private void OnNewOfferActivated(OfferInfo offerInfo)
        {
            TryToAddOffer(offerInfo);
        }

        private void OnNewEventActivated(EventInfo eventInfo)
        {
            
        }
        
        private void RemoveChildren()
        {
            int n = content.childCount - 1;

            for (int i = n; i >= 0; --i)
            {
                var child = content.GetChild(i);
                if (child == null) continue;
                Destroy(child.gameObject);
            }
        }
    }
}
