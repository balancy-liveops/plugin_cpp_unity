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
            /// <summary>Opt in to the Xsolla selector; checkout mode is configured on the server. No API key or bearer token belongs in these options.</summary>
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
            public string nextBefore, error, grantId;
            public bool applied, requiresPersistence, acknowledged;
        }
        [Serializable] private class PresentationShow
        { public string type, requestId, title, nativeLabel; public bool browserPopup; }
        [Serializable] private class UnavailablePresentationShow : PresentationShow
        { public string xsollaUnavailable; }
        [Serializable] private class PresentationUpdate
        { public string type, requestId, state, message; }
        [Serializable] private class PresentationCheckoutUpdate : PresentationUpdate
        { public string checkoutUrl; }
        [Serializable] internal class PresentationMessage
        {
            public string type, requestId, title, nativeLabel, state, message, choice;
            public bool browserPopup, popupOpened;
            public string checkoutUrl, xsollaUnavailable;
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
        private static string recoverySession;
        private static int recoveryEpoch;
        private static int generation;
        private static bool nativeExportsUnavailable;
        private static int nextCallbackId;
        private const int MaximumDeferredReads = 64;
        private sealed class DeferredOrder { internal string OrderId, GrantId, RequestId, ProductId, RequestedProductId; }
        private static readonly Queue<string> deferredReadQueue = new Queue<string>();
        private static string deferredSession;
        private static int deferredEpoch;
        private static readonly Dictionary<string, DeferredOrder> deferredOrders = new Dictionary<string, DeferredOrder>();
        private static readonly Dictionary<string, Actions.BalancyProductInfo> deferredReports = new Dictionary<string, Actions.BalancyProductInfo>();
        private static readonly HashSet<string> reportedGrants = new HashSet<string>();
        private static readonly Dictionary<int, TaskCompletionSource<string>> requests = new Dictionary<int, TaskCompletionSource<string>>();
        private static readonly LibraryMethods.API.XsollaCallback nativeCallback = NativeResponse;

        internal static void Configure(XsollaOptions value)
        {
            Stop();
            nativeExportsUnavailable = false;
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
            deferredReports.Clear();
            deferredOrders.Clear(); deferredReadQueue.Clear();
        }
        internal static bool TryIntercept(Actions.BalancyProductInfo product, Action<bool, string> callback, Action native)
        {
            if (options == null || !options.Enabled) return false;
            if (current != null) { SafeCallback(callback, false, "xsolla_purchase_in_progress"); return true; }
            try
            {
                Prepared prepared = null;
                string unavailable = null, session = null;
                try
                {
                    if (nativeExportsUnavailable) throw new InvalidOperationException("xsolla_native_api_unavailable");
                    prepared = JsonUtility.FromJson<Prepared>(Prepare(product));
                    if (prepared == null || string.IsNullOrEmpty(prepared.productId) || !Guid.TryParse(prepared.requestId, out _) || prepared.context == null)
                        throw new InvalidOperationException("xsolla_purchase_context_unavailable");
                    session = SessionIdentity();
                    if (string.IsNullOrEmpty(session)) throw new InvalidOperationException("xsolla_session_unavailable");
                }
                catch (Exception error)
                {
                    if (error is EntryPointNotFoundException || error is DllNotFoundException) nativeExportsUnavailable = true;
                    prepared = null;
                    unavailable = "Xsolla checkout is not available right now. You can continue with your regular payment method.";
                }
                var purchase = new Purchase(product, prepared, callback, native, unavailable, session);
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
            private readonly string unavailable;
            private readonly Action<bool, string> callback;
            private readonly Action native;
            private readonly string session;
            private readonly int epoch = generation;
            private readonly XsollaOptions config = options;
            private IDisposable lease;
            private bool finished, ready, started, busy, completed, callbackReported, presentationDismissed;
            private string orderId;
            private bool checkoutAttempted;
            internal Purchase(Actions.BalancyProductInfo product, Prepared prepared, Action<bool, string> callback, Action native, string unavailable, string session)
            { Id = prepared?.requestId ?? Guid.NewGuid().ToString(); this.product = product; this.prepared = prepared; this.callback = callback; this.native = native; this.unavailable = unavailable; this.session = session; }
            private void Guard() { if (finished || epoch != generation || (unavailable == null && session != SessionIdentity())) throw new OperationCanceledException("xsolla_session_changed"); }
            internal void Show()
            {
                InstallPopup(Id);
                var acquired = Present(() => ShowWhenReady(), PresentationFailed);
                if (finished) acquired?.Dispose(); else lease = acquired;
            }
            private async void ShowWhenReady()
            {
                try
                {
                    for (int i = 0; i < 50 && !ready && !presentationDismissed; i++)
                    {
                        Guard();
                        PresentationShow show = unavailable == null ? new PresentationShow() : new UnavailablePresentationShow { xsollaUnavailable = unavailable };
                        show.type = "paymentChoiceShow"; show.requestId = Id; show.title = "Choose how to pay";
                        show.nativeLabel = NativeLabel(config); show.browserPopup = BrowserPopup;
                        Send(JsonUtility.ToJson(show));
                        await Task.Delay(200);
                    }
                    if (!ready && !presentationDismissed) Finish(false, "xsolla_payment_view_timeout");
                }
                catch (OperationCanceledException) { Finish(false, "xsolla_session_changed"); }
                catch (Exception error) { Finish(false, error.Message); }
            }
            internal void Receive(PresentationMessage message)
            {
                try { Guard(); } catch (OperationCanceledException) { Finish(false, "xsolla_session_changed"); return; }
                if (message.type == "paymentChoiceReady") { ready = true; return; }
                if (completed)
                {
                    if (message.choice == "cancel") Finish(true, null); // Success button is Done.
                    return;
                }
                if (message.type == "paymentChoiceRetry")
                {
                    if (unavailable != null) return;
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
                    if (unavailable != null) return;
                    if (BrowserPopup && !message.popupOpened) { Update("error", "Allow popups to open secure checkout"); return; }
                    started = true; Run();
                }
            }
            private void Update(string state, string message, string checkoutUrl = null)
            {
                if (presentationDismissed) return;
                object payload = checkoutUrl == null
                    ? new PresentationUpdate { type = "paymentChoiceUpdate", requestId = Id, state = state, message = message }
                    : new PresentationCheckoutUpdate { type = "paymentChoiceUpdate", requestId = Id, state = state, message = message, checkoutUrl = checkoutUrl };
                Send(JsonUtility.ToJson(payload));
            }
            private async void Run()
            {
                if (busy || finished || completed) return;
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
                            await Apply(orderId, product, session, epoch, prepared, status.order);
                            Guard(); completed = true;
                            Update("success", "Payment completed");
                            ReportResult(true, null);
                            if (presentationDismissed) Finish(true, null);
                            return;
                        }
                        if (status.order.state == "canceled" || status.order.state == "creation_failed" || status.order.reconciliationRequired)
                            throw new InvalidOperationException(status.order.reconciliationRequired ? "xsolla_reconciliation_required" : "xsolla_order_" + status.order.state);
                        await Task.Delay(config.PollIntervalMs);
                    }
                    throw new InvalidOperationException("xsolla_purchase_pending");
                }
                catch (OperationCanceledException) { Finish(false, "xsolla_session_changed"); }
                catch (Exception error)
                {
                    if (!finished)
                    {
                        if (presentationDismissed) Finish(false, error.Message);
                        else Update("error", error.Message);
                    }
                }
                finally { busy = false; }
            }
            private void PresentationFailed(string error)
            {
                if (finished) return;
                if (!started) { Finish(false, error); return; }
                // A renderer closure is a presentation event, never a payment cancellation.
                presentationDismissed = true;
                Release();
                if (completed) Finish(true, null);
                else if (!busy) Finish(false, "xsolla_purchase_pending");
            }
            private void ReportResult(bool success, string error)
            {
                if (callbackReported) return;
                callbackReported = true;
                SafeCallback(callback, success, error);
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
                if (prepared == null || checkoutAttempted || epoch != generation || session != SessionIdentity()) return;
                try { await Request("discard", "{\"requestId\":" + Quote(Id) + "}"); await Persist(); }
                catch (Exception error) { Debug.LogWarning("Xsolla unused reservation cleanup: " + error.Message); }
            }
            internal void Finish(bool success, string error)
            {
                if (finished) return;
                finished = true; Release();
                DiscardReservation();
                if (ReferenceEquals(current, this)) current = null;
                ReportResult(success, error);
            }
        }
        internal static Task RecoverPurchases()
        {
            if (options == null || !options.Enabled) return Task.CompletedTask;
            if (nativeExportsUnavailable) return Task.FromException(new InvalidOperationException("xsolla_native_api_unavailable"));
            var session = SessionIdentity();
            if (recovery != null && !recovery.IsCompleted && recoveryEpoch == generation && recoverySession == session) return recovery;
            recoveryEpoch = generation; recoverySession = session;
            return recovery = Recover();
        }
        private static async Task Recover()
        {
            var epoch = generation; var session = SessionIdentity(); string cursor = null;
            EnsureSession(session, epoch);
            InitializeDeferredScope(session, epoch);
            var visited = new HashSet<string>();
            var errors = new List<Exception>();
            // Independent paid orders have priority over failed/terminal finalizations.
            // A single order failure is reported after the other orders are processed.
            var cursors = new HashSet<string>();
            try
            {
                do
                {
                    EnsureSession(session, epoch);
                    var result = Parse(await Request("pending", cursor == null ? "{}" : "{\"before\":" + Quote(cursor) + "}"));
                    EnsureSession(session, epoch);
                    foreach (var order in result.orders ?? Array.Empty<Order>())
                    {
                        if (order == null || order.state != "paid" || order.acknowledged || !visited.Add(order.id)) continue;
                        try
                        {
                            if (order.reconciliationRequired) throw new InvalidOperationException("xsolla_reconciliation_required");
                            await Apply(order.id, Descriptor(order), session, epoch, observedOrder: order);
                        }
                        catch (Exception error) { EnsureSession(session, epoch); errors.Add(error); }
                    }
                    cursor = result.nextBefore;
                    if (!string.IsNullOrEmpty(cursor) && !cursors.Add(cursor)) throw new InvalidOperationException("xsolla_pending_cursor_repeated");
                } while (!string.IsNullOrEmpty(cursor));
            }
            catch (Exception error) { EnsureSession(session, epoch); errors.Add(error); }
            // An accepted ACK with a lost response disappears from server pending.
            // Keep every known order until ACK/scope change; bound GETs, not retention.
            // Rotate every attempted order so terminal/failed entries cannot starve later entries.
            int remaining = deferredReadQueue.Count, reads = 0;
            while (remaining-- > 0 && reads < MaximumDeferredReads)
            {
                var orderId = deferredReadQueue.Dequeue();
                if (!deferredOrders.TryGetValue(orderId, out var deferred)) continue;
                deferredReadQueue.Enqueue(orderId);
                if (!visited.Add(orderId)) continue;
                reads++;
                try
                {
                    EnsureSession(session, epoch);
                    var status = Parse(await Request("get", "{\"orderId\":" + Quote(orderId) + "}"));
                    EnsureSession(session, epoch);
                    ValidateDeferredOrder(status.order, deferred);
                    if (status.order.state != "paid" || status.order.reconciliationRequired)
                        throw new InvalidOperationException("xsolla_reconciliation_required");
                    await Apply(orderId, Descriptor(status.order), session, epoch, observedOrder: status.order);
                }
                catch (Exception error) { EnsureSession(session, epoch); errors.Add(error); }
            }
            if (errors.Count > 0) throw new InvalidOperationException("xsolla_recovery_incomplete", new AggregateException(errors));
        }
        private static async Task Apply(string orderId, Actions.BalancyProductInfo product, string session, int epoch, Prepared livePurchase = null, Order observedOrder = null)
        {
            EnsureSession(session, epoch);
            InitializeDeferredScope(session, epoch);
            if (deferredOrders.TryGetValue(orderId, out var expected) && observedOrder != null)
                ValidateDeferredOrder(observedOrder, expected);
            var completion = Parse(await Complete(orderId));
            EnsureSession(session, epoch);
            ValidateOrder(completion.order, orderId, livePurchase);
            if (observedOrder != null) ValidateObservedIdentity(completion.order, observedOrder);
            if (deferredOrders.TryGetValue(orderId, out expected)) ValidateDeferredOrder(completion.order, expected);
            if (completion.grant == null || string.IsNullOrEmpty(completion.grant.grantId) || completion.order.state != "paid" || completion.order.reconciliationRequired)
                throw new InvalidOperationException("xsolla_unverified_completion");
            if (deferredOrders.TryGetValue(orderId, out var previous) && previous.GrantId != completion.grant.grantId)
                throw new InvalidOperationException("xsolla_grant_identity_mismatch");
            var reportKey = session + ":" + completion.grant.grantId;
            // A core checkpoint may fail after applying its durable-ledger candidate.
            // Only a subsequent successful verified completion may restore reporting:
            // authenticated unacknowledged orders remain eligible even if applied:false.
            // An acknowledged historical receipt cannot create a new notification.
            bool verifiedUnacknowledged = observedOrder != null && !observedOrder.acknowledged && !completion.order.acknowledged;
            if ((completion.applied || verifiedUnacknowledged) && product != null && !reportedGrants.Contains(reportKey))
                deferredReports[reportKey] = product;
            if (!deferredOrders.ContainsKey(orderId)) deferredReadQueue.Enqueue(orderId);
            deferredOrders[orderId] = new DeferredOrder { OrderId = orderId, GrantId = completion.grant.grantId,
                RequestId = completion.order.requestId, ProductId = completion.order.productId,
                RequestedProductId = completion.order.requestedProductId };
            if (completion.requiresPersistence) await Persist();
            EnsureSession(session, epoch);
            // The applied ledger survives ack failures. Only a correlated acknowledgment
            // permits definitive callbacks/Success; retry reconciles the existing grant.
            var acknowledgment = await Request("ack", "{\"orderId\":" + Quote(orderId) + ",\"grantId\":" + Quote(completion.grant.grantId) + "}");
            EnsureSession(session, epoch);
            ValidateAcknowledgment(acknowledgment, completion.grant.grantId);
            deferredOrders.Remove(orderId);
            // A live retry can have applied the grant before a failed durability barrier.
            // The same-scope report is sent once after a validated acknowledgment.
            if (deferredReports.TryGetValue(reportKey, out var reportProduct) && reportedGrants.Add(reportKey))
            {
                deferredReports.Remove(reportKey);
                var payment = new Core.PaymentInfo { ProductId = completion.grant.baseProductId, OrderId = completion.grant.orderId,
                    Price = completion.grant.priceBalancy, PriceUSD = completion.grant.priceUSD, Currency = "USD",
                    Receipt = "{\"orderId\":" + Quote(orderId) + "}" };
                try { reportProduct.ReportThePurchase(payment); } catch (Exception error) { Debug.LogException(error); }
            }
        }
        private static void InitializeDeferredScope(string session, int epoch)
        {
            if (deferredSession == session && deferredEpoch == epoch) return;
            deferredSession = session; deferredEpoch = epoch;
            deferredOrders.Clear(); deferredReadQueue.Clear(); deferredReports.Clear();
        }
        internal static void ValidateAcknowledgment(string json, string grantId)
        {
            if (!HasStrictAcknowledgmentFields(json)) throw new InvalidOperationException("xsolla_invalid_acknowledgment");
            var acknowledgment = Parse(json);
            if (!acknowledgment.acknowledged || acknowledgment.grantId != grantId)
                throw new InvalidOperationException("xsolla_invalid_acknowledgment");
        }
        // JsonUtility coerces numeric/string booleans. The ACK endpoint's strict
        // two-field object schema requires literal true and a JSON string grant ID,
        // in either order. An anchored schema rejects nested/duplicate/escaped keys.
        private static bool HasStrictAcknowledgmentFields(string json)
        {
            if (json == null) return false;
            const string space = @"[ \t\r\n]*";
            const string jsonString = @"""(?:[^""\\\x00-\x1F]|\\(?:[""\\/bfnrt]|u[0-9a-fA-F]{4}))*""";
            var acknowledged = @"""acknowledged""" + space + ":" + space + "true";
            var grant = @"""grantId""" + space + ":" + space + jsonString;
            var schema = @"\A" + space + @"\{" + space + "(?:" + acknowledged + space + "," + space + grant + "|" +
                grant + space + "," + space + acknowledged + ")" + space + @"\}" + space + @"\z";
            return System.Text.RegularExpressions.Regex.IsMatch(json, schema);
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
        private static void ValidateObservedIdentity(Order order, Order observed)
        {
            ValidateOrder(order, observed.id, null);
            if ((!string.IsNullOrEmpty(observed.requestId) && order.requestId != observed.requestId) ||
                (!string.IsNullOrEmpty(observed.productId) && order.productId != observed.productId) ||
                (!string.IsNullOrEmpty(observed.requestedProductId) && order.requestedProductId != observed.requestedProductId))
                throw new InvalidOperationException("xsolla_order_identity_mismatch");
        }
        private static void ValidateDeferredOrder(Order order, DeferredOrder expected)
        {
            ValidateObservedIdentity(order, new Order { id = expected.OrderId, requestId = expected.RequestId,
                productId = expected.ProductId, requestedProductId = expected.RequestedProductId });
            if (!string.IsNullOrEmpty(order.grantId) && order.grantId != expected.GrantId)
                throw new InvalidOperationException("xsolla_grant_identity_mismatch");
        }
        private static string ContextJson(Context context)
        {
            var fields = new List<string>();
            void Add(string name, string value) { if (!string.IsNullOrEmpty(value)) fields.Add(Quote(name) + ":" + Quote(value)); }
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
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || (uri.Host != "sandbox-secure.xsolla.com" && uri.Host != "secure.xsolla.com") ||
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
                StoreItemUnnyId = order.context.storeItemUnnyId, OfferInstanceId = order.context.offerInstanceId, OfferUnnyId = !string.IsNullOrEmpty(order.context.offerUnnyId) ? order.context.offerUnnyId : order.context.slotUnnyId };
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
