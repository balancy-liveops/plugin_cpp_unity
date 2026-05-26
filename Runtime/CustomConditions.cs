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
            if (_registered)
                return;

            if (!_canPassHandle.IsAllocated)
                _canPassHandle = GCHandle.Alloc(_canPassCallback);
            if (!_subscribeHandle.IsAllocated)
                _subscribeHandle = GCHandle.Alloc(_subscribeCallback);
            if (!_unsubscribeHandle.IsAllocated)
                _unsubscribeHandle = GCHandle.Alloc(_unsubscribeCallback);

            LibraryMethods.CustomConditions.balancyCustomConditionRegisterHandler(_canPassCallback);
            LibraryMethods.CustomConditions.balancyCustomConditionRegisterSubscribeHandler(_subscribeCallback, _unsubscribeCallback);
            _registered = true;
        }

        /// <summary>
        /// Unregisters the handlers from the C++ core. Called automatically during SDK shutdown.
        /// </summary>
        internal static void Unregister()
        {
            if (!_registered)
                return;

            LibraryMethods.CustomConditions.balancyCustomConditionUnregisterHandler();
            _registered = false;
        }

        /// <summary>
        /// Call this when your custom condition's state has changed.
        /// This will trigger re-evaluation of the condition and notify all subscribers
        /// (offers, events, etc.) that depend on it.
        /// </summary>
        public static void ForceUpdate(string unnyId)
        {
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
