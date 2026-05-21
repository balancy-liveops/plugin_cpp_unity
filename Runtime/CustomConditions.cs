using System;
using System.Runtime.InteropServices;

namespace Balancy
{
    /// <summary>
    /// Manages custom condition evaluation. Automatically routes canPass calls
    /// from C++ to the corresponding C# Custom condition instance's CanPassCustom() method.
    /// </summary>
    public static class CustomConditions
    {
        private static readonly LibraryMethods.CustomConditions.CustomConditionCanPassCallback _staticCallback = OnCanPassCallback;
        private static GCHandle _callbackHandle;
        private static bool _registered;

        /// <summary>
        /// Registers the handler with the C++ core. Called automatically during SDK initialization.
        /// </summary>
        internal static void Register()
        {
            if (_registered)
                return;

            if (!_callbackHandle.IsAllocated)
                _callbackHandle = GCHandle.Alloc(_staticCallback);

            LibraryMethods.CustomConditions.balancyCustomConditionRegisterHandler(_staticCallback);
            _registered = true;
        }

        /// <summary>
        /// Unregisters the handler from the C++ core. Called automatically during SDK shutdown.
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
    }
}
