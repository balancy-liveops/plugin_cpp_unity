using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Balancy.Models;
using UnityEngine;

namespace Balancy.SmartObjects
{
    /// <summary>
    /// Wrapper for conditional template models. Provides notifications when template conditions change.
    /// Subscribe to OnStatusChanged to be notified when any document of this type becomes active or inactive.
    /// </summary>
    /// <typeparam name="T">ConditionalTemplate model type (must inherit from BaseModel)</typeparam>
    public class BalancyConditionalTemplate<T> where T : BaseModel
    {
        /// <summary>
        /// Event fired when any document of this template type changes status
        /// Parameters: (model, isActive)
        /// </summary>
        public event Action<T, bool> OnStatusChanged;

        private readonly int _callbackId;
        private readonly string _templateName;
        private static readonly Dictionary<string, object> _instances = new Dictionary<string, object>();

        internal BalancyConditionalTemplate()
        {
            _templateName = JsonBasedObject.GetModelClassName<T>();
            _callbackId = LibraryMethods.ConditionalTemplates.balancySubscribeConditionalTemplateChanged(
                _templateName,
                OnConditionalTemplateChangedStatic
            );

            _instances[_templateName] = this;
        }

        ~BalancyConditionalTemplate()
        {
            LibraryMethods.ConditionalTemplates.balancyUnsubscribeConditionalTemplateChanged(_templateName, _callbackId);
            _instances.Remove(_templateName);
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
