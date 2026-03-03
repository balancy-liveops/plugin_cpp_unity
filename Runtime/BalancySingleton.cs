using System;
using System.Collections.Generic;
using Balancy.Models;
using UnityEngine;

namespace Balancy.SmartObjects
{
    // Non-generic dispatcher so [MonoPInvokeCallback] works on Android/IL2CPP.
    // IL2CPP cannot generate a native thunk for a static method inside a generic class.
    internal static class BalancySingletonDispatcher
    {
        private static readonly Dictionary<string, Action<string>> _handlers =
            new Dictionary<string, Action<string>>();

        internal static void Register(string templateName, Action<string> handler)
        {
            _handlers[templateName] = handler;
        }

        internal static void Unregister(string templateName)
        {
            _handlers.Remove(templateName);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Singletons.SingletonChangedCallback))]
        internal static void OnSingletonChangedStatic(string templateName, string unnyId)
        {
            if (_handlers.TryGetValue(templateName, out var handler))
                handler(unnyId);
        }
    }

    /// <summary>
    /// Wrapper for singleton models. Supports both regular singletons and ConditionalTemplate-based singletons.
    /// ConditionalTemplate singletons automatically update based on user conditions and priority.
    /// </summary>
    /// <typeparam name="T">Singleton model type (must be a BaseModel)</typeparam>
    public class BalancySingleton<T> where T : BaseModel
    {
        /// <summary>
        /// Event fired when singleton value changes (primarily for ConditionalTemplate singletons)
        /// </summary>
        public event Action<T> OnChanged;

        private readonly int _callbackId;
        private readonly string _templateName;

        internal BalancySingleton()
        {
            _templateName = JsonBasedObject.GetModelClassName<T>();
            BalancySingletonDispatcher.Register(_templateName, OnSingletonChanged);
            _callbackId = LibraryMethods.Singletons.balancySubscribeSingletonChanged(
                _templateName,
                BalancySingletonDispatcher.OnSingletonChangedStatic
            );
        }

        ~BalancySingleton()
        {
            BalancySingletonDispatcher.Unregister(_templateName);
            LibraryMethods.Singletons.balancyUnsubscribeSingletonChanged(_templateName, _callbackId);
        }

        private void OnSingletonChanged(string unnyId)
        {
            if (!string.IsNullOrEmpty(unnyId))
            {
                var model = CMS.GetModelByUnnyId<T>(unnyId);
                OnChanged?.Invoke(model);
            }
        }

        /// <summary>
        /// Get the current singleton instance. For ConditionalTemplate singletons,
        /// this returns the variant with the highest priority whose condition passes.
        /// </summary>
        /// <returns>Current singleton instance or null if not available</returns>
        public T Get()
        {
            var ptr = LibraryMethods.Singletons.balancyGetSingleton(_templateName);
            if (ptr == IntPtr.Zero)
                return null;

            var unnyId = JsonBasedObject.GetStringFromIntPtr(ptr);
            if (string.IsNullOrEmpty(unnyId))
                return null;

            return CMS.GetModelByUnnyId<T>(unnyId);
        }
    }
}
