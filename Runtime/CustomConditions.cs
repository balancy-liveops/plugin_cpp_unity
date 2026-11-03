using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Balancy
{
    /// <summary>
    /// Manages custom condition evaluation. Automatically routes canPass, subscribe, and unsubscribe calls
    /// from C++ to the corresponding C# Custom condition instance methods.
    /// </summary>
    public static class CustomConditions
    {
        private static readonly LibraryMethods.CustomConditions.CustomConditionCanPassCallback _canPassCallback = OnCanPassCallback;
        private static readonly LibraryMethods.CustomConditions.CustomConditionSubscribeCallback _subscribeCallback = OnSubscribeCallback;
        private static readonly LibraryMethods.CustomConditions.CustomConditionSubscribeCallback _unsubscribeCallback = OnUnsubscribeCallback;
        private static GCHandle _canPassHandle;
        private static GCHandle _subscribeHandle;
        private static GCHandle _unsubscribeHandle;
        private static bool _registered;
        private static bool _unregistering;
        private static int _subscriptionGeneration;
        // Keep the actual subscribed instance: CMS may be unavailable or may
        // resolve the same ID to a replacement when native teardown arrives.
        private static readonly Dictionary<string, Models.SmartObjects.Conditions.Custom> Subscriptions =
            new Dictionary<string, Models.SmartObjects.Conditions.Custom>();

        /// <summary>
        /// Registers the handlers with the C++ core. Called automatically during SDK initialization.
        /// </summary>
        internal static void Register()
        {
            RegisterCore(
                () => LibraryMethods.CustomConditions.balancyCustomConditionRegisterHandler(_canPassCallback),
                () => LibraryMethods.CustomConditions.balancyCustomConditionRegisterSubscribeHandler(_subscribeCallback, _unsubscribeCallback),
                LibraryMethods.CustomConditions.balancyCustomConditionUnregisterHandler);
        }

        private static void RegisterCore(Action registerCanPass, Action registerSubscriptions, Action rollback)
        {
            if (_registered || _unregistering)
                return;

            if (!_canPassHandle.IsAllocated)
                _canPassHandle = GCHandle.Alloc(_canPassCallback);
            if (!_subscribeHandle.IsAllocated)
                _subscribeHandle = GCHandle.Alloc(_subscribeCallback);
            if (!_unsubscribeHandle.IsAllocated)
                _unsubscribeHandle = GCHandle.Alloc(_unsubscribeCallback);

            try
            {
                registerCanPass();
                registerSubscriptions();
                _registered = true;
            }
            catch
            {
                // Registration is two native calls. If the second one fails, the
                // first handler must not survive and point back into a failed init.
                try { rollback(); }
                catch (Exception rollbackException) { UnityEngine.Debug.LogException(rollbackException); }
                _registered = false;
                UnregisterCore(() => { });
                throw;
            }
        }

        /// <summary>
        /// Unregisters the handlers from the C++ core. Called automatically during SDK shutdown.
        /// </summary>
        internal static void Unregister()
        {
            UnregisterCore(LibraryMethods.CustomConditions.balancyCustomConditionUnregisterHandler);
        }

        private static void UnregisterCore(Action unregister)
        {
            if (_unregistering)
                return;

            _unregistering = true;
            ++_subscriptionGeneration;
            try
            {
                try
                {
                    if (_registered)
                        unregister();
                }
                finally
                {
                    _registered = false;
                    // Remove ownership before invoking game code: Unsubscribe
                    // may reenter Stop or deliver another native unsubscribe.
                    var subscriptions = new List<KeyValuePair<string, Models.SmartObjects.Conditions.Custom>>(Subscriptions);
                    Subscriptions.Clear();
                    foreach (var subscription in subscriptions)
                        UnsubscribeCondition(subscription.Key, subscription.Value);
                }
            }
            finally
            {
                _unregistering = false;
            }
        }

        private static void UnsubscribeCondition(string unnyId, Models.SmartObjects.Conditions.Custom condition)
        {
            try { condition.Unsubscribe(); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Balancy.CustomConditions] Exception in Unsubscribe for {unnyId}: {ex}");
            }
        }

        /// <summary>
        /// Call this when your custom condition's state has changed.
        /// This will trigger re-evaluation of the condition and notify all subscribers
        /// (offers, events, etc.) that depend on it.
        /// </summary>
        public static void ForceUpdate(string unnyId)
        {
            if (string.IsNullOrEmpty(unnyId))
            {
                UnityEngine.Debug.LogError("[Balancy.CustomConditions] ForceUpdate requires a non-empty condition ID.");
                return;
            }
            if (!Controller.IsNativeInitialized)
            {
                UnityEngine.Debug.LogError("[Balancy.CustomConditions] ForceUpdate ignored because the SDK is not initialized.");
                return;
            }
            LibraryMethods.CustomConditions.balancyCustomConditionForceUpdate(unnyId);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.CustomConditions.CustomConditionCanPassCallback))]
        private static bool OnCanPassCallback(string unnyId)
        {
            try
            {
                var condition = CMS.GetModelByUnnyId<Models.SmartObjects.Conditions.Custom>(unnyId);
                if (condition != null)
                    return condition.CanPassCustom();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Balancy.CustomConditions] Exception in CanPassCustom for {unnyId}: {ex}");
            }
            return false;
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.CustomConditions.CustomConditionSubscribeCallback))]
        private static void OnSubscribeCallback(string unnyId)
        {
            if (_unregistering)
                return;

            try
            {
                var generation = _subscriptionGeneration;
                var condition = CMS.GetModelByUnnyId<Models.SmartObjects.Conditions.Custom>(unnyId);
                if (condition == null)
                    return;

                if (Subscriptions.TryGetValue(unnyId, out var previous))
                {
                    if (ReferenceEquals(previous, condition))
                        return;
                    Subscriptions.Remove(unnyId);
                    UnsubscribeCondition(unnyId, previous);
                    if (generation != _subscriptionGeneration || _unregistering || Subscriptions.ContainsKey(unnyId))
                        return;
                }

                // Publish before calling game code so a reentrant Stop also
                // closes a subscription created inside Subscribe itself.
                Subscriptions.Add(unnyId, condition);
                try { condition.Subscribe(); }
                catch
                {
                    if (Subscriptions.TryGetValue(unnyId, out var current) && ReferenceEquals(current, condition))
                    {
                        Subscriptions.Remove(unnyId);
                        UnsubscribeCondition(unnyId, condition);
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Balancy.CustomConditions] Exception in Subscribe for {unnyId}: {ex}");
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.CustomConditions.CustomConditionSubscribeCallback))]
        private static void OnUnsubscribeCallback(string unnyId)
        {
            if (unnyId != null && Subscriptions.TryGetValue(unnyId, out var condition))
            {
                Subscriptions.Remove(unnyId);
                UnsubscribeCondition(unnyId, condition);
            }
        }
    }
}
