using System;
using System.Collections.Generic;
using System.Threading;

namespace Balancy
{
    public static class ScriptCompletionManager
    {
        private static readonly Dictionary<string, Action<string, string>> _pendingCallbacks =
            new Dictionary<string, Action<string, string>>();
        private static readonly object _lock = new object();
        private static bool _isInitialized;
        private static int _generation;
        private static int _mainThreadId;

        internal static void Init()
        {
            var generation = ++_generation;
            lock (_lock)
                _pendingCallbacks.Clear();
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _isInitialized = true;
            try
            {
                LibraryMethods.General.balancySetScriptCompletionCallback(OnScriptCompleted);
            }
            catch
            {
                if (_generation == generation)
                    _isInitialized = false;
                throw;
            }
        }

        internal static void CleanUp()
        {
            ++_generation;
            _isInitialized = false;
            lock (_lock)
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
            {
                lock (_lock)
                    _pendingCallbacks[instanceId] = onComplete;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.ScriptCompletionCallback))]
        private static void OnScriptCompleted(string instanceId, string exitPort, string outputsJson)
        {
            if (string.IsNullOrEmpty(instanceId) || !_isInitialized)
                return;

            var generation = _generation;
            try
            {
                if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
                    DeliverCompletion(instanceId, exitPort, outputsJson, generation);
                else
                {
                    UnityMainThreadDispatcher.EnqueueFromAnyThread(() =>
                        DeliverCompletion(instanceId, exitPort, outputsJson, generation));
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static void DeliverCompletion(string instanceId, string exitPort, string outputsJson, int generation)
        {
            if (!_isInitialized || generation != _generation)
                return;

            Action<string, string> callback;
            lock (_lock)
            {
                if (!_pendingCallbacks.TryGetValue(instanceId, out callback))
                    return;
                _pendingCallbacks.Remove(instanceId);
            }
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
