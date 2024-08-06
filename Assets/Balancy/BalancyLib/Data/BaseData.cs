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
            var callbackDelegate = new LibraryMethods.Data.ParamChangedCallback(callback);
            var callbackHandle = GCHandle.Alloc(callbackDelegate);

            IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(callbackDelegate);
            var callbackId = LibraryMethods.Data.balancySubscribeBaseDataParamChange(_pointer, paramName, callbackPtr);
            _callbacks.Add(new CallbacksHolder
            {
                ParamName = paramName,
                Handle = callbackHandle,
                Id = callbackId
            });
        }
        
        ~BaseData()
        {
            foreach (var callback in _callbacks)
            {
                if (callback.Handle.IsAllocated)
                    callback.Handle.Free();
                LibraryMethods.Data.balancyUnsubscribeBaseDataParamChange(_pointer, callback.ParamName, callback.Id);
            }
        }
        
        protected T GetBaseDataParam<T>(string paramName) where T: BaseData, new()
        {
            var className = GetDataClassName<T>();
            var ptr = GetBaseDataParamPrivate(paramName, className);
            return CreateObject<T>(ptr);
        }
        
        protected SmartList<T> GetListBaseDataParam<T>(string paramName) where T: BaseData, new()
        {
            var className = GetDataClassName<T>();
            var ptr = GetListBaseDataParamPrivate(paramName, className);
            return CreateObject<SmartList<T>>(ptr);
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
            public GCHandle Handle;
            public int Id;
        }

        private readonly List<CallbacksHolder> _callbacks = new List<CallbacksHolder>();
    }
}
