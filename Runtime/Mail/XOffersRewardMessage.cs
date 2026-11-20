using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Balancy.Mail
{
    public enum XOffersRewardType { Package = 0, Currency = 1 }
#pragma warning disable 0649
    [Serializable, Preserve]
    public sealed class XOffersReward
    {
        [SerializeField] private string type;
        [SerializeField] private string packageId;
        [SerializeField] private string currencyId;
        [SerializeField] private long amount;
        public XOffersRewardType Type => type == "currency" ? XOffersRewardType.Currency : XOffersRewardType.Package;
        public string PackageId => packageId;
        public string CurrencyId => currencyId;
        public long Amount => amount;
    }
    [Serializable, Preserve]
    public sealed class XOffersOfferContent
    {
        [SerializeField] private string locale;
        [SerializeField] private string title;
        [SerializeField] private string description;
        [SerializeField] private string rewardText;
        public string Locale => locale;
        public string Title => title;
        public string Description => description;
        public string RewardText => rewardText;
    }
    [Serializable, Preserve]
    internal sealed class XOffersRewardPayload
    {
        public string grantId, assignmentId, offerId, campaignId, sourceGameId, completedAt;
        public XOffersReward reward;
        public XOffersOfferContent content;
    }
#pragma warning restore 0649
    public sealed class XOffersRewardMessage : MailMessage
    {
        public override MailMessageKind Kind => MailMessageKind.XOffersReward;
        private readonly XOffersRewardPayload payload;
        internal XOffersRewardMessage(string json)
        {
            payload = JsonUtility.FromJson<XOffersRewardPayload>(json);
            if (payload?.reward == null) throw new InvalidOperationException("Invalid XOffers reward payload");
        }
        public string GrantId => payload.grantId;
        public string AssignmentId => payload.assignmentId;
        public string OfferId => payload.offerId;
        public string CampaignId => payload.campaignId;
        public string SourceGameId => payload.sourceGameId;
        public string CompletedAt => payload.completedAt;
        public XOffersReward Reward => payload.reward;
        public XOffersOfferContent Content => string.IsNullOrEmpty(payload.content?.Title) ? null : payload.content;
    }
}
