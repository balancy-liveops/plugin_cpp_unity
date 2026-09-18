using Balancy.Data.SmartObjects;
using Balancy.Models.SmartObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.CheatPanel
{
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private GameObject itemGameObject;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private TMP_Text itemId;
        [SerializeField] private TMP_Text itemCount;
        
        [SerializeField] private Button btnRemoveItem;
        [SerializeField] private Button btnAddItem;

        private InventorySlot _inventorySlot;
        private Item _item;
        private bool _isCatalogItem;
        
        public void Init(InventorySlot inventorySlot)
        {
            _inventorySlot = inventorySlot;
            _item = null;
            _isCatalogItem = false;
            Refresh();
        }

        public void Init(Item item)
        {
            _inventorySlot = null;
            _item = item;
            _isCatalogItem = true;
            Refresh();
        }

        private void Refresh()
        {
            var item = GetItem();
            if (item != null)
            {
                itemGameObject.SetActive(true);
                itemName.text = item.Name?.Value;
                itemId.text = item.UnnyId;
                itemCount.text = _isCatalogItem
                    ? $"x{Balancy.API.Inventory.GetTotalItemsCount(item)}"
                    : $"x{_inventorySlot.Item.Amount}";
                btnRemoveItem.gameObject.SetActive(!_isCatalogItem);
                btnAddItem.gameObject.SetActive(true);
            }
            else
            {
                itemGameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            btnRemoveItem.onClick.AddListener(RemoveItem);
            btnAddItem.onClick.AddListener(AddItem);
        }

        private void RemoveItem()
        {
            var item = GetItem();
            if (item == null)
                return;

            Balancy.API.Inventory.RemoveItems(item, 1);
            Refresh();
        }

        private void AddItem()
        {
            var item = GetItem();
            if (item == null)
                return;

            Balancy.API.Inventory.AddItems(item, 1);
            Refresh();
        }

        private Item GetItem()
        {
            return _isCatalogItem ? _item : _inventorySlot?.Item?.Item;
        }
    }
}
