using System;
using UnityEngine;

namespace Balancy.WebView
{
    internal static class ClassicReadyRequest
    {
        // Keep batch items non-recursive: JsonUtility traverses the serialized type graph.
        [Serializable] private class Request { public string type, id, viewId; public int action; }
        [Serializable] private sealed class Envelope : Request { public Request[] requests; }

        internal static bool IsRequest(string message, int action)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(message);
                if (envelope == null) return false;
                if (envelope.type == "request")
                    return !string.IsNullOrEmpty(envelope.id) && string.IsNullOrEmpty(envelope.viewId) && envelope.action == action;
                if (envelope.type != "batch" || envelope.requests == null || envelope.requests.Length == 0)
                    return false;
                bool matches = false;
                foreach (var request in envelope.requests)
                {
                    if (request == null || string.IsNullOrEmpty(request.id) || !string.IsNullOrEmpty(request.viewId) ||
                        (!string.IsNullOrEmpty(request.type) && request.type != "request")) return false;
                    matches |= request.action == action;
                }
                return matches;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
