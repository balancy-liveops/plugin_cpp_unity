using Balancy.Cheats;
using Balancy.Data.SmartObjects;
using Balancy.Example;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class GameOffersPanel : MonoBehaviour
    {
        [SerializeField] private GameObject offerPrefab;
        [SerializeField] private RectTransform content;
        
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
            foreach (var offerInfo in activeOffers)
            {
                var newItem = Instantiate(offerPrefab, content);
                newItem.SetActive(true);
                var offerView = newItem.GetComponent<OfferPanel>();
                offerView.Init(offerInfo);
            }
        }
    }
}