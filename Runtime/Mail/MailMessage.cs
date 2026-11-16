using System;
using System.Collections.Generic;
using Balancy.Core;

namespace Balancy.Mail
{
    public enum MailMessageKind { Raw = 0, NutakuDailyReward = 1 }
    public enum MailMessageType { Raw = 0, NutakuDailyReward = 1 }
    public enum MailParseStatus { Parsed = 0, UnsupportedType = 1, UnsupportedVersion = 2, InvalidPayload = 3 }
    public enum MailMessageStatus { Unread = 0, Read = 1, Processed = 2 }
    public abstract class MailMessage
    {
        public abstract MailMessageKind Kind { get; }
        public string Id { get; internal set; }
        public MailMessageType Type { get; internal set; }
        public int PayloadVersion { get; internal set; }
        public string PayloadJson { get; internal set; }
        public MailParseStatus ParseStatus { get; internal set; }
        public MailMessageStatus Status { get; internal set; }
        public string CreatedAt { get; internal set; }
        public string ReadAt { get; internal set; }
        public string ProcessedAt { get; internal set; }
    }
    public sealed class RawMailMessage : MailMessage
    {
        public override MailMessageKind Kind => MailMessageKind.Raw;
        internal bool HasText;
        internal string Text;
        public bool TryGetText(out string value) { value = HasText ? Text : null; return HasText; }
    }
    public sealed class NutakuDailyRewardMessage : MailMessage
    {
        public override MailMessageKind Kind => MailMessageKind.NutakuDailyReward;
        public string RewardId { get; internal set; }
    }
    /// <summary>All data is copied from native memory before invoking game code.</summary>
    public sealed class MailboxResponse : Responses.ResponseData
    {
        public IReadOnlyList<MailMessage> Messages { get; internal set; } = Array.Empty<MailMessage>();
        public MailMessage Message => Messages.Count == 1 ? Messages[0] : null;
        public string NextCursor { get; internal set; }
        public int UnreadCount { get; internal set; }
        public string ServerTime { get; internal set; }
        public int DeliveredCount { get; internal set; }
        public bool HasMore { get; internal set; }
        public int InvalidMessageCount { get; internal set; }
    }
    public sealed class MailboxListOptions
    {
        public int Limit { get; set; } = 50;
        public string Cursor { get; set; }
    }
}
