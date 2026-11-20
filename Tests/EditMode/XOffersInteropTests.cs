using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Balancy.XOffers;
using NUnit.Framework;

namespace Balancy.Tests
{
    public class XOffersInteropTests
    {
        private static T Read<T>(IntPtr ptr) where T : Balancy.Core.Responses.ResponseData, new()
        {
            var type = typeof(API).Assembly.GetType("Balancy.XOffers.XOffersInterop", true);
            return (T)type.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(T)).Invoke(null, new object[] { ptr });
        }
        private static T Copy<T>(string json, string context = null) where T : Balancy.Core.Responses.ResponseData, new()
        {
            IntPtr data = Marshal.AllocHGlobal(5 + 3 * IntPtr.Size);
            IntPtr text = Utf8(json), ctx = context == null ? IntPtr.Zero : Utf8(context);
            try
            {
                Marshal.WriteByte(data, 1); Marshal.WriteInt32(data, 1, 0); Marshal.WriteIntPtr(data, 5, IntPtr.Zero);
                Marshal.WriteIntPtr(data, 5 + IntPtr.Size, text); Marshal.WriteIntPtr(data, 5 + 2 * IntPtr.Size, ctx);
                return Read<T>(data);
            }
            finally { Marshal.FreeHGlobal(data); Marshal.FreeHGlobal(text); if (ctx != IntPtr.Zero) Marshal.FreeHGlobal(ctx); }
        }
        private static IntPtr Utf8(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\0"); var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length); return ptr;
        }
        [Test]
        public void CopiesContextBeforeNativeMemoryIsFreed()
        {
            const string context = "{\"identity\":{\"game_id\":\"game\",\"user_id\":\"22\",\"external\":null,\"signature_version\":1,\"signature\":\"signed\"},\"targets\":[1,7],\"locale\":\"en\",\"origin\":null}";
            var result = Copy<XOffersContextResponse>("{\"baseUrl\":\"https://example.com\",\"sourceEnabled\":true,\"targetEnabled\":true,\"context\":" + context + "}", context);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Context.Identity.UserId, Is.EqualTo("22"));
            Assert.That(result.Context.Targets.Count, Is.EqualTo(2));
            Assert.That(result.ContextJson, Is.EqualTo(context));
        }
        [Test]
        public void ExposesPendingCompletionSeparatelyFromConfirmedCompletion()
        {
            var pending = Copy<XOffersCompletionResponse>("{\"pending\":true,\"request_id\":\"job\",\"assignment_id\":null,\"state\":\"PENDING\",\"completed_at\":null,\"duplicate\":false}");
            Assert.That(pending.Success && pending.Pending, Is.True);
            Assert.That(pending.State, Is.EqualTo(XOffersAssignmentState.Pending));
            var done = Copy<XOffersCompletionResponse>("{\"pending\":false,\"request_id\":\"result\",\"assignment_id\":\"assignment\",\"state\":\"COMPLETED\",\"completed_at\":\"date\",\"duplicate\":true}");
            Assert.That(done.Pending, Is.False); Assert.That(done.Duplicate, Is.True);
            Assert.That(done.State, Is.EqualTo(XOffersAssignmentState.Completed));
        }
        [Test]
        public void ParsesCurrencyAmountsWithoutTruncatingToInt32()
        {
            var type = typeof(Balancy.Mail.XOffersRewardMessage);
            var constructor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            var message = (Balancy.Mail.XOffersRewardMessage)constructor.Invoke(new object[] {
                "{\"grantId\":\"grant\",\"reward\":{\"type\":\"currency\",\"currencyId\":\"coins\",\"amount\":9007199254740991},\"content\":{\"title\":\"Привет 🌍\"}}"
            });
            Assert.That(message.Kind, Is.EqualTo(Balancy.Mail.MailMessageKind.XOffersReward));
            Assert.That(message.Reward.Type, Is.EqualTo(Balancy.Mail.XOffersRewardType.Currency));
            Assert.That(message.Reward.Amount, Is.EqualTo(9007199254740991L));
            Assert.That(message.Content.Title, Is.EqualTo("Привет 🌍"));
        }
    }
}
