using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Balancy.Mail;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class MailboxInteropTests
    {
        private static MailboxResponse Read(IntPtr pointer)
        {
            var type = typeof(MailMessage).Assembly.GetType("Balancy.Mail.MailboxInterop", true);
            return (MailboxResponse)type.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { pointer });
        }
        private sealed class Buffer : IDisposable
        {
            private readonly List<IntPtr> allocations = new List<IntPtr>();
            public IntPtr Allocate(int bytes)
            {
                IntPtr value = Marshal.AllocHGlobal(bytes); allocations.Add(value);
                Marshal.Copy(new byte[bytes], 0, value, bytes); return value;
            }
            public IntPtr Text(string value)
            {
                if (value == null) return IntPtr.Zero;
                byte[] data = Encoding.UTF8.GetBytes(value + "\0"); IntPtr pointer = Allocate(data.Length);
                Marshal.Copy(data, 0, pointer, data.Length); return pointer;
            }
            public IntPtr Response(int count, IntPtr messages)
            {
                IntPtr pointer = Allocate(5 + IntPtr.Size + 3 * IntPtr.Size + 5 * sizeof(int));
                Marshal.WriteByte(pointer, 1);
                int offset = 5 + IntPtr.Size;
                Marshal.WriteIntPtr(pointer, offset, messages); offset += IntPtr.Size;
                Marshal.WriteInt32(pointer, offset, count); offset += sizeof(int);
                Marshal.WriteIntPtr(pointer, offset, Text("cursor")); offset += IntPtr.Size;
                Marshal.WriteInt32(pointer, offset, count); offset += sizeof(int);
                Marshal.WriteIntPtr(pointer, offset, Text("2026-11-09T12:00:00Z"));
                return pointer;
            }
            public void Message(IntPtr pointer, MailMessageKind kind, MailParseStatus parseStatus, MailMessageType type, string payload, string text = null, string rewardId = null, MailMessageStatus status = MailMessageStatus.Unread)
            {
                Marshal.WriteInt32(pointer, 0, (int)kind); Marshal.WriteInt32(pointer, 4, (int)parseStatus); Marshal.WriteInt32(pointer, 8, 1);
                string[] strings = {"12345678-1234-1234-1234-123456789012", payload, "2026-11-09T12:00:00Z", null, null, rewardId, text};
                Marshal.WriteInt32(pointer, 12, (int)status);
                Marshal.WriteInt32(pointer, 16, (int)type);
                int offset = 20;
                foreach (string value in strings) { Marshal.WriteIntPtr(pointer, offset, Text(value)); offset += IntPtr.Size; }
                Marshal.WriteInt32(pointer, offset, text == null ? 0 : Encoding.UTF8.GetByteCount(text));
                Marshal.WriteInt32(pointer, offset + 4, text == null ? 0 : 1);
            }
            public void Dispose() { foreach (IntPtr pointer in allocations) Marshal.FreeHGlobal(pointer); allocations.Clear(); }
        }
        [Test]
        public void CopiesTypedMessagesAndUtf8BeforeNativeMemoryIsFreed()
        {
            MailboxResponse response;
            using (var memory = new Buffer())
            {
                int stride = 28 + 7 * IntPtr.Size;
                IntPtr messages = memory.Allocate(2 * stride);
                memory.Message(messages, MailMessageKind.Raw, MailParseStatus.Parsed, MailMessageType.Raw, "\"Привет 🌍\"", "Привет 🌍");
                memory.Message(IntPtr.Add(messages, stride), MailMessageKind.NutakuDailyReward, MailParseStatus.Parsed, MailMessageType.NutakuDailyReward, "{\"rewardId\":\"november\"}", rewardId: "november", status: MailMessageStatus.Processed);
                response = Read(memory.Response(2, messages));
            }
            Assert.That(response.Success, Is.True);
            Assert.That(response.Messages[0].Status, Is.EqualTo(MailMessageStatus.Unread));
            Assert.That(response.Messages[1].Status, Is.EqualTo(MailMessageStatus.Processed));
            Assert.That(response.Messages.Count, Is.EqualTo(2));
            Assert.That(response.Messages[0].Type, Is.EqualTo(MailMessageType.Raw));
            Assert.That(response.Messages[1].Type, Is.EqualTo(MailMessageType.NutakuDailyReward));
            Assert.That(response.Messages[0], Is.InstanceOf<RawMailMessage>());
            Assert.That(((RawMailMessage)response.Messages[0]).TryGetText(out var text), Is.True);
            Assert.That(text, Is.EqualTo("Привет 🌍"));
            Assert.That(response.Messages[1], Is.InstanceOf<NutakuDailyRewardMessage>());
            Assert.That(((NutakuDailyRewardMessage)response.Messages[1]).RewardId, Is.EqualTo("november"));
            Assert.That(response.NextCursor, Is.EqualTo("cursor"));
        }
        [Test]
        public void RawFallbackPreservesOriginalTypeAndParseStatus()
        {
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                memory.Message(message, MailMessageKind.Raw, MailParseStatus.UnsupportedType, (MailMessageType)42, "{\"new\":42}");
                var response = Read(memory.Response(1, message));
                var raw = (RawMailMessage)response.Message;
                Assert.That(raw.Type, Is.EqualTo((MailMessageType)42));
                Assert.That(raw.ParseStatus, Is.EqualTo(MailParseStatus.UnsupportedType));
                Assert.That(raw.PayloadJson, Is.EqualTo("{\"new\":42}"));
                Assert.That(raw.TryGetText(out _), Is.False);
            }
        }
        [Test]
        public void CopiesTextByLengthIncludingEmbeddedNull()
        {
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                memory.Message(message, MailMessageKind.Raw, MailParseStatus.Parsed, MailMessageType.Raw, "\"a\\u0000b\"", "a\0b");
                var raw = (RawMailMessage)Read(memory.Response(1, message)).Message;
                Assert.That(raw.TryGetText(out var text), Is.True); Assert.That(text, Is.EqualTo("a\0b"));
            }
        }
        [Test]
        public void PreservesPayloadJsonWhenEscapingMakesItLargerThanTwoMiB()
        {
            const int count = 350000;
            string text = new string('\0', count);
            string payload = "\"" + text.Replace("\0", "\\u0000") + "\"";
            MailboxResponse response;
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                memory.Message(message, MailMessageKind.Raw, MailParseStatus.Parsed, MailMessageType.Raw, payload, text);
                response = Read(memory.Response(1, message));
            }
            Assert.That(response.Message.PayloadJson, Is.EqualTo(payload));
            Assert.That(((RawMailMessage)response.Message).TryGetText(out var copied), Is.True);
            Assert.That(copied, Is.EqualTo(text));
        }

        [Test]
        public void ConsumerExceptionOnMailboxFailureDoesNotEscapeTheApi()
        {
            bool previous = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            int calls = 0;
            try
            {
                Assert.DoesNotThrow(() => API.Mailbox.Sync(response =>
                {
                    ++calls;
                    Assert.That(response.Success, Is.False);
                    throw new InvalidOperationException("Expected consumer callback failure");
                }));
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previous;
            }
        }

        [Test]
        public void CopiesLargeNativeTextWithoutAnIndependentManagedLimit()
        {
            string text = new string('x', 2097153);
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                memory.Message(message, MailMessageKind.Raw, MailParseStatus.Parsed, MailMessageType.Raw, "\"" + text + "\"", text);
                var raw = (RawMailMessage)Read(memory.Response(1, message)).Message;
                Assert.That(raw.TryGetText(out var copied), Is.True);
                Assert.That(copied, Is.EqualTo(text));
            }
        }

        [Test]
        public void RejectsNegativeNativeTextLength()
        {
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                memory.Message(message, MailMessageKind.Raw, MailParseStatus.Parsed, MailMessageType.Raw, "\"hello\"", "hello");
                Marshal.WriteInt32(message, 20 + 7 * IntPtr.Size, -1);
                Assert.Throws<TargetInvocationException>(() => Read(memory.Response(1, message)));
            }
        }

        [Test]
        public void ReadsAllNumericStatusesAsEnums()
        {
            using (var memory = new Buffer())
            {
                int stride = 28 + 7 * IntPtr.Size;
                IntPtr messages = memory.Allocate(3 * stride);
                for (int status = 0; status <= 2; ++status)
                    memory.Message(IntPtr.Add(messages, status * stride), MailMessageKind.Raw, MailParseStatus.Parsed,
                        MailMessageType.Raw, "null", status: (MailMessageStatus)status);
                var response = Read(memory.Response(3, messages));
                Assert.That(response.Messages[0].Status, Is.EqualTo(MailMessageStatus.Unread));
                Assert.That(response.Messages[1].Status, Is.EqualTo(MailMessageStatus.Read));
                Assert.That(response.Messages[2].Status, Is.EqualTo(MailMessageStatus.Processed));
            }
        }
        [Test]
        public void RejectsInvalidNumericStatusBeforeReadingStrings()
        {
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                foreach (int status in new[] {-1, 3, int.MaxValue})
                {
                    Marshal.WriteInt32(message, 12, status);
                    Assert.Throws<TargetInvocationException>(() => Read(memory.Response(1, message)));
                }
            }
        }
        [Test]
        public void RejectsInvalidNumericTypeBeforeReadingStrings()
        {
            using (var memory = new Buffer())
            {
                IntPtr message = memory.Allocate(28 + 7 * IntPtr.Size);
                foreach (int type in new[] {-1, 32768, int.MaxValue})
                {
                    Marshal.WriteInt32(message, 16, type);
                    Assert.Throws<TargetInvocationException>(() => Read(memory.Response(1, message)));
                }
            }
        }
        [Test]
        public void RejectsMalformedNativeArrayBeforeDereferencing()
        {
            using (var memory = new Buffer())
            {
                Assert.Throws<TargetInvocationException>(() => Read(memory.Response(101, IntPtr.Zero)));
                Assert.Throws<TargetInvocationException>(() => Read(memory.Response(1, IntPtr.Zero)));
            }
        }
    }
}
