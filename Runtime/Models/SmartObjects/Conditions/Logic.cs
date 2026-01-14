using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Balancy.Models.SmartObjects.Conditions
{
    public class Logic : BaseModel
    {
        // Static callback registry to handle callbacks from C++
        private static readonly Dictionary<string, Action<bool>> s_callbackRegistry = new Dictionary<string, Action<bool>>();
        private static readonly LibraryMethods.Conditions.ConditionStatusChangedCallback s_staticCallback = OnConditionStatusChanged;

        // Keep a strong reference to prevent GC
        private static readonly GCHandle s_callbackHandle = GCHandle.Alloc(s_staticCallback);

        private bool _isSubscribed = false;

        /// <summary>
        /// Evaluates whether this condition currently passes for the active user.
        /// </summary>
        /// <returns>True if condition passes, false otherwise</returns>
        public bool CanPass()
        {
            if (_pointer == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[Balancy.Conditions] CanPass called on condition with null pointer: {UnnyId}");
                return false;
            }

            return LibraryMethods.Conditions.balancyConditionCanPass(_pointer);
        }

        /// <summary>
        /// Subscribes to condition status changes. Callback will be invoked on Unity main thread
        /// whenever the condition's pass/fail status changes.
        /// Note: Only one subscription per condition is supported. Calling this multiple times
        /// will replace the previous callback.
        /// </summary>
        /// <param name="callback">Action to invoke with new status (true = passed, false = failed)</param>
        public void SubscribeForStatusChange(Action<bool> callback)
        {
            if (_pointer == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[Balancy.Conditions] SubscribeForStatusChange called on condition with null pointer: {UnnyId}");
                return;
            }

            if (callback == null)
            {
                UnityEngine.Debug.LogError($"[Balancy.Conditions] SubscribeForStatusChange called with null callback: {UnnyId}");
                return;
            }

            // Unsubscribe first if already subscribed
            if (_isSubscribed)
            {
                UnsubscribeFromStatusChange();
            }

            // Register callback
            s_callbackRegistry[UnnyId] = callback;
            _isSubscribed = true;

            // Subscribe in C++
            LibraryMethods.Conditions.balancyConditionSubscribe(_pointer, s_staticCallback);
        }

        /// <summary>
        /// Unsubscribes from condition status changes.
        /// </summary>
        public void UnsubscribeFromStatusChange()
        {
            if (_pointer == IntPtr.Zero)
            {
                UnityEngine.Debug.LogWarning($"[Balancy.Conditions] UnsubscribeFromStatusChange called on condition with null pointer: {UnnyId}");
                return;
            }

            if (!_isSubscribed)
            {
                return;
            }

            // Remove callback from registry
            s_callbackRegistry.Remove(UnnyId);
            _isSubscribed = false;

            // Unsubscribe in C++
            LibraryMethods.Conditions.balancyConditionUnsubscribe(_pointer);
        }

        /// <summary>
        /// Gets the number of seconds remaining before this condition deactivates.
        /// </summary>
        /// <returns>Seconds until deactivation, -1 if not currently active, int.MaxValue if indefinite</returns>
        public int GetSecondsLeftBeforeDeactivation()
        {
            if (_pointer == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[Balancy.Conditions] GetSecondsLeftBeforeDeactivation called on condition with null pointer: {UnnyId}");
                return -1;
            }

            return LibraryMethods.Conditions.balancyConditionGetSecondsLeftBeforeDeactivation(_pointer);
        }

        /// <summary>
        /// Gets the number of seconds until this condition will activate.
        /// </summary>
        /// <param name="ignoreTriggers">If true, ignores trigger-based conditions</param>
        /// <returns>Seconds until activation, -1 if already active, int.MaxValue if never/trigger-based</returns>
        public int GetSecondsBeforeActivation(bool ignoreTriggers = false)
        {
            if (_pointer == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[Balancy.Conditions] GetSecondsBeforeActivation called on condition with null pointer: {UnnyId}");
                return -1;
            }

            return LibraryMethods.Conditions.balancyConditionGetSecondsBeforeActivation(_pointer, ignoreTriggers);
        }

        // Static callback invoked from C++
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Conditions.ConditionStatusChangedCallback))]
        private static void OnConditionStatusChanged(string unnyId, bool passed)
        {
            if (s_callbackRegistry.TryGetValue(unnyId, out var callback))
            {
                try
                {
                    callback?.Invoke(passed);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Balancy.Conditions] Exception in condition callback for {unnyId}: {ex}");
                }
            }
        }

        // Cleanup when condition is destroyed
        ~Logic()
        {
            if (_isSubscribed)
            {
                UnsubscribeFromStatusChange();
            }
        }
    }
}