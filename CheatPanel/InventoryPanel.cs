using Balancy.Data.SmartObjects;
using Balancy.Example;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private RectTransform currencyItemsContent;
        [SerializeField] private RectTransform inventoryCurrenciesContent;
        [SerializeField] private RectTransform inventoryItemsContent;
        
        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            currencyItemsContent.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var inventories = Profiles.System.Inventories;
            FillInventory(inventoryCurrenciesContent, inventories.Currencies);
            FillInventory(inventoryItemsContent, inventories.Items);
        }

        private void FillInventory(RectTransform content, Inventory inventory)
        {
            content.RemoveChildren();

            foreach (var slot in inventory.Slots)
            {
                var newItem = Instantiate(slotPrefab, content);
                newItem.SetActive(true);
                var offerView = newItem.GetComponent<InventorySlotView>();
                offerView.Init(slot);
            }
        }
    }
}