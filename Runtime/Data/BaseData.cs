using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Models;

namespace Balancy.Data
{
    public class BaseData : JsonBasedObject
    {
        public void SetIntValue(string paramName, int inValue)
        {
            LibraryMethods.Data.balancySetIntParam(_pointer, paramName, inValue);
        }
        
        public void SetLongValue(string paramName, long inValue)
        {
            LibraryMethods.Data.balancySetLongParam(_pointer, paramName, inValue);
        }
        
        public void SetStringValue(string paramName, string inValue)
        {
            LibraryMethods.Data.balancySetStringParam(_pointer, paramName, inValue);
        }
        
        public void SetFloatValue(string paramName, float inValue)
        {
            LibraryMethods.Data.balancySetFloatParam(_pointer, paramName, inValue);
        }
        
        public void SetBoolValue(string paramName, bool inValue)
        {
            LibraryMethods.Data.balancySetBoolParam(_pointer, paramName, inValue);
        }
        
        protected void InitAndSubscribeForParamChange(string paramName, Action callback)
        {
            callback();
            SubscribeForParamChange(paramName, callback);
        }

        /// <summary>
        /// Subscribe to changes of a specific parameter. The callback fires whenever the parameter is modified.
        /// </summary>
        public void SubscribeForParamChange(string paramName, Action callback)
        {
            // An unbound wrapper has nothing to subscribe to, and registering under
            // IntPtr.Zero would collide with every other unbound object. Binding the
            // wrapper re-runs InitData(), which subscribes again with the real
            // pointer, so nothing is lost.
            if (TempCopy || _pointer == IntPtr.Zero)
                return;

            _callbacks.Add(new CallbacksHolder
            {
                ParamName = paramName,
                Callback = callback,
            });

            Profiles.AddDataSubscription(_pointer, paramName, callback);
        }

        /// <summary>
        /// Unsubscribe from changes of a specific parameter.
        /// </summary>
        public void UnsubscribeFromParamChange(string paramName, Action callback)
        {
            _callbacks.RemoveAll(c => c.ParamName == paramName && c.Callback == callback);
            Profiles.RemoveDataSubscription(_pointer, paramName, callback);
        }

        /// <summary>
        /// Subscribe to changes of any parameter on this object and all its children recursively.
        /// The callback receives the full path of the changed parameter relative to this object
        /// (e.g. "generalInfo.level" when subscribed on the profile).
        /// </summary>
        public void SubscribeForChanges(Action<string> callback)
        {
            if (TempCopy || _pointer == IntPtr.Zero)
                return;

            _wildcardCallbacks.Add(callback);
            Profiles.AddWildcardSubscription(_pointer, callback);

            foreach (var entry in _children)
            {
                var childName = entry.Name;
                Action<string> wrapped = paramName => callback(childName + "." + paramName);
                entry.WrappedCallbacks[callback] = wrapped;
                entry.Data.SubscribeForChanges(wrapped);
            }
        }

        /// <summary>
        /// Unsubscribe from changes of any parameter on this object and all its children recursively.
        /// </summary>
        public void UnsubscribeFromChanges(Action<string> callback)
        {
            _wildcardCallbacks.Remove(callback);
            Profiles.RemoveWildcardSubscription(_pointer, callback);

            foreach (var entry in _children)
            {
                if (entry.WrappedCallbacks.TryGetValue(callback, out var wrapped))
                {
                    entry.Data.UnsubscribeFromChanges(wrapped);
                    entry.WrappedCallbacks.Remove(callback);
                }
            }
        }

        ~BaseData()
        {
            //I commented it because it was crashing after multiple play/stop. usually BaseData is connected to the C++ class, they should be destroyed together anyway.
            //But RESET doesn't work without it
            CleanUp(false);
        }

        internal override void CleanUp(bool parentWasDestroyed)
        {
            foreach (var callback in _callbacks)
            {
                Profiles.RemoveDataSubscription(_pointer, callback.ParamName, callback.Callback);
            }
            _callbacks.Clear();

            foreach (var callback in _wildcardCallbacks)
            {
                Profiles.RemoveWildcardSubscription(_pointer, callback);
            }
            _wildcardCallbacks.Clear();

            foreach (var entry in _children)
                entry.Data.CleanUp(parentWasDestroyed);
            _children.Clear();
            base.CleanUp(parentWasDestroyed);
        }
        
        protected T GetBaseDataParam<T>(string paramName) where T: BaseData, new()
        {
            var className = GetDataClassName<T>();

            // The native child can be destroyed and recreated while this object stays
            // at the same address: InventorySlot.deleteItem() + setItem() does exactly
            // that on every RemoveItems/AddItems round trip. A wrapper resolved once in
            // InitData() would keep pointing at the freed object — the reported "the
            // slot has no item". So the wrapper is created unconditionally (it may
            // start out unbound, e.g. an inventory slot with no item — check IsValid)
            // and re-bound whenever the parameter changes, which is the same contract
            // GetListBaseDataParam already gives lists.
            var data = new T();
            if (TempCopy)
                data.MarkAsTempObject();

            var entry = new ChildEntry { Name = paramName, Data = data };
            _children.Add(entry);

            RebindChild(entry, className);
            SubscribeForParamChange(paramName, () => RebindChild(entry, className));
            return data;
        }

        private void RebindChild(ChildEntry entry, string className)
        {
            var ptr = _pointer == IntPtr.Zero
                ? IntPtr.Zero
                : GetBaseDataParamPrivate(entry.Name, className);

            if (entry.Data.Equals(ptr))
                return;

            if (ptr == IntPtr.Zero)
            {
                // Nothing to bind to any more. Unbind so reads return defaults instead
                // of reaching into a destroyed native object; do NOT run InitData() on
                // a null pointer.
                entry.Data.CleanUp(true);
                return;
            }

            entry.Data.RefreshData(ptr);

            // RefreshData() clears the child's subscriptions, including the wrappers a
            // parent-level SubscribeForChanges() installed on it. Re-arm them so a
            // recursive subscription keeps working across a rebind.
            foreach (var wrapped in entry.WrappedCallbacks.Values)
                entry.Data.SubscribeForChanges(wrapped);
        }

        protected SmartList<T> GetListBaseDataParam<T>(string paramName) where T: BaseData, new()
        {
            var className = GetDataClassName<T>();
            var ptr = GetListBaseDataParamPrivate(paramName, className);
            var data = CreateObject<SmartList<T>>(ptr, TempCopy);
            data.SubscribeForUpdates(paramName, this);
            _children.Add(new ChildEntry { Name = paramName, Data = data });
            return data;
        }

        protected SmartListSimple<T> GetListSimpleParam<T>(string paramName)
        {
            IntPtr ptr;
            if (typeof(T) == typeof(int))
            {
                ptr = LibraryMethods.Data.balancyGetListSimpleIntParam(_pointer, paramName);
            }
            else if (typeof(T) == typeof(float))
            {
                ptr = LibraryMethods.Data.balancyGetListSimpleFloatParam(_pointer, paramName);
            }
            else if (typeof(T) == typeof(string))
            {
                ptr = LibraryMethods.Data.balancyGetListSimpleStringParam(_pointer, paramName);
            }
            else if (typeof(T) == typeof(long))
            {
                ptr = LibraryMethods.Data.balancyGetListSimpleLongParam(_pointer, paramName);
            }
            else if (typeof(T) == typeof(bool))
            {
                ptr = LibraryMethods.Data.balancyGetListSimpleBoolParam(_pointer, paramName);
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }

            var data = CreateObject<SmartListSimple<T>>(ptr, TempCopy);
            data.SubscribeForUpdates(paramName, this);
            _children.Add(new ChildEntry { Name = paramName, Data = data });
            return data;
        }

        private IntPtr GetBaseDataParamPrivate(string paramName, string fileName)
        {
            return LibraryMethods.Data.balancyGetBaseDataParam(_pointer, paramName, fileName);
        }
        
        private IntPtr GetListBaseDataParamPrivate(string paramName, string fileName)
        {
            return LibraryMethods.Data.balancyGetListBaseDataParam(_pointer, paramName, fileName);
        }

        private struct CallbacksHolder
        {
            public string ParamName;
            public Action Callback;
        }

        private class ChildEntry
        {
            public string Name;
            public BaseData Data;
            public readonly Dictionary<Action<string>, Action<string>> WrappedCallbacks = new Dictionary<Action<string>, Action<string>>();
        }

        private readonly List<CallbacksHolder> _callbacks = new List<CallbacksHolder>();
        private readonly List<Action<string>> _wildcardCallbacks = new List<Action<string>>();
        private readonly List<ChildEntry> _children = new List<ChildEntry>();
        
        protected static T FindElementInList<T>(SmartList<T> list, IntPtr ptr) where T: BaseData, new()
        {
            foreach (var eEvent in list)
            {
                if (eEvent.Equals(ptr))
                    return eEvent;
            }

            return null;
        }

    }
}
