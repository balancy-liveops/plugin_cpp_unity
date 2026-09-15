using System;
using System.Collections.Generic;

namespace Balancy.WebView
{
    // Independent of Unity/native APIs so protocol transitions can be tested deterministically.
    internal sealed class PersistentViewState
    {
        internal bool Enabled { get; private set; }
        internal bool Preparing { get; private set; }
        internal bool Visible { get; set; }
        internal string ShellId { get; private set; }
        internal string CurrentId { get; private set; }
        internal string ClosingId { get; private set; }
        internal bool CanShow => Enabled && CurrentId == null;
        private readonly Func<string, bool> send;
        private readonly Action show, hide, destroy, closed;
        private readonly Action<string> released;
        private readonly Func<double> clock;
        private readonly double timeout;
        private readonly Dictionary<string, double> deadlines = new Dictionary<string, double>();
        private Action onPrepared, onReady;
        private Action<string> onPrepareFailed, onFailed;
        private Func<string, string> createMessage;
        private bool sent;

        internal PersistentViewState(Func<string, bool> send, Action show, Action hide, Action destroy,
            Action closed, Action<string> released, Func<double> clock, double timeout = 30)
        {
            this.send = send; this.show = show; this.hide = hide; this.destroy = destroy;
            this.closed = closed; this.released = released; this.clock = clock; this.timeout = timeout;
        }

        internal bool Prepare(Func<bool> start, Action ready, Action<string> failed)
        {
            if (Enabled && !Preparing) { ready?.Invoke(); return true; }
            onPrepared += ready; onPrepareFailed += failed;
            if (Preparing) return true;
            Enabled = Preparing = true;
            ShellId = Guid.NewGuid().ToString("N");
            deadlines["prepare"] = clock() + timeout;
            try { if (!start()) { Fail("Cannot start persistent shell"); return false; } }
            catch (Exception error) { Fail(error.Message); return false; }
            return true;
        }

        internal bool Show(Func<string, string> message, Action ready, Action<string> failed)
        {
            if (!CanShow) { failed?.Invoke("Another view is active or persistent mode is unavailable"); return false; }
            CurrentId = Guid.NewGuid().ToString("N");
            createMessage = message; onReady = ready; onFailed = failed; sent = false;
            // Tick dispatches after the caller associates this accepted id with its owner.
            return true;
        }

        private void Flush()
        {
            if (Preparing || ClosingId != null || CurrentId == null || sent) return;
            sent = true;
            deadlines["view"] = clock() + timeout;
            try { if (!send(createMessage(CurrentId))) Fail("Cannot send persistent view"); }
            catch (Exception error) { Fail(error.Message); }
        }

        internal void Close()
        {
            if (CurrentId == null) return;
            var id = CurrentId;
            var wasSent = sent;
            CurrentId = null; createMessage = null; onReady = null; onFailed = null; sent = false;
            deadlines.Remove("view"); Visible = false;
            hide();
            if (wasSent)
            {
                ClosingId = id;
                deadlines["clear"] = clock() + timeout;
                try { if (!send("{\"type\":\"clearView\",\"viewId\":\"" + id + "\"}")) Fail("Cannot clear persistent view"); }
                catch (Exception error) { Fail(error.Message); }
            }
            else released?.Invoke(id);
            closed();
        }

        internal bool Receive(string type, string viewId, string shellId, string error)
        {
            switch (type)
            {
                case "shellReady":
                    if (!Preparing || shellId != ShellId) return true;
                    deadlines.Remove("prepare"); Preparing = false;
                    var ready = onPrepared; onPrepared = null; onPrepareFailed = null;
                    Flush(); ready?.Invoke(); return true;
                case "shellError":
                    if (Enabled && shellId == ShellId) Fail(error ?? "Shell initialization failed");
                    return true;
                case "viewReady":
                    if (viewId != CurrentId || !sent || Visible) return true;
                    deadlines.Remove("view"); Visible = true; show();
                    var callback = onReady; onReady = null; callback?.Invoke(); return true;
                case "viewLoadError":
                    if (CurrentId == null || viewId != CurrentId) return true;
                    var failed = onFailed;
                    deadlines.Remove("view"); released?.Invoke(CurrentId);
                    CurrentId = null; createMessage = null; onReady = null; onFailed = null; sent = false;
                    Visible = false; hide(); closed(); failed?.Invoke(error ?? "View load failed"); return true;
                case "viewCleared":
                    if (ClosingId == null || viewId != ClosingId) return true;
                    deadlines.Remove("clear"); released?.Invoke(ClosingId); ClosingId = null;
                    Flush(); return true;
                default: return false;
            }
        }

        internal void Tick()
        {
            foreach (var deadline in deadlines)
                if (clock() >= deadline.Value) { Fail("Persistent WebView " + deadline.Key + " timed out"); return; }
            Flush();
        }

        internal void Fail(string error)
        {
            var preparationFailed = onPrepareFailed; var viewFailed = onFailed;
            bool hadView = CurrentId != null;
            Reset(); destroy();
            if (hadView) closed();
            preparationFailed?.Invoke(error); viewFailed?.Invoke(error);
        }

        internal void Reset()
        {
            if (CurrentId != null) released?.Invoke(CurrentId);
            if (ClosingId != null) released?.Invoke(ClosingId);
            Enabled = Preparing = Visible = sent = false;
            ShellId = CurrentId = ClosingId = null;
            onPrepared = onReady = null; onPrepareFailed = onFailed = null; createMessage = null;
            deadlines.Clear();
        }
    }
}
