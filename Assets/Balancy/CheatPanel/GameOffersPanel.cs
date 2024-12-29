using Balancy.Cheats;
using Balancy.Data.SmartObjects;
using Balancy.Example;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class GameOffersPanel : MonoBehaviour
    {
        [SerializeField] private GameObject offerPrefab;
        [SerializeField] private GameObject offerGroupPrefab;
        [SerializeField] private RectTransform content;
        
        [SerializeField] private GameObject offersHeaderPrefab;
        [SerializeField] private GameObject offerGroupsHeaderPrefab;
        
        private void OnOfferGroupDeactivated(OfferGroupInfo offerGroupInfo)
        {
            Refresh();
        }

        private void OnNewOfferGroupActivated(OfferGroupInfo offerGroupInfo)
        {
            Refresh();
        }

        private void OnOfferDeactivated(OfferInfo offerInfo, bool wasPurchased)
        {
            Refresh();
        }

        private void OnNewOfferActivated(OfferInfo offerInfo)
        {
            Refresh();
        }
        
        private void OnEnable()
        {
            Balancy.Callbacks.OnNewOfferActivated += OnNewOfferActivated;
            Balancy.Callbacks.OnOfferDeactivated += OnOfferDeactivated;
            Balancy.Callbacks.OnNewOfferGroupActivated += OnNewOfferGroupActivated;
            Balancy.Callbacks.OnOfferGroupDeactivated += OnOfferGroupDeactivated;
            
            Refresh();
        }

        private void OnDisable()
        {
            Balancy.Callbacks.OnNewOfferActivated -= OnNewOfferActivated;
            Balancy.Callbacks.OnOfferDeactivated -= OnOfferDeactivated;
            Balancy.Callbacks.OnNewOfferGroupActivated -= OnNewOfferGroupActivated;
            Balancy.Callbacks.OnOfferGroupDeactivated -= OnOfferGroupDeactivated;
        }

        private void Refresh()
        {
            content.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;
            
            var activeOffers = Profiles.System.SmartInfo.GameOffers;
            
            if (activeOffers.Count > 0)
            {
                var headerItem = Instantiate(offersHeaderPrefab, content);
                headerItem.SetActive(true);
                
                foreach (var offerInfo in activeOffers)
                {
                    var newItem = Instantiate(offerPrefab, content);
                    newItem.SetActive(true);
                    var offerView = newItem.GetComponent<OfferView>();
                    offerView.Init(offerInfo);
                }
            }
            
            var activeOfferGroups = Profiles.System.SmartInfo.GameOfferGroups;
            if (activeOfferGroups.Count > 0)
            {
                var headerItem = Instantiate(offerGroupsHeaderPrefab, content);
                headerItem.SetActive(true);
                
                foreach (var offerInfo in activeOfferGroups)
                {
                    var newItem = Instantiate(offerGroupPrefab, content);
                    newItem.SetActive(true);
                    var offerView = newItem.GetComponent<OfferGroupView>();
                    offerView.Init(offerInfo);
                }
            }
        }
    }
}