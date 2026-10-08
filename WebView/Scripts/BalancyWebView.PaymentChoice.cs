using System;

namespace Balancy.WebView
{
    public partial class BalancyWebView
    {
        private PaymentPresentation _paymentPresentation;
        public event Action OnPaymentBackgroundClosed;

        // A lease changes presentation only. It never changes CurrentViewId or owner.
        public IDisposable BeginPaymentPresentation(Action ready, Action<string> failed)
        {
            if (_paymentPresentation != null) throw new InvalidOperationException("A payment presentation is already active");
            var lease = new PaymentPresentation(this, ready, failed);
            _paymentPresentation = lease;
            try { lease.Begin(); }
            catch { lease.Dispose(); throw; }
            return lease;
        }

        // Closing game content must not destroy the independent checkout overlay.
        // Return true when the payment lease retains the native renderer.
        public bool TryClosePaymentBackground()
        {
            if (_paymentPresentation == null) return false;
            _paymentPresentation.BackgroundClosed();
            if (Persistent.Enabled) Persistent.Close();
            else OnPaymentBackgroundClosed?.Invoke();
            return true;
        }

        internal sealed class PaymentPresentationState
        {
            internal readonly bool Persistent, Created;
            private readonly bool wasVisible;
            private bool backgroundReleased;
            internal PaymentPresentationState(bool persistent, bool visible)
            { Persistent = persistent; wasVisible = visible; Created = !persistent && !visible; }
            internal void BackgroundClosed() => backgroundReleased = true;
            internal bool ShouldCloseRenderer => !Persistent && (Created || backgroundReleased);
            internal bool ShouldHideRenderer(bool backgroundVisible) => Persistent && !backgroundVisible && (!wasVisible || backgroundReleased);
        }

        private sealed class PaymentPresentation : IDisposable
        {
            private readonly BalancyWebView host;
            private readonly Action ready;
            private readonly Action<string> failed;
            private readonly PaymentPresentationState state;
            private bool active = true, loaded;
            internal PaymentPresentation(BalancyWebView host, Action ready, Action<string> failed)
            {
                this.host = host; this.ready = ready; this.failed = failed;
                state = new PaymentPresentationState(host.Persistent.Enabled, host._isWebViewOpen || host._isWebViewEmbedded);
            }
            internal void Begin()
            {
                host.OnClosed += Closed;
                host.Persistent.ExternalPresentationActive = true;
                if (state.Persistent)
                    host.PrepareWebView(() => {
                        if (!active) return;
                        ApplyPersistentWindowSize();
                        _balancyShowWebView();
                        host._isWebViewOpen = true;
                        Loaded(true);
                    }, Failed);
                else if (state.Created)
                {
                    host.OnLoadCompleted += Loaded;
                    const string html = "<!doctype html><html><head><meta name='viewport' content='width=device-width, initial-scale=1'></head><body></body></html>";
#if UNITY_WEBGL && !UNITY_EDITOR
                    bool opened = host.OpenWebViewHtml(html, "", "", "");
#else
                    var directory = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Balancy");
                    System.IO.Directory.CreateDirectory(directory);
                    var path = System.IO.Path.Combine(directory, "payment-choice-host.html");
                    System.IO.File.WriteAllText(path, html);
                    var size = ResolveWebViewSize();
                    bool opened = host.OpenWebView(ToWebViewUrl(path), "", "", size.x, size.y);
#endif
                    if (!opened) Failed("Payment WebView could not be opened");
                }
                else Loaded(true);
            }
            private void Loaded(bool success)
            {
                if (!active || loaded) return;
                host.OnLoadCompleted -= Loaded;
                if (!success) { Failed("Payment WebView failed to load"); return; }
                loaded = true;
                ready?.Invoke();
            }
            private void Failed(string error)
            {
                if (!active) return;
                Dispose();
                failed?.Invoke(error);
            }
            internal void BackgroundClosed()
            {
                state.BackgroundClosed();
            }
            internal void RendererClosed() => Failed("Payment renderer closed");
            private void Closed()
            {
                if (state.Persistent && host.Persistent.Enabled)
                {
                    state.BackgroundClosed();
                    return;
                }
                Failed("Payment presentation closed");
            }
            public void Dispose()
            {
                if (!active) return;
                active = false;
                host.OnClosed -= Closed;
                host.OnLoadCompleted -= Loaded;
                if (host._paymentPresentation == this) host._paymentPresentation = null;
                host.Persistent.ExternalPresentationActive = false;
                if (state.ShouldCloseRenderer)
                {
                    if (host._isWebViewEmbedded) host.CloseEmbedded();
                    else if (host._isWebViewOpen) host.CloseWebView();
                }
                else if (host.Persistent.Enabled && state.ShouldHideRenderer(host.Persistent.Visible)) host.HideWebView();
            }
        }
    }
}
