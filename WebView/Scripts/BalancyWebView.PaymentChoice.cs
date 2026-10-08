using System;

namespace Balancy.WebView
{
    public partial class BalancyWebView
    {
        // A lease changes presentation only. It never changes CurrentViewId or owner.
        public IDisposable BeginPaymentPresentation(Action ready, Action<string> failed)
        {
            var lease = new PaymentPresentation(this, ready, failed);
            try { lease.Begin(); }
            catch { lease.Dispose(); throw; }
            return lease;
        }

        private sealed class PaymentPresentation : IDisposable
        {
            private readonly BalancyWebView host;
            private readonly Action ready;
            private readonly Action<string> failed;
            private readonly bool persistent, wasVisible, created;
            private bool active = true, loaded;
            internal PaymentPresentation(BalancyWebView host, Action ready, Action<string> failed)
            {
                this.host = host; this.ready = ready; this.failed = failed;
                persistent = host.Persistent.Enabled;
                wasVisible = host._isWebViewOpen || host._isWebViewEmbedded;
                created = !persistent && !wasVisible;
            }
            internal void Begin()
            {
                host.OnClosed += Closed;
                if (persistent)
                    host.PrepareWebView(() => {
                        if (!active) return;
                        ApplyPersistentWindowSize();
                        _balancyShowWebView();
                        host._isWebViewOpen = true;
                        Loaded(true);
                    }, Failed);
                else if (created)
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
            private void Closed() { Failed("Payment presentation closed"); }
            public void Dispose()
            {
                if (!active) return;
                active = false;
                host.OnClosed -= Closed;
                host.OnLoadCompleted -= Loaded;
                if (created && host._isWebViewOpen) host.CloseWebView();
                else if (persistent && !wasVisible && host.Persistent.Enabled) host.HideWebView();
            }
        }
    }
}
