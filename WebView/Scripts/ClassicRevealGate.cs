using System;

namespace Balancy.WebView
{
    // A classic (full-page) view opened hidden is revealed once: by the page's ready signal, an error, or the
    // timeout. Independent of Unity so the transitions can be tested deterministically.
    internal sealed class ClassicRevealGate
    {
        private readonly Action<string> reveal;
        private readonly Func<double> clock;
        private double deadline = -1;
        private bool loading, revealedWhileLoading;
        private Action shown, failed;

        internal ClassicRevealGate(Action<string> reveal, Func<double> clock)
        {
            this.reveal = reveal;
            this.clock = clock;
        }

        internal bool Pending => deadline >= 0;

        // As in persistent mode, shown runs when the view becomes visible and failed when it dies before that;
        // closing it first runs neither.
        internal void Begin(double timeoutSeconds, Action shown = null, Action failed = null)
        {
            deadline = clock() + timeoutSeconds;
            loading = true;
            revealedWhileLoading = false;
            this.shown = shown;
            this.failed = failed;
        }

        internal void Cancel()
        {
            deadline = -1;
            loading = revealedWhileLoading = false;
            shown = failed = null;
        }

        internal void Signal(string reason)
        {
            if (!Pending) return;
            deadline = -1;
            if (loading) revealedWhileLoading = true;
            var callback = shown;
            shown = failed = null;
            reveal(reason);
            callback?.Invoke();
        }

        // Unity injects the page's loader when the page finishes loading, so its ready signal or bootstrap error comes
        // after LoadCompleted. One that arrives while the page is still loading was sent by the previous page.
        internal void PageSignal(string reason)
        {
            if (!loading) Signal(reason);
        }

        // The view died before its reveal (Android's renderer went away): ends the gate and returns the open's failed
        // callback, for the caller to run after the teardown, as persistent mode does.
        internal Action Fail()
        {
            if (!Pending) return null;
            var callback = failed;
            Cancel();
            return callback;
        }

        internal void Tick()
        {
            if (Pending && clock() >= deadline) Signal("timeout");
        }

        // The page finished loading. Android's onPageFinished hides a WebView opened with startHidden once more, so a
        // reveal that came before it (the timeout or a load error) has to be repeated. Returns whether to show it again.
        internal bool LoadCompleted()
        {
            bool showAgain = revealedWhileLoading;
            loading = revealedWhileLoading = false;
            return showAgain;
        }

        // A page message that is (or batches) a request with this action; the bridge serializes "action":201.
        internal static bool IsRequest(string message, int action)
        {
            string token = "\"action\":" + action;
            int at = message?.IndexOf(token, StringComparison.Ordinal) ?? -1;
            int end = at + token.Length;
            return at >= 0 && (end >= message.Length || !char.IsDigit(message[end]));
        }
    }
}
