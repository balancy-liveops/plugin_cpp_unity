using System;
using UnityEngine;

namespace Balancy
{
    public class BalancyLauncher : MonoBehaviour
    {
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool activateTestPayments = true;
        
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
            
            if (activateTestPayments)
                PreparePayments();
            
            DontDestroyOnLoad(gameObject);
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

            // Below is the testing receipt, it's not designed for production
            paymentInfo.Receipt = JsonUtility.ToJson(new
            {
                Payload = JsonUtility.ToJson(new
                {
                    json = JsonUtility.ToJson(new
                    {
                        orderId = paymentInfo.OrderId,
                        productId = paymentInfo.ProductId
                    }),
                    signature = "bypass"
                })
            });

            return paymentInfo;
        }
        
        private void PreparePayments()
        {
            Balancy.Actions.Ads.SetAdWatchCallback((storeItem) =>
            {
                Debug.Log($"Fake ad watched for: {storeItem?.Name}");
                //TODO Implement your ad watch logic here
                storeItem?.AdWasWatched();
            });

            Balancy.Actions.Purchasing.SetHardPurchaseCallback((productInfo) =>
            {
                Debug.Log($"Starting Purchase: {productInfo?.ProductId}");
                var price = productInfo?.GetStoreItem()?.Price;
                if (price != null)
                {
                    var paymentInfo = CreateTestPaymentInfo(price);
                    Balancy.API.FinalizedHardPurchase(Actions.PurchaseResult.Success, productInfo, paymentInfo,
                        (validationSuccess, removeFromPending) =>
                        {
                            Debug.Log("Purchase completed successfully. Validation success: " + validationSuccess + " Remove from pending: " + removeFromPending);
                        });
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

        public static void Init()
        {
            if (_instance)
                _instance.InitPrivate();
            else
                Debug.LogError("No BalancyLauncher instance found. Please add one to the scene.");
        }
        
        private void InitPrivate()
        {
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
