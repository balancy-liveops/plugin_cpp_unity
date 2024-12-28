using System;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;

namespace Balancy.Cheats
{
    public class UserPropertiesPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputLevel;

        
        GameOffer _gameOffer;
        private StoreItem _storeItem;
        
        private void OnEnable()
        {
            if (!Balancy.Main.IsReadyToUse)
                return;

            Refresh();
            
            inputLevel.onEndEdit.AddListener(OnLevelChanged);

            _gameOffer = CMS.GetModelByUnnyId<GameOffer>("1437");
            _storeItem = CMS.GetModelByUnnyId<StoreItem>("1104");
        }

        private void OnDisable()
        {
            inputLevel.onEndEdit.RemoveListener(OnLevelChanged);
        }

        private void Refresh()
        {
            var profile = Profiles.System;
            inputLevel.text = profile.GeneralInfo.Level.ToString();
        }
        
        private void OnLevelChanged(string newValue)
        {
            if (!int.TryParse(newValue, out var newLevel))
            {
                Debug.LogWarning("Invalid level input. Please enter a valid integer.");
                return;
            }

            // Update the profile level
            var profile = Profiles.System;
            profile.GeneralInfo.Level = newLevel;

            // Optional: Log the change
            Debug.Log($"Level updated to: {newLevel}");
        }

        private void OnGUI()
        {
            if (_gameOffer == null)
                return;

            Rect rect = new Rect(10, 500, 200, 50);
            GUI.Label(rect, $"Offer Price = " + _gameOffer.StoreItem.Price.Product.Price);
            rect.y += rect.height;
            GUI.Label(rect, $"StoreItem Price = " + _storeItem.Price.Product.Price);
            rect.y += rect.height;
            GUI.Label(rect, $"Offer2 Price = " + CMS.GetModelByUnnyId<GameOffer>("1437").StoreItem.Price.Product.Price);
        }
    }
}