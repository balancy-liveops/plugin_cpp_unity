using Balancy.Data.SmartObjects;
using Balancy.Example;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private RectTransform allItemsContent;
        [SerializeField] private RectTransform inventoryCurrenciesContent;
        [SerializeField] private RectTransform inventoryItemsContent;
        
        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            allItemsContent.RemoveChildren();
            inventoryCurrenciesContent.RemoveChildren();
            inventoryItemsContent.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var inventories = Profiles.System.Inventories;
            FillInventory(inventoryCurrenciesContent, inventories.Currencies);
            FillInventory(inventoryItemsContent, inventories.Items);
            FillAllItems(allItemsContent);
        }

        private void FillInventory(RectTransform content, Inventory inventory)
        {
            content.RemoveChildren();

            foreach (var slot in inventory.Slots)
            {
                var newItem = Instantiate(slotPrefab, content);
                newItem.SetActive(true);
                var slotView = newItem.GetComponent<InventorySlotView>();
                slotView.Init(slot);
            }
        }

        private void FillAllItems(RectTransform content)
        {
            foreach (var item in CMS.GetModels<Item>(true))
            {
                if (item == null)
                    continue;

                var newItem = Instantiate(slotPrefab, content);
                newItem.SetActive(true);
                var slotView = newItem.GetComponent<InventorySlotView>();
                slotView.Init(item);
            }
        }
    }
}
