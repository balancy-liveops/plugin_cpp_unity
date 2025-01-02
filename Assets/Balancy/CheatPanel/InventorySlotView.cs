using System;
using Balancy.Data.SmartObjects;
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
        
        public void Init(InventorySlot inventorySlot)
        {
            _inventorySlot = inventorySlot;
            Refresh();
        }

        private void Refresh()
        {
            if (_inventorySlot.Item != null)
            {
                itemGameObject.SetActive(true);
                var item = _inventorySlot.Item.Item;
                if (item != null)
                {
                    itemName.text = item.Name?.Value;
                    itemId.text = item.UnnyId;
                    itemCount.text = $"x{_inventorySlot.Item.Amount}";
                }
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
            _inventorySlot.Item.Amount--;
            Refresh();
        }

        private void AddItem()
        {
            _inventorySlot.Item.Amount++;
            Refresh();
        }
    }
}