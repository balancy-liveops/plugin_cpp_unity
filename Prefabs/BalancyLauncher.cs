using System;
using UnityEngine;

namespace Balancy
{
    public class BalancyLauncher : MonoBehaviour
    {
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool activateTestPayments = true;
        [SerializeField] private bool activateDebugLogs = true;
        
        [SerializeField] private string apiGameId;
        [SerializeField] private string apiPublicKey;

        [SerializeField] private Constants.Environment environment = Constants.Environment.Development;
        [SerializeField] private string branchName;
        
        public void SetGameId(string value) { apiGameId = value; }
        public void SetPublicKey(string value) { apiPublicKey = value; }
        public void SetBranchName(string value) { branchName = value; }
        
        private static BalancyLauncher _instance;

        private void Start()
        {
            if (autoStart)
                InitPrivate();
            
            DontDestroyOnLoad(gameObject);
        }
        
        [System.Serializable]
        private class PaymentReceiptData
        {
            public string Payload;
        }

        [System.Serializable]
        private class PaymentPayloadData
        {
            public string json;
            public string signature;
        }

        [System.Serializable]
        private class PaymentOrderData
        {
            public string orderId;
            public string productId;
        }
        
        private static Balancy.Core.PaymentInfo CreateTestPaymentInfo(Balancy.Models.SmartObjects.Price price)
        {
            var orderId = Guid.NewGuid().ToString();
            var productId = price?.Product?.ProductId ?? string.Empty;
            var productPrice = price?.Product?.Price ?? 0;

            var paymentInfo = new Balancy.Core.PaymentInfo
            {
                Price = productPrice,
                Currency = "USD",
                OrderId = orderId,
                ProductId = productId,
                Receipt = "<receipt>" // Placeholder for receipt
            };

            Debug.Log("1>>> " + paymentInfo.Receipt);
            // Below is the testing receipt, it's not designed for production
            var orderData = new PaymentOrderData
            {
                orderId = paymentInfo.OrderId,
                productId = paymentInfo.ProductId
            };

            var payloadData = new PaymentPayloadData
            {
                json = JsonUtility.ToJson(orderData),
                signature = "bypass"
            };

            var receiptData = new PaymentReceiptData
            {
                Payload = JsonUtility.ToJson(payloadData)
            };

            paymentInfo.Receipt = JsonUtility.ToJson(receiptData);
            
            Debug.Log("2>>> " + paymentInfo.Receipt);

            return paymentInfo;
        }
        
        private void PreparePayments()
        {
            Balancy.Actions.Ads.SetAdWatchCallback((callback) =>
            {
                Debug.Log($"Fake ad was watched");
                //TODO Implement your ad watch logic here
                callback(true);
            });

            Balancy.Actions.Purchasing.SetHardPurchaseCallback((productInfo) =>
            {
                Debug.Log($"Starting Purchase:: {productInfo?.ProductId}");
                var price = productInfo?.GetStoreItem()?.Price;
                if (price != null)
                {
                    var paymentInfo = CreateTestPaymentInfo(price);
                    Debug.LogError(">>== " + paymentInfo.Receipt);
                    Balancy.API.FinalizedHardPurchase(Actions.PurchaseResult.Success, productInfo, paymentInfo,
                        (validationSuccess, removeFromPending) =>
                        {
                            Debug.Log("Purchase completed successfully. Validation success: " + validationSuccess +
                                      " Remove from pending: " + removeFromPending);
                        }, false);
                }
                else
                {
                    Debug.LogWarning($"No price information available for the product: {productInfo?.ProductId}");
                }
            });

            Balancy.Actions.Purchasing.SetGetHardPurchaseInfoCallback((productId) =>
            {
                var allStoreItems = Balancy.CMS.GetModels<Balancy.Models.SmartObjects.StoreItem>(true);
                var price = 0.01f;
                foreach (var storeItem in allStoreItems)
                {
                    if (storeItem?.Price?.Product?.ProductId == productId)
                    {
                        price = storeItem.Price.Product.Price;
                        break;
                    }
                }

                return new Balancy.Actions.Purchasing.HardProductInfo
                {
                    LocalizedTitle = "Test Purchase",
                    LocalizedDescription = "Test Purchase Description",
                    LocalizedPriceString = $"${price:F2}",
                    LocalizedPrice = price,
                    IsoCurrencyCode = "USD",
                    
                };
            });
        }
        
        private void Awake()
        {
            _instance = this;
        }

        public static void Init()
        {
            if (_instance)
                _instance.InitPrivate();
            else
                Debug.LogError("No BalancyLauncher instance found. Please add one to the scene.");
        }
        
        private void InitPrivate()
        {
            if (activateTestPayments)
                PreparePayments();
            
            if (activateDebugLogs)
                Balancy.Callbacks.InitExamplesWithLogs();
            
            Balancy.Main.Init(new AppConfig
            {
                ApiGameId = apiGameId,
                PublicKey = apiPublicKey,
                Environment = GetEnvironment(),
                BranchName = branchName,
                OnProgressUpdateCallback = ((fileName, progress) =>
                {
                    Debug.Log($"Balancy launch progress {(progress*100):2}% : {fileName}");
                }),
            });
        }
        
        private Constants.Environment GetEnvironment()
        {
            //TODO use define symbols here, like PRODUCTION or DEVELOPMENT if required
            return environment;
        }

        private void OnDestroy()
        {
            Balancy.Callbacks.ClearAll();
            Main.Stop();
        }
    }
}
