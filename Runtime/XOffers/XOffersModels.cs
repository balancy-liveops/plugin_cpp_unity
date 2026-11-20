using System;
using System.Collections.Generic;
using Balancy.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace Balancy.XOffers
{
    public enum XOffersTarget { FreeOld = 1, PayerPast = 2, TrialPayer = 3, TrialFree = 4, PayerCurrent = 5, Developers = 6, FreeNew = 7 }
    public enum XOffersAssignmentState { Unknown = 0, Accepted = 1, Expired = 2, Completed = 3, Rewarded = 4, Failed = 5, Declined = 6, Pending = 7 }
    public sealed class XOffersContextOptions
    {
        public XOffersTarget[] Targets { get; set; } = Array.Empty<XOffersTarget>();
        public string Locale { get; set; } = "en";
        public string Origin { get; set; }
        /// <summary>-1 selects the only configured integration. Set this when the game has several platform integrations.</summary>
        public int Platform { get; set; } = -1;
    }

#pragma warning disable 0649 // Fields are populated by Unity's JSON serializer.
    [Serializable, Preserve]
    public sealed class XOffersExternalIdentity
    {
        [SerializeField] private string provider;
        [SerializeField] private string external_id;
        public string Provider => provider;
        public string ExternalId => external_id;
    }
    [Serializable, Preserve]
    public sealed class XOffersIdentity
    {
        [SerializeField] private string game_id;
        [SerializeField] private string user_id;
        [SerializeField] private XOffersExternalIdentity external;
        [SerializeField] private int signature_version;
        [SerializeField] private string signature;
        public string GameId => game_id;
        public string UserId => user_id;
        public XOffersExternalIdentity External => string.IsNullOrEmpty(external?.Provider) ? null : external;
        public int SignatureVersion => signature_version;
        public string Signature => signature;
    }
    [Serializable, Preserve]
    public sealed class XOffersContext
    {
        [SerializeField] private XOffersIdentity identity;
        [SerializeField] private int[] targets;
        [SerializeField] private string locale;
        [SerializeField] private string origin;
        public XOffersIdentity Identity => identity;
        public IReadOnlyList<int> Targets => Array.AsReadOnly(targets ?? Array.Empty<int>());
        public string Locale => locale;
        public string Origin => origin;
    }
    [Serializable, Preserve]
    public sealed class XOffersAssignment
    {
        [SerializeField] private string assignment_id;
        [SerializeField] private string offer_id;
        [SerializeField] private string campaign_id;
        [SerializeField] private string state;
        [SerializeField] private string accepted_at;
        [SerializeField] private string expires_at;
        [SerializeField] private string completed_at;
        public string AssignmentId => assignment_id;
        public string OfferId => offer_id;
        public string CampaignId => campaign_id;
        public XOffersAssignmentState State => ParseState(state);
        public string AcceptedAt => accepted_at;
        public string ExpiresAt => expires_at;
        public string CompletedAt => completed_at;
        internal static XOffersAssignmentState ParseState(string value)
        {
            switch (value)
            {
                case "ACCEPTED": return XOffersAssignmentState.Accepted;
                case "EXPIRED": return XOffersAssignmentState.Expired;
                case "COMPLETED": return XOffersAssignmentState.Completed;
                case "REWARDED": return XOffersAssignmentState.Rewarded;
                case "FAILED": return XOffersAssignmentState.Failed;
                case "DECLINED": return XOffersAssignmentState.Declined;
                case "PENDING": return XOffersAssignmentState.Pending;
                default: return XOffersAssignmentState.Unknown;
            }
        }
    }
    [Serializable, Preserve]
    internal sealed class XOffersWireResponse
    {
        public string baseUrl;
        public bool sourceEnabled, targetEnabled;
        public XOffersContext context;
        public string request_id, origin_offer_id, assignment_id, state, completed_at;
        public XOffersAssignment[] assignments;
        public bool pending, duplicate;
    }
#pragma warning restore 0649

    public sealed class XOffersContextResponse : Responses.ResponseData
    {
        public string BaseUrl { get; internal set; }
        public bool SourceEnabled { get; internal set; }
        public bool TargetEnabled { get; internal set; }
        public XOffersContext Context { get; internal set; }
        /// <summary>Exact context JSON for the Holi SDK, including nulls. Contains a credential; do not log it.</summary>
        public string ContextJson { get; internal set; }
    }
    public sealed class XOffersAssignmentsResponse : Responses.ResponseData
    {
        public string RequestId { get; internal set; }
        public string OriginOfferId { get; internal set; }
        public IReadOnlyList<XOffersAssignment> Assignments { get; internal set; } = Array.Empty<XOffersAssignment>();
    }
    public sealed class XOffersCompletionResponse : Responses.ResponseData
    {
        public string RequestId { get; internal set; }
        public string AssignmentId { get; internal set; }
        public XOffersAssignmentState State { get; internal set; }
        public string CompletedAt { get; internal set; }
        /// <summary>The backend durably queued this request and will retry it. Completion is not confirmed yet.</summary>
        public bool Pending { get; internal set; }
        public bool Duplicate { get; internal set; }
    }
}
