using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Balancy.Mail
{
    // Explicit packed reads also work with IL2CPP/Wasm32, without PtrToStructure.
    internal static class MailboxInterop
    {
        internal static string Utf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0;
            // The native response owns these terminated UTF-8 strings. JSON
            // escaping can expand payloadJson beyond the decoded text size.
            while (Marshal.ReadByte(pointer, length) != 0)
                length = checked(length + 1);
            return Utf8(pointer, length);
        }
        private static string Utf8(IntPtr pointer, int length)
        {
            if (length < 0 || (length > 0 && pointer == IntPtr.Zero))
                throw new InvalidOperationException("Invalid mail text length");
            if (length == 0) return string.Empty;
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        private static int Int(IntPtr pointer, ref int offset) { int value = Marshal.ReadInt32(pointer, offset); offset += 4; return value; }
        private static IntPtr Ptr(IntPtr pointer, ref int offset) { IntPtr value = Marshal.ReadIntPtr(pointer, offset); offset += IntPtr.Size; return value; }
        // C++ validates the payload before emitting the typed kind. Decode its
        // existing JSON projection without changing the packed interop layout.
        [Serializable]
        private sealed class RewardPayload
        {
#pragma warning disable CS0649 // Filled by Unity's JSON serializer.
            public string grantId, assignmentId, offerId, campaignId, sourceGameId, completedAt;
            public RewardData reward;
            public ContentData content;
#pragma warning restore CS0649
        }

        [Serializable]
        private sealed class RewardData
        {
#pragma warning disable CS0649
            public string type, packageId, currencyId;
            public long amount;
#pragma warning restore CS0649
        }

        [Serializable]
        private sealed class ContentData
        {
#pragma warning disable CS0649
            public string locale, title, description, rewardText;
#pragma warning restore CS0649
        }

        private static XOffersRewardMessage ReadXOffersReward(string json)
        {
            RewardPayload payload;
            try { payload = UnityEngine.JsonUtility.FromJson<RewardPayload>(json); }
            catch (ArgumentException) { return null; }
            if (payload?.reward == null || string.IsNullOrEmpty(payload.grantId) ||
                string.IsNullOrEmpty(payload.assignmentId) || string.IsNullOrEmpty(payload.offerId) ||
                string.IsNullOrEmpty(payload.sourceGameId) || string.IsNullOrEmpty(payload.completedAt))
                return null;

            XOffersReward reward;
            if (payload.reward.type == "package" && !string.IsNullOrEmpty(payload.reward.packageId))
                reward = new XOffersReward { Type = XOffersRewardType.Package, PackageId = payload.reward.packageId };
            else if (payload.reward.type == "currency" && !string.IsNullOrEmpty(payload.reward.currencyId) &&
                     payload.reward.amount > 0 && payload.reward.amount <= 9007199254740991L)
                reward = new XOffersReward { Type = XOffersRewardType.Currency, CurrencyId = payload.reward.currencyId, Amount = payload.reward.amount };
            else
                return null;

            // JsonUtility can materialize a default object for explicit null.
            // A native-validated content object always has a locale and title.
            var content = payload.content;
            if (string.IsNullOrEmpty(content?.locale) || string.IsNullOrEmpty(content.title))
                content = null;
            return new XOffersRewardMessage
            {
                GrantId = payload.grantId, AssignmentId = payload.assignmentId, OfferId = payload.offerId,
                CampaignId = string.IsNullOrEmpty(payload.campaignId) ? null : payload.campaignId,
                SourceGameId = payload.sourceGameId, CompletedAt = payload.completedAt, Reward = reward,
                Content = content == null ? null : new XOffersOfferContent
                {
                    Locale = content.locale, Title = content.title, Description = content.description, RewardText = content.rewardText
                }
            };
        }

        internal static MailboxResponse Read(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("Null mailbox response");
            int offset = 1;
            var response = new MailboxResponse { Success = Marshal.ReadByte(pointer) == 1 };
            response.ErrorCode = Int(pointer, ref offset);
            response.ErrorMessage = Utf8(Ptr(pointer, ref offset));
            IntPtr messages = Ptr(pointer, ref offset);
            int count = Int(pointer, ref offset);
            response.NextCursor = Utf8(Ptr(pointer, ref offset));
            response.UnreadCount = Int(pointer, ref offset);
            response.ServerTime = Utf8(Ptr(pointer, ref offset));
            response.DeliveredCount = Int(pointer, ref offset);
            response.HasMore = Int(pointer, ref offset) == 1;
            response.InvalidMessageCount = Int(pointer, ref offset);
            if (count < 0 || count > 100 || (count > 0 && messages == IntPtr.Zero)) throw new InvalidOperationException("Invalid mail count");
            var result = new List<MailMessage>(count);
            const int fixedBytes = 28;
            int stride = fixedBytes + 7 * IntPtr.Size;
            for (int index = 0; index < count; ++index)
            {
                IntPtr current = IntPtr.Add(messages, index * stride);
                int field = 0;
                var kind = (MailMessageKind)Int(current, ref field);
                var parseStatus = (MailParseStatus)Int(current, ref field);
                int version = Int(current, ref field);
                var status = (MailMessageStatus)Int(current, ref field);
                if (status < MailMessageStatus.Unread || status > MailMessageStatus.Processed)
                    throw new InvalidOperationException("Invalid native mail status");
                var type = (MailMessageType)Int(current, ref field);
                if ((int)type < 0 || (int)type > short.MaxValue)
                    throw new InvalidOperationException("Invalid native mail type");
                string id = Utf8(Ptr(current, ref field));
                string payload = Utf8(Ptr(current, ref field));
                string createdAt = Utf8(Ptr(current, ref field)), readAt = Utf8(Ptr(current, ref field)), processedAt = Utf8(Ptr(current, ref field));
                string rewardId = Utf8(Ptr(current, ref field));
                IntPtr text = Ptr(current, ref field);
                int textLength = Int(current, ref field), hasText = Int(current, ref field);
                MailMessage message;
                if (kind == MailMessageKind.NutakuDailyReward) message = new NutakuDailyRewardMessage { RewardId = rewardId };
                else if (kind == MailMessageKind.Raw) message = new RawMailMessage { HasText = hasText == 1, Text = hasText == 1 ? Utf8(text, textLength) : null };
                else if (kind == MailMessageKind.XOffersReward)
                {
                    message = ReadXOffersReward(payload);
                    if (message == null)
                    {
                        message = new RawMailMessage();
                        parseStatus = MailParseStatus.InvalidPayload;
                    }
                }
                else
                {
                    message = new RawMailMessage();
                    parseStatus = MailParseStatus.UnsupportedType;
                }
                message.Id = id; message.Type = type; message.PayloadVersion = version; message.PayloadJson = payload;
                message.ParseStatus = parseStatus; message.Status = status; message.CreatedAt = createdAt;
                message.ReadAt = string.IsNullOrEmpty(readAt) ? null : readAt; message.ProcessedAt = string.IsNullOrEmpty(processedAt) ? null : processedAt;
                result.Add(message);
            }
            response.Messages = result.AsReadOnly();
            return response;
        }
    }
}
