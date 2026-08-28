using System;
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
            if (_registered)
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
            if (!_registered)
                return;

            try { unregister(); }
            finally
            {
                // A failed native cleanup must never poison the next init by
                // making Register believe the old native session is still wired.
                _registered = false;
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
            try
            {
                var condition = CMS.GetModelByUnnyId<Models.SmartObjects.Conditions.Custom>(unnyId);
                condition?.Subscribe();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Balancy.CustomConditions] Exception in Subscribe for {unnyId}: {ex}");
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.CustomConditions.CustomConditionSubscribeCallback))]
        private static void OnUnsubscribeCallback(string unnyId)
        {
            try
            {
                var condition = CMS.GetModelByUnnyId<Models.SmartObjects.Conditions.Custom>(unnyId);
                condition?.Unsubscribe();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Balancy.CustomConditions] Exception in Unsubscribe for {unnyId}: {ex}");
            }
        }
    }
}
