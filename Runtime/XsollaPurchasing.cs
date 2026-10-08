using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Balancy
{
    [Serializable]
    public sealed class XsollaOptions
    {
        public bool Enabled;
        public string Country;
        public string Language = "en";
        public string NativeLabel;
        public int PollIntervalMs = 2000;
        public int TimeoutMs = 240000;
    }

    public static partial class API
    {
        public static class Xsolla
        {
            /// <summary>Opt in to the sandbox selector. No API key or bearer token belongs in these options.</summary>
            public static void Configure(XsollaOptions options) => XsollaPurchasing.Configure(options);
            /// <summary>Resume verified purchases after interruption. Closing Pay Station does not cancel an order.</summary>
            public static Task RecoverPurchases() => XsollaPurchasing.RecoverPurchases();
        }
    }

    internal static class XsollaPurchasing
    {
        [Serializable] internal class Context
        {
            public string type, storeItemUnnyId, offerInstanceId, offerUnnyId, slotUnnyId;
        }
        [Serializable] internal class Prepared { public string productId, requestId; public Context context; }
        [Serializable] internal class Order
        {
            public string id, state, productId, requestedProductId, requestId, sku, grantId, checkoutUrl, errorCode;
            public bool sandbox, acknowledged, reconciliationRequired;
            public Context context;
        }
        [Serializable] internal class Grant
        {
            public string grantId, baseProductId, orderId;
            public float priceUSD, priceBalancy;
        }
        [Serializable] internal class Result
        {
            public Order order;
            public Order[] orders;
            public Grant grant;
            public string nextBefore, error;
            public bool applied, requiresPersistence, acknowledged;
        }
        [Serializable] private class PresentationUpdate
        { public string type, requestId, state, message; }
        [Serializable] private class PresentationCheckoutUpdate : PresentationUpdate
        { public string checkoutUrl; }
        [Serializable] internal class PresentationMessage
        {
            public string type, requestId, title, nativeLabel, state, message, choice;
            public bool browserPopup, popupOpened;
            public string checkoutUrl;
        }
        internal static Func<string> SessionIdentity = () => Copy(LibraryMethods.API.balancyXsollaGetSessionIdentity());
        internal static Func<Actions.BalancyProductInfo, string> Prepare = PrepareNative;
        internal static Func<string, string, Task<string>> Request = RequestNative;
        internal static Func<string, Task<string>> Complete = CompleteNative;
        internal static Func<Action, Action<string>, IDisposable> Present = RenderViewsManager.PresentPaymentChoice;
        internal static Action<string> Send = RenderViewsManager.SendMessageToView;
        internal static Action<string> OpenCheckout = Application.OpenURL;
        // WebGL needs a real IDBFS sync barrier. Refuse to acknowledge without one.
        internal static Func<Task> Persist = () => {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Dispatch(id => LibraryMethods.API.balancyXsollaFlushStorage(id, nativeCallback));
#else
            return Task.CompletedTask; // C++ checked native whole-profile checkpoint before returning success.
#endif
        };
        internal static Action<string> InstallPopup = id => {
#if UNITY_WEBGL && !UNITY_EDITOR
            LibraryMethods.API.balancyXsollaInstallPopupOpener(id);
#endif
        };
        internal static Action<string> RemovePopup = id => {
#if UNITY_WEBGL && !UNITY_EDITOR
            LibraryMethods.API.balancyXsollaRemovePopupOpener(id);
#endif
        };
#if UNITY_WEBGL && !UNITY_EDITOR
        internal static bool BrowserPopup = true;
#else
        internal static bool BrowserPopup = false;
#endif
        private static XsollaOptions options;
        private static Purchase current;
        private static Task recovery;
        private static int generation;
        private static int nextCallbackId;
        private static readonly HashSet<string> reportedGrants = new HashSet<string>();
        private static readonly Dictionary<int, TaskCompletionSource<string>> requests = new Dictionary<int, TaskCompletionSource<string>>();
        private static readonly LibraryMethods.API.XsollaCallback nativeCallback = NativeResponse;

        internal static void Configure(XsollaOptions value)
        {
            Stop();
            options = null;
            if (value == null || !value.Enabled) { options = null; return; }
            if (value.Country == null || !System.Text.RegularExpressions.Regex.IsMatch(value.Country, "^[A-Z]{2}$"))
                throw new ArgumentException("Xsolla Country must be a two-letter uppercase country code");
            if (!System.Text.RegularExpressions.Regex.IsMatch(value.Language ?? "", "^[a-z]{2}(-[A-Z]{2})?$"))
                throw new ArgumentException("Xsolla Language must be a supported language code");
            if (value.NativeLabel != null && (string.IsNullOrWhiteSpace(value.NativeLabel) || value.NativeLabel.Length > 80))
                throw new ArgumentException("Xsolla NativeLabel must contain between 1 and 80 characters");
            options = new XsollaOptions { Enabled = true, Country = value.Country, Language = value.Language,
                NativeLabel = value.NativeLabel, PollIntervalMs = Math.Min(60000, Math.Max(250, value.PollIntervalMs)), TimeoutMs = Math.Min(3600000, Math.Max(1000, value.TimeoutMs)) };
            Start();
            if (Controller.IsReadyToUse) Resume();
        }
        internal static async void Resume()
        {
            if (options == null || !options.Enabled || !Controller.IsReadyToUse) return;
            try { await RecoverPurchases(); }
            catch (Exception error) { Debug.LogWarning("Xsolla recovery: " + error.Message); }
        }
        internal static void Start()
        {
            Controller.OnDataUpdated -= DataUpdated;
            if (options != null && options.Enabled) Controller.OnDataUpdated += DataUpdated;
        }
        private static async void DataUpdated(bool dictionaries, bool profile)
        {
            if (!profile || options == null) return;
            current?.Finish(false, "xsolla_session_changed");
            try { await RecoverPurchases(); } catch (Exception error) { Debug.LogWarning("Xsolla recovery: " + error.Message); }
        }
        internal static void Stop()
        {
            ++generation;
            Controller.OnDataUpdated -= DataUpdated;
            current?.Finish(false, "SDK stopped");
            current = null;
            foreach (var request in requests.Values) request.TrySetCanceled();
            requests.Clear();
            reportedGrants.Clear();
        }
        internal static bool TryIntercept(Actions.BalancyProductInfo product, Action<bool, string> callback, Action native)
        {
            if (options == null || !options.Enabled) return false;
            if (current != null) { SafeCallback(callback, false, "xsolla_purchase_in_progress"); return true; }
            try
            {
                var prepared = JsonUtility.FromJson<Prepared>(Prepare(product));
                if (prepared == null || string.IsNullOrEmpty(prepared.productId) || !Guid.TryParse(prepared.requestId, out _) || prepared.context == null)
                    throw new InvalidOperationException("xsolla_purchase_context_unavailable");
                var purchase = new Purchase(product, prepared, callback, native);
                current = purchase;
                purchase.Show();
            }
            catch (Exception error)
            {
                if (current != null) current.Finish(false, error.Message);
                else SafeCallback(callback, false, error.Message);
            }
            return true;
        }
        internal static bool ReceivePresentationMessage(string json)
        {
            PresentationMessage message;
            try { message = JsonUtility.FromJson<PresentationMessage>(json); } catch { return false; }
            if (message == null || (message.type != "paymentChoiceReady" && message.type != "paymentChoiceSelected" && message.type != "paymentChoiceRetry")) return false;
            if (current == null || message.requestId != current.Id) return true;
            current.Receive(message);
            return true;
        }
        private sealed class Purchase
        {
            internal readonly string Id;
            private readonly Actions.BalancyProductInfo product;
            private readonly Prepared prepared;
            private readonly Action<bool, string> callback;
            private readonly Action native;
            private readonly string session = SessionIdentity();
            private readonly int epoch = generation;
            private readonly XsollaOptions config = options;
            private IDisposable lease;
            private bool finished, ready, started, busy;
            private string orderId;
            private bool checkoutAttempted;
            internal Purchase(Actions.BalancyProductInfo product, Prepared prepared, Action<bool, string> callback, Action native)
            { Id = prepared.requestId; this.product = product; this.prepared = prepared; this.callback = callback; this.native = native; }
            private void Guard() { if (finished || epoch != generation || session != SessionIdentity()) throw new OperationCanceledException("xsolla_session_changed"); }
            internal void Show()
            {
                InstallPopup(Id);
                var acquired = Present(() => ShowWhenReady(), error => Finish(false, error));
                if (finished) acquired?.Dispose(); else lease = acquired;
            }
            private async void ShowWhenReady()
            {
                try
                {
                    for (int i = 0; i < 50 && !ready; i++)
                    {
                        Guard();
                        Send(JsonUtility.ToJson(new PresentationMessage { type = "paymentChoiceShow", requestId = Id,
                            title = "Choose how to pay", nativeLabel = NativeLabel(config), browserPopup = BrowserPopup }));
                        await Task.Delay(200);
                    }
                    if (!ready) Finish(false, "xsolla_payment_view_timeout");
                }
                catch (OperationCanceledException) { Finish(false, "xsolla_session_changed"); }
                catch (Exception error) { Finish(false, error.Message); }
            }
            internal void Receive(PresentationMessage message)
            {
                try { Guard(); } catch (OperationCanceledException) { Finish(false, "xsolla_session_changed"); return; }
                if (message.type == "paymentChoiceReady") { ready = true; return; }
                if (message.type == "paymentChoiceRetry")
                {
                    if (busy) return;
                    if (BrowserPopup && !message.popupOpened) { Update("error", "Allow popups to open secure checkout"); return; }
                    started = true; Run(); return;
                }
                if (message.choice == "cancel") { Finish(false, started ? "xsolla_purchase_pending" : "Purchase canceled"); return; }
                if (started || busy) return;
                if (message.choice == "native")
                {
                    Release(); finished = true; current = null;
                    DiscardReservation();
                    try { native(); } catch (Exception error) { SafeCallback(callback, false, error.Message); }
                }
                else if (message.choice == "xsolla")
                {
                    if (BrowserPopup && !message.popupOpened) { Update("error", "Allow popups to open secure checkout"); return; }
                    started = true; Run();
                }
            }
            private void Update(string state, string message, string checkoutUrl = null)
            {
                object payload = checkoutUrl == null
                    ? new PresentationUpdate { type = "paymentChoiceUpdate", requestId = Id, state = state, message = message }
                    : new PresentationCheckoutUpdate { type = "paymentChoiceUpdate", requestId = Id, state = state, message = message, checkoutUrl = checkoutUrl };
                Send(JsonUtility.ToJson(payload));
            }
            private async void Run()
            {
                if (busy || finished) return;
                busy = true;
                bool checkoutOpened = false;
                try
                {
                    Guard(); Update("preparing", "Preparing secure checkout…");
                    if (orderId == null)
                    {
                        await Persist(); // The reservation and reward snapshot must survive closing the app.
                        Guard();
                        checkoutAttempted = true;
                        var response = await Request("checkout", "{\"requestId\":" + Quote(Id) + ",\"productId\":" + Quote(prepared.productId) +
                            ",\"country\":" + Quote(config.Country) + ",\"language\":" + Quote(config.Language) + ",\"context\":" + ContextJson(prepared.context) + "}");
                        Guard();
                        var created = Parse(response);
                        ValidateOrder(created.order, null, prepared);
                        orderId = created.order.id;
                        if (created.order.state == "pending")
                        {
                            ValidateCheckoutUrl(created.order.checkoutUrl);
                            if (BrowserPopup) Update("pending", "Waiting for payment confirmation", created.order.checkoutUrl);
                            else OpenCheckout(created.order.checkoutUrl);
                            checkoutOpened = true;
                        }
                    }
                    Update("pending", "Waiting for payment confirmation. You can close this window and resume later.");
                    var deadline = DateTime.UtcNow.AddMilliseconds(config.TimeoutMs);
                    while (DateTime.UtcNow < deadline)
                    {
                        Guard();
                        var status = Parse(await Request("get", "{\"orderId\":" + Quote(orderId) + "}"));
                        Guard();
                        ValidateOrder(status.order, orderId, prepared);
                        if (!checkoutOpened && status.order.state == "pending" && !string.IsNullOrEmpty(status.order.checkoutUrl))
                        {
                            ValidateCheckoutUrl(status.order.checkoutUrl);
                            if (BrowserPopup) Update("pending", "Waiting for payment confirmation", status.order.checkoutUrl);
                            else OpenCheckout(status.order.checkoutUrl);
                            checkoutOpened = true;
                        }
                        if (status.order.state == "paid")
                        {
                            await Apply(orderId, product, session, epoch, prepared);
                            Guard(); Update("success", "Payment completed"); Finish(true, null); return;
                        }
                        if (status.order.state == "canceled" || status.order.state == "creation_failed" || status.order.reconciliationRequired)
                            throw new InvalidOperationException(status.order.reconciliationRequired ? "xsolla_reconciliation_required" : "xsolla_order_" + status.order.state);
                        await Task.Delay(config.PollIntervalMs);
                    }
                    throw new InvalidOperationException("xsolla_purchase_pending");
                }
                catch (OperationCanceledException) { Finish(false, "xsolla_session_changed"); }
                catch (Exception error) { if (!finished) Update("error", error.Message); }
                finally { busy = false; }
            }
            private void Release()
            {
                try { RemovePopup(Id); } catch (Exception error) { Debug.LogException(error); }
                try { Send(JsonUtility.ToJson(new PresentationMessage { type = "paymentChoiceHide", requestId = Id })); }
                catch (Exception error) { Debug.LogException(error); }
                try { lease?.Dispose(); } catch (Exception error) { Debug.LogException(error); }
                lease = null;
            }
            private async void DiscardReservation()
            {
                if (checkoutAttempted || epoch != generation || session != SessionIdentity()) return;
                try { await Request("discard", "{\"requestId\":" + Quote(Id) + "}"); await Persist(); }
                catch (Exception error) { Debug.LogWarning("Xsolla unused reservation cleanup: " + error.Message); }
            }
            internal void Finish(bool success, string error)
            {
                if (finished) return;
                finished = true; Release();
                DiscardReservation();
                if (ReferenceEquals(current, this)) current = null;
                SafeCallback(callback, success, error);
            }
        }
        internal static Task RecoverPurchases()
        {
            if (options == null || !options.Enabled) return Task.CompletedTask;
            if (recovery != null && !recovery.IsCompleted) return recovery;
            return recovery = Recover();
        }
        private static async Task Recover()
        {
            var epoch = generation; var session = SessionIdentity(); string cursor = null;
            var cursors = new HashSet<string>();
            do
            {
                EnsureSession(session, epoch);
                var result = Parse(await Request("pending", cursor == null ? "{}" : "{\"before\":" + Quote(cursor) + "}"));
                EnsureSession(session, epoch);
                foreach (var order in result.orders ?? Array.Empty<Order>())
                    if (order.state == "paid" && !order.acknowledged)
                        await Apply(order.id, Descriptor(order), session, epoch);
                cursor = result.nextBefore;
                if (!string.IsNullOrEmpty(cursor) && !cursors.Add(cursor)) throw new InvalidOperationException("xsolla_pending_cursor_repeated");
            } while (!string.IsNullOrEmpty(cursor));
        }
        private static async Task Apply(string orderId, Actions.BalancyProductInfo product, string session, int epoch, Prepared livePurchase = null)
        {
            EnsureSession(session, epoch);
            var completion = Parse(await Complete(orderId));
            EnsureSession(session, epoch);
            ValidateOrder(completion.order, orderId, livePurchase);
            if (completion.grant == null || string.IsNullOrEmpty(completion.grant.grantId) || completion.order.state != "paid")
                throw new InvalidOperationException("xsolla_unverified_completion");
            if (completion.requiresPersistence) await Persist();
            EnsureSession(session, epoch);
            // Completion already saved the coherent grant ledger and inventory. Ack failure is recoverable.
            try { await Request("ack", "{\"orderId\":" + Quote(orderId) + ",\"grantId\":" + Quote(completion.grant.grantId) + "}"); }
            catch (Exception error) { Debug.LogWarning("Xsolla payment acknowledgment pending: " + error.Message); }
            EnsureSession(session, epoch);
            // A live retry can have applied the grant before a failed durability barrier.
            // Recovery of a previous session reports only a newly applied grant.
            if ((completion.applied || livePurchase != null) && product != null && reportedGrants.Add(session + ":" + completion.grant.grantId))
            {
                var payment = new Core.PaymentInfo { ProductId = completion.grant.baseProductId, OrderId = completion.grant.orderId,
                    Price = completion.grant.priceBalancy, PriceUSD = completion.grant.priceUSD, Currency = "USD",
                    Receipt = "{\"orderId\":" + Quote(orderId) + "}" };
                try { product.ReportThePurchase(payment); } catch (Exception error) { Debug.LogException(error); }
            }
        }
        private static void EnsureSession(string session, int epoch)
        { if (epoch != generation || session != SessionIdentity()) throw new OperationCanceledException("xsolla_session_changed"); }
        private static void ValidateOrder(Order order, string orderId, Prepared purchase)
        {
            if (order == null || string.IsNullOrEmpty(order.id) || (orderId != null && order.id != orderId) ||
                (purchase != null && (order.requestId != purchase.requestId ||
                 (!string.IsNullOrEmpty(order.requestedProductId) && order.requestedProductId != purchase.productId))))
                throw new InvalidOperationException("xsolla_order_identity_mismatch");
        }
        private static string ContextJson(Context context)
        {
            var fields = new List<string>();
            void Add(string name, string value) { if (value != null) fields.Add(Quote(name) + ":" + Quote(value)); }
            Add("type", context.type); Add("storeItemUnnyId", context.storeItemUnnyId);
            Add("offerInstanceId", context.offerInstanceId); Add("offerUnnyId", context.offerUnnyId); Add("slotUnnyId", context.slotUnnyId);
            return "{" + string.Join(",", fields) + "}";
        }
        private static string Quote(string text)
        {
            var result = new System.Text.StringBuilder("\"");
            foreach (var character in text ?? "")
            {
                if (character == '"' || character == '\\') result.Append('\\').Append(character);
                else if (character < 32) result.Append("\\u").Append(((int)character).ToString("x4"));
                else result.Append(character);
            }
            return result.Append('"').ToString();
        }
        internal static void ValidateCheckoutUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "sandbox-secure.xsolla.com" ||
                !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort || uri.AbsolutePath != "/paystation4/" ||
                !string.IsNullOrEmpty(uri.Fragment) || !System.Text.RegularExpressions.Regex.IsMatch(uri.Query, "(?:^\\?|&)token=[^&#]+"))
                throw new InvalidOperationException("xsolla_invalid_checkout_url");
        }
        private static string NativeLabel(XsollaOptions config)
        {
            if (!string.IsNullOrEmpty(config.NativeLabel)) return config.NativeLabel;
            var configured = Controller.Config?.BalancyPlatform ?? Constants.BalancyPlatform.Undefined;
            if (configured == Constants.BalancyPlatform.IosAppStore) return "Apple App Store";
            if (configured == Constants.BalancyPlatform.AndroidGooglePlay) return "Google Play";
            return Application.platform == RuntimePlatform.IPhonePlayer ? "Apple App Store" :
                Application.platform == RuntimePlatform.Android ? "Google Play" : "Standard payment";
        }
        private static Result Parse(string json)
        {
            var result = JsonUtility.FromJson<Result>(json);
            if (result == null || !string.IsNullOrEmpty(result.error)) throw new InvalidOperationException(result?.error ?? "xsolla_invalid_response");
            return result;
        }
        private static Actions.BalancyProductInfo Descriptor(Order order)
        {
            if (order.context == null || !Enum.TryParse(order.context.type, out Actions.BalancyProductInfo.PurchaseType kind)) return null;
            return new Actions.BalancyProductInfo((Models.SmartObjects.StoreItem)null) { ProductId = order.productId, Type = kind,
                StoreItemUnnyId = order.context.storeItemUnnyId, OfferInstanceId = order.context.offerInstanceId, OfferUnnyId = order.context.offerUnnyId ?? order.context.slotUnnyId };
        }
        private static string PrepareNative(Actions.BalancyProductInfo product)
        {
            IntPtr owner = IntPtr.Zero, store = CMS.GetModelByUnnyId<Models.SmartObjects.StoreItem>(product.StoreItemUnnyId)?.GetRawPointer() ?? IntPtr.Zero;
            int kind;
            switch (product.Type)
            {
                case Actions.BalancyProductInfo.PurchaseType.StoreItem: kind = 0; owner = store; break;
                case Actions.BalancyProductInfo.PurchaseType.ShopSlot: kind = 1; owner = Profiles.System.ShopsInfo.FindShopSlot(product.OfferUnnyId)?.GetRawPointer() ?? IntPtr.Zero; break;
                case Actions.BalancyProductInfo.PurchaseType.Offer: kind = 2; owner = Profiles.System.SmartInfo.FindOfferInfo(product.OfferInstanceId)?.GetRawPointer() ?? IntPtr.Zero; break;
                case Actions.BalancyProductInfo.PurchaseType.OfferGroup: kind = 3; owner = Profiles.System.SmartInfo.FindOfferGroupInfo(product.OfferInstanceId)?.GetRawPointer() ?? IntPtr.Zero; break;
                default: throw new InvalidOperationException("xsolla_unsupported_context");
            }
            return Copy(LibraryMethods.API.balancyXsollaPreparePurchase(kind, owner, store));
        }
        private static string Copy(IntPtr ptr) => ptr == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(ptr) ?? "";
        private static Task<string> RequestNative(string action, string json) => Dispatch(id => LibraryMethods.API.balancyXsollaRequest(action, json, id, nativeCallback));
        private static Task<string> CompleteNative(string orderId) => Dispatch(id => LibraryMethods.API.balancyXsollaComplete(orderId, id, nativeCallback));
        private static Task<string> Dispatch(Action<int> send)
        {
            var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously); var id = ++nextCallbackId; requests.Add(id, source);
            try { send(id); } catch (Exception error) { requests.Remove(id); source.TrySetException(error); }
            return source.Task;
        }
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.API.XsollaCallback))]
        private static void NativeResponse(int id, bool success, string json)
        {
            if (!requests.TryGetValue(id, out var source)) return;
            requests.Remove(id);
            if (success) source.TrySetResult(json);
            else
            {
                string error = "xsolla_request_failed";
                try { error = JsonUtility.FromJson<Result>(json)?.error ?? error; } catch { }
                source.TrySetException(new InvalidOperationException(error));
            }
        }
        private static void SafeCallback(Action<bool, string> callback, bool success, string error)
        { try { callback?.Invoke(success, error); } catch (Exception exception) { Debug.LogException(exception); } }
    }
}
