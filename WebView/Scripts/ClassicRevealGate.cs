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

        internal ClassicRevealGate(Action<string> reveal, Func<double> clock)
        {
            this.reveal = reveal;
            this.clock = clock;
        }

        internal bool Pending => deadline >= 0;

        internal void Begin(double timeoutSeconds) => deadline = clock() + timeoutSeconds;

        internal void Cancel() => deadline = -1;

        internal void Signal(string reason)
        {
            if (!Pending) return;
            deadline = -1;
            reveal(reason);
        }

        internal void Tick()
        {
            if (Pending && clock() >= deadline) Signal("timeout");
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
