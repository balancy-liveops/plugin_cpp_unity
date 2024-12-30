using Balancy.Cheats;
using Balancy.Data.SmartObjects;
using Balancy.Example;
using Balancy.Models.SmartObjects;
using UnityEngine;

namespace Balancy.CheatPanel
{
    public class ShopPanel : MonoBehaviour
    {
        [SerializeField] private GameObject shopPageBtnPrefab;
        [SerializeField] private RectTransform pagesContent;
        
        [SerializeField] private GameObject shopSlotViewPrefab;
        [SerializeField] private RectTransform slotsContent;
        
        private void OnEnable()
        {
            Refresh();
        }
        
        private void Refresh()
        {
            pagesContent.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var shops = Profiles.System.ShopsInfo;
            var activeShop = shops.GameShops[0];
            foreach (var activePage in activeShop.ActivePages)
            {
                var newItem = Instantiate(shopPageBtnPrefab, pagesContent);
                newItem.SetActive(true);
                var btnWithText = newItem.GetComponent<ButtonWithText>();
                var page = activePage;
                btnWithText.Init(activePage.Page?.Name?.Value, () =>
                {
                    Debug.LogError("Page selected " + page?.Page?.Name.Value);
                    ShowPage(page);
                });
            }
            
            ShowPage(activeShop.ActivePages.Count > 0 ? activeShop.ActivePages[0] : null);
        }

        private void ShowPage(ShopPage shopPage)
        {
            slotsContent.RemoveChildren();

            if (shopPage == null)
                return;
            
            foreach (var activeSlot in shopPage.ActiveSlots)
            {
                var newItem = Instantiate(shopSlotViewPrefab, slotsContent);
                newItem.SetActive(true);
                var storeItemView = newItem.GetComponent<StoreItemView>();
                storeItemView.Init(activeSlot.Slot.StoreItem, true, TryToBuySlot);
            }
        }

        private void TryToBuySlot(StoreItem storeItem)
        {
            Debug.LogError("Trying to Buy StoreSlot " + storeItem?.Name?.Value);
        }
    }
}