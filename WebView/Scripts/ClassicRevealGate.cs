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

        internal ClassicRevealGate(Action<string> reveal, Func<double> clock)
        {
            this.reveal = reveal;
            this.clock = clock;
        }

        internal bool Pending => deadline >= 0;

        internal void Begin(double timeoutSeconds)
        {
            deadline = clock() + timeoutSeconds;
            loading = true;
            revealedWhileLoading = false;
        }

        internal void Cancel()
        {
            deadline = -1;
            loading = revealedWhileLoading = false;
        }

        internal void Signal(string reason)
        {
            if (!Pending) return;
            deadline = -1;
            if (loading) revealedWhileLoading = true;
            reveal(reason);
        }

        // Unity injects the page's loader when the page finishes loading, so its ready signal or bootstrap error comes
        // after LoadCompleted. One that arrives while the page is still loading was sent by the previous page.
        internal void PageSignal(string reason)
        {
            if (!loading) Signal(reason);
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
