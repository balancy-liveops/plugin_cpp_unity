using System;
using System.Collections.Generic;

namespace Balancy
{
    public static class ScriptCompletionManager
    {
        private static readonly Dictionary<string, Action<string, string>> _pendingCallbacks =
            new Dictionary<string, Action<string, string>>();

        // Rooted delegate instance — prevent GC from collecting the P/Invoke wrapper
        // while native code still holds the function pointer.
        private static readonly LibraryMethods.ScriptCompletionCallback s_ScriptCompletionCb = OnScriptCompleted;

        internal static void Init()
        {
            LibraryMethods.General.balancySetScriptCompletionCallback(s_ScriptCompletionCb);
        }

        /// <summary>
        /// Register a completion callback for a script instance. Called by ScriptRef.Launch()
        /// and API.VisualScripting.RunScriptById() when an onComplete handler is provided.
        /// </summary>
        public static void Register(string instanceId, Action<string, string> onComplete)
        {
            if (!string.IsNullOrEmpty(instanceId) && onComplete != null)
                _pendingCallbacks[instanceId] = onComplete;
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.ScriptCompletionCallback))]
        private static void OnScriptCompleted(string instanceId, string exitPort, string outputsJson)
        {
            if (_pendingCallbacks.TryGetValue(instanceId, out var callback))
            {
                _pendingCallbacks.Remove(instanceId);
                callback?.Invoke(exitPort, outputsJson);
            }
        }
    }
}
