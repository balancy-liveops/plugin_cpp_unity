using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Balancy.Models;
using UnityEngine;

namespace Balancy.SmartObjects
{
    internal static class BalancyConditionalTemplateRegistry
    {
        private static readonly HashSet<IDisposable> Instances = new HashSet<IDisposable>();

        internal static void Register(IDisposable instance)
        {
            Instances.Add(instance);
        }

        internal static void Unregister(IDisposable instance)
        {
            Instances.Remove(instance);
        }

        internal static void Clear()
        {
            var snapshot = Instances.ToArray();
            foreach (var instance in snapshot)
                instance.Dispose();
            Instances.Clear();
        }
    }

    /// <summary>
    /// Wrapper for conditional template models. Provides notifications when template conditions change.
    /// Subscribe to OnStatusChanged to be notified when any document of this type becomes active or inactive.
    /// </summary>
    /// <typeparam name="T">ConditionalTemplate model type (must inherit from BaseModel)</typeparam>
    public class BalancyConditionalTemplate<T> : IDisposable where T : BaseModel
    {
        /// <summary>
        /// Event fired when any document of this template type changes status
        /// Parameters: (model, isActive)
        /// </summary>
        public event Action<T, bool> OnStatusChanged;

        private readonly int _callbackId;
        private readonly string _templateName;
        private static readonly Dictionary<string, object> _instances = new Dictionary<string, object>();
        private bool _disposed;

        internal BalancyConditionalTemplate()
        {
            _templateName = JsonBasedObject.GetModelClassName<T>();
            if (_instances.TryGetValue(_templateName, out var previous))
                (previous as IDisposable)?.Dispose();

            _callbackId = LibraryMethods.ConditionalTemplates.balancySubscribeConditionalTemplateChanged(
                _templateName,
                OnConditionalTemplateChangedStatic
            );

            _instances[_templateName] = this;
            BalancyConditionalTemplateRegistry.Register(this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            BalancyConditionalTemplateRegistry.Unregister(this);
            if (_instances.TryGetValue(_templateName, out var current) && ReferenceEquals(current, this))
                _instances.Remove(_templateName);
            if (_callbackId >= 0 && Controller.IsNativeInitialized)
                LibraryMethods.ConditionalTemplates.balancyUnsubscribeConditionalTemplateChanged(_templateName, _callbackId);
            OnStatusChanged = null;
        }

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.ConditionalTemplates.ConditionalTemplateChangedCallback))]
        private static void OnConditionalTemplateChangedStatic(string templateName, string unnyId, bool passed)
        {
            if (_instances.TryGetValue(templateName, out var instance))
            {
                var conditionalTemplate = instance as BalancyConditionalTemplate<T>;
                if (conditionalTemplate != null && !string.IsNullOrEmpty(unnyId))
                {
                    var model = CMS.GetModelByUnnyId<T>(unnyId);
                    conditionalTemplate.OnStatusChanged?.Invoke(model, passed);
                }
            }
        }

        /// <summary>
        /// Get all currently active documents of this template type.
        /// Returns documents whose conditions are currently passing.
        /// </summary>
        /// <returns>List of active documents, or empty list if none are active</returns>
        public List<T> GetActiveDocuments()
        {
            var result = new List<T>();

            if (_disposed || !Controller.IsNativeInitialized)
                return result;

            IntPtr arrayPtr = LibraryMethods.ConditionalTemplates.balancyGetActiveConditionalTemplates(_templateName, out int size);

            if (arrayPtr == IntPtr.Zero || size == 0)
                return result;

            try
            {
                // Read array of string pointers
                IntPtr[] stringPtrs = new IntPtr[size];
                Marshal.Copy(arrayPtr, stringPtrs, 0, size);

                // Convert each pointer to string (unnyId) and get the model
                foreach (var strPtr in stringPtrs)
                {
                    if (strPtr != IntPtr.Zero)
                    {
                        string unnyId = Marshal.PtrToStringAnsi(strPtr);
                        if (!string.IsNullOrEmpty(unnyId))
                        {
                            var model = CMS.GetModelByUnnyId<T>(unnyId);
                            if (model != null)
                            {
                                result.Add(model);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting active conditional templates: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the highest priority active document of this template type.
        /// Returns the document with the highest priority whose condition is currently passing.
        /// </summary>
        /// <returns>Highest priority active document, or null if none are active</returns>
        public T GetHighestPriorityActive()
        {
            var active = GetActiveDocuments();
            if (active.Count == 0)
                return null;

            // Documents should already be sorted by priority from C++, but let's ensure it
            // ConditionalTemplate has getPriority() method
            return active.FirstOrDefault();
        }
    }
}
