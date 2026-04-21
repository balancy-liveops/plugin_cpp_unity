using System;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]
namespace Balancy.Runtime.Core
{
    /// <summary>
    /// Status of a purchase operation
    /// </summary>
    public enum PurchaseStatus
    {
        Success,
        Failed,
        Pending,
        Cancelled,
        AlreadyOwned,
        InvalidProduct
    }
    
    /// <summary>
    /// Information about a subscription
    /// </summary>
    [Serializable]
    public class SubscriptionInfo
    {
        public string ProductId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime ExpireDate { get; set; }
        public bool IsSubscribed { get; set; }
        public bool IsExpired { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsFreeTrial { get; set; }
        public bool IsAutoRenewing { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public string IntroductoryPrice { get; set; }
        public TimeSpan IntroductoryPricePeriod { get; set; }
        public long IntroductoryPriceCycles { get; set; }
    }

    /// <summary>
    /// Receipt information for purchase validation
    /// </summary>
    [Serializable]
    public class PurchaseReceipt
    {
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public string Receipt { get; set; }
        public string Store { get; set; }
        public DateTime PurchaseTime { get; set; }
        public object RawReceipt { get; set; } // Store-specific receipt data
    }

    /// <summary>
    /// Result of a purchase operation
    /// </summary>
    [Serializable]
    public class PurchaseResult
    {
        public PurchaseStatus Status { get; set; }
        public string ProductId { get; set; }
        public PurchaseReceipt Receipt { get; set; }
        public string ErrorMessage { get; set; }
        
        public string TransactionId { get; set; }
        public string CurrencyCode { get; set; }
        public float Price { get; set; }
    }
}