using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Models;

namespace Balancy.Data
{
    public class BaseData : JsonBasedObject
    {
        protected void SetIntValue(string paramName, int inValue)
        {
            LibraryMethods.Data.balancySetIntParam(_pointer, paramName, inValue);
        }
        
        protected void SetLongValue(string paramName, long inValue)
        {
            LibraryMethods.Data.balancySetLongParam(_pointer, paramName, inValue);
        }
        
        protected void SetStringValue(string paramName, string inValue)
        {
            LibraryMethods.Data.balancySetStringParam(_pointer, paramName, inValue);
        }
        
        protected void SetFloatValue(string paramName, float inValue)
        {
            LibraryMethods.Data.balancySetFloatParam(_pointer, paramName, inValue);
        }
        
        protected void SetBoolValue(string paramName, bool inValue)
        {
            LibraryMethods.Data.balancySetBoolParam(_pointer, paramName, inValue);
        }
        
        protected void InitAndSubscribeForParamChange(string paramName, Action callback)
        {
            callback();
            SubscribeForParamChange(paramName, callback);
        }
        
        internal void SubscribeForParamChange(string paramName, Action callback)
        {
            if (TempCopy)
                return;
            
            // var callbackDelegate = new LibraryMethods.Data.ParamChangedCallback(callback);
            // var callbackHandle = GCHandle.Alloc(callbackDelegate);
            //
            // IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(callbackDelegate);
            // var callbackId = LibraryMethods.Data.balancySubscribeBaseDataParamChange(_pointer, paramName, callbackPtr);
            _callbacks.Add(new CallbacksHolder
            {
                ParamName = paramName,
                Callback = callback,
                // Handle = callbackHandle,
                // Id = callbackId
            });
            
            Profiles.AddDataSubscription(_pointer, paramName, callback);
        }
        
        ~BaseData()
        {
            //I commented it because it was crashing after multiple play/stop. usually BaseData is connected to the C++ class, they should be destroyed together anyway.
            //But RESET doesn't work without it
            CleanUp(false);
        }

        internal override void CleanUp(bool parentWasDestroyed)
        {
            base.CleanUp(parentWasDestroyed);
            foreach (var callback in _callbacks)
            {
                // if (callback.Handle.IsAllocated)
                //     callback.Handle.Free();
                // if (!parentWasDestroyed)
                //     LibraryMethods.Data.balancyUnsubscribeBaseDataParamChange(_pointer, callback.ParamName, callback.Id);
                Profiles.RemoveDataSubscription(_pointer, callback.ParamName, callback.Callback);
            }
            _callbacks.Clear();

            foreach (var child in _children)
                child.CleanUp(parentWasDestroyed);
            _children.Clear();
        }
        
        protected T GetBaseDataParam<T>(string paramName) where T: BaseData, new()
        {
            var className = GetDataClassName<T>();
            var ptr = GetBaseDataParamPrivate(paramName, className);
            var data = CreateObject<T>(ptr, TempCopy);
            _children.Add(data);
            return data;
        }
        
        protected SmartList<T> GetListBaseDataParam<T>(string paramName) where T: BaseData, new()
        {
            var className = GetDataClassName<T>();
            var ptr = GetListBaseDataParamPrivate(paramName, className);
            var data = CreateObject<SmartList<T>>(ptr, TempCopy);
            data.SubscribeForUpdates(paramName, this);
            _children.Add(data);
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
            // public GCHandle Handle;
            // public int Id;
        }

        private readonly List<CallbacksHolder> _callbacks = new List<CallbacksHolder>();
        private readonly List<BaseData> _children = new List<BaseData>();
        
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
