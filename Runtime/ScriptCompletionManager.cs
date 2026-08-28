using System;
using System.Collections.Generic;

namespace Balancy
{
    public static class ScriptCompletionManager
    {
        private static readonly Dictionary<string, Action<string, string>> _pendingCallbacks =
            new Dictionary<string, Action<string, string>>();

        internal static void Init()
        {
            _pendingCallbacks.Clear();
            LibraryMethods.General.balancySetScriptCompletionCallback(OnScriptCompleted);
        }

        internal static void CleanUp()
        {
            _pendingCallbacks.Clear();
            if (Controller.IsNativeInitialized)
                LibraryMethods.General.balancySetScriptCompletionCallback(null);
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
            if (string.IsNullOrEmpty(instanceId))
                return;

            if (_pendingCallbacks.TryGetValue(instanceId, out var callback))
            {
                _pendingCallbacks.Remove(instanceId);
                try
                {
                    callback?.Invoke(exitPort, outputsJson);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[Balancy] Script completion callback failed: {e}");
                }
            }
        }
    }
}
