using System;
using System.Collections.Generic;
using Balancy.Models;
using UnityEngine;

namespace Balancy.SmartObjects
{
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
        private static readonly Dictionary<string, object> _instances = new Dictionary<string, object>();

        internal BalancySingleton()
        {
            _templateName = JsonBasedObject.GetModelClassName<T>();
            _callbackId = LibraryMethods.Singletons.balancySubscribeSingletonChanged(
                _templateName,
                OnSingletonChangedStatic
            );

            _instances[_templateName] = this;
        }

        ~BalancySingleton()
        {
            LibraryMethods.Singletons.balancyUnsubscribeSingletonChanged(_templateName, _callbackId);
            _instances.Remove(_templateName);
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Singletons.SingletonChangedCallback))]
        private static void OnSingletonChangedStatic(string templateName, IntPtr modelPtr)
        {
            if (_instances.TryGetValue(templateName, out var instance))
            {
                var singleton = instance as BalancySingleton<T>;
                if (singleton != null && modelPtr != IntPtr.Zero)
                {
                    // Get the unnyId from the model pointer
                    var unnyId = JsonBasedObject.GetUnnyId(modelPtr);
                    if (!string.IsNullOrEmpty(unnyId))
                    {
                        var model = CMS.GetModelByUnnyId<T>(unnyId);
                        singleton.OnChanged?.Invoke(model);
                    }
                }
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

            var unnyId = JsonBasedObject.GetUnnyId(ptr);
            if (string.IsNullOrEmpty(unnyId))
                return null;

            return CMS.GetModelByUnnyId<T>(unnyId);
        }
    }
}
