using System;
using Balancy.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.UI
{
    public class MainUI : MonoBehaviour
    {
        [SerializeField] private MessageUI messageUI;

        private static MainUI _instance = null;

        [SerializeField] private Button openShopBtn;
        [SerializeField] private Button resetBtn;

        private void Awake()
        {
            _instance = this;
            // The manager waits for OnDataUpdated if the SDK is not ready yet.
            Balancy.API.PrepareWebView();
            openShopBtn?.onClick.AddListener(OpenShopClicked);
            resetBtn?.onClick.AddListener(ResetClicked);
        }

        private void OpenShopClicked()
        {
            var profile = Balancy.Profiles.System;
            var shop = profile?.ShopsInfo?.ActiveShopInfo;
            if (shop?.Shop?.UnnyView != null)
                shop.Shop.UnnyView.OpenView(null, shop);
            else
                Debug.LogWarning("No Shop to open");
        }

        private void ResetClicked()
        {
            Balancy.Profiles.Reset(null);
        }

        public static void ShowMessage(string header, string message, string buttonText, Action callback)
        {
            _instance?.ShowMessagePrivate(header, message, buttonText, callback);
        }


        private void ShowMessagePrivate(string header, string message, string buttonText, Action callback)
        {
            messageUI.ShowMessage(header, message, buttonText, callback);
        }
    }
}
