using System;
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
            Balancy.Callbacks.OnShopUpdated += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Balancy.Callbacks.OnShopUpdated -= Refresh;
        }

        private void Refresh(Callbacks.ShopUpdatedInfo info)
        {
            Refresh();
        }

        private void Refresh()
        {
            pagesContent.RemoveChildren();
            
            if (!Balancy.Main.IsReadyToUse)
                return;

            var shops = Profiles.System.ShopsInfo;
            if (shops.GameShops.Count == 0)
                return;

            var activeShop = shops.ActiveShopInfo;
            if (activeShop != null)
            {
                foreach (var activePage in activeShop.ActivePages)
                {
                    var newItem = Instantiate(shopPageBtnPrefab, pagesContent);
                    newItem.SetActive(true);
                    var btnWithText = newItem.GetComponent<ButtonWithText>();
                    var page = activePage;
                    btnWithText.Init(activePage.Page?.Name?.Value, () =>
                    {
                        ShowPage(page);
                    });
                }

                ShowPage(activeShop.ActivePages.Count > 0 ? activeShop.ActivePages[0] : null);
            }
        }

        private void ShowPage(ShopPage shopPage)
        {
            slotsContent.RemoveChildren();

            if (shopPage == null)
                return;

            for (int i = 0; i < shopPage.ActiveSlots.Count; i++)
            {
                var activeSlot = shopPage.ActiveSlots[i];
                var newItem = Instantiate(shopSlotViewPrefab, slotsContent);
                newItem.SetActive(true);
                var storeItemView = newItem.GetComponent<StoreItemViewAdvanced>();
                storeItemView.Init(activeSlot, true, (storeItem)=> TryToBuySlot(activeSlot));
            }
        }

        private void TryToBuySlot(Balancy.Data.SmartObjects.ShopSlot shopSlot)
        {
            var reward = shopSlot.Slot.StoreItem.Reward;
            Debug.LogWarning("== Trying to buy " + reward.Items.Length);
            for (int i = 0; i < reward.Items.Length; i++)
            {
                var item = reward.Items[i];
                Debug.Log($"Item {i}: {item.Item.Name} x{item.Count}");
            }
            Balancy.API.InitPurchaseShop(shopSlot, (success, error) =>
            {
                Debug.Log("BUY COMPLETE : " + success + " error = " + error);
                Refresh();
            });
        }
    }
}