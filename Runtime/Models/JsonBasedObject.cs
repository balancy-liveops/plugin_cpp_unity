using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Localization;

namespace Balancy.Models
{
    public class JsonBasedObject
    {
        public virtual void InitData()
        {
        }

        protected IntPtr _pointer;
        protected bool TempCopy = false;//We don't subscribe for updates for such objects

        // Reverse index: native pointer -> all live C# wrappers referencing it.
        // When the C++ object is destroyed (profile recreation), we look the
        // pointer up here and null out _pointer on every wrapper so that any
        // reference the game code still holds (e.g. captured in a timer delegate)
        // stops dereferencing freed memory. Multiple wrappers may share one
        // pointer, so the value is a set.
        private static readonly Dictionary<IntPtr, HashSet<JsonBasedObject>> _liveByPointer
            = new Dictionary<IntPtr, HashSet<JsonBasedObject>>();
        // Guards _liveByPointer. Wrappers can be created/refreshed from a worker
        // thread (e.g. list refresh) while InvalidateByPointer runs on the main
        // thread, so all index mutations must be serialized.
        private static readonly object _liveByPointerLock = new object();

        public bool Equals(IntPtr ptr)
        {
            return _pointer == ptr;
        }

        public void SetData(IntPtr p)
        {
            if (_pointer == p)
                return;

            lock (_liveByPointerLock)
            {
                UnregisterFromLiveIndex();
                _pointer = p;
                RegisterInLiveIndex();
            }
        }

        internal IntPtr GetRawPointer()
        {
            return _pointer;
        }

        internal void RefreshData(IntPtr p)
        {
            CleanUp(true);
            SetData(p);
            InitData();
        }

        internal virtual void CleanUp(bool parentWasDestroyed)
        {
            SetData(IntPtr.Zero);
        }

        private void RegisterInLiveIndex()
        {
            if (_pointer == IntPtr.Zero)
                return;

            if (!_liveByPointer.TryGetValue(_pointer, out var set))
            {
                set = new HashSet<JsonBasedObject>();
                _liveByPointer.Add(_pointer, set);
            }
            set.Add(this);
        }

        private void UnregisterFromLiveIndex()
        {
            if (_pointer == IntPtr.Zero)
                return;

            if (_liveByPointer.TryGetValue(_pointer, out var set))
            {
                set.Remove(this);
                if (set.Count == 0)
                    _liveByPointer.Remove(_pointer);
            }
        }

        /// <summary>
        /// Called when the native C++ object at <paramref name="ptr"/> has been
        /// destroyed. Nulls _pointer on every live C# wrapper referencing it so
        /// that subsequent calls become safe no-ops instead of use-after-free.
        /// Must be called on the Unity main thread.
        /// </summary>
        internal static void InvalidateByPointer(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return;

            List<JsonBasedObject> wrappers;
            lock (_liveByPointerLock)
            {
                if (!_liveByPointer.TryGetValue(ptr, out var set))
                    return;

                // Copy first: clearing _pointer triggers UnregisterFromLiveIndex
                // which mutates the set/dictionary we are iterating.
                wrappers = new List<JsonBasedObject>(set);
            }

            foreach (var wrapper in wrappers)
                wrapper.SetData(IntPtr.Zero);
        }
        
        public static string GetModelClassName<T>()
        {
            var fullName = typeof(T).FullName;
            var className = fullName?.Replace("Balancy.Models.", "");
            return className;
        }
        
        public static string GetDataClassName<T>()
        {
            var fullName = typeof(T).FullName;
            var className = fullName?.Replace("Balancy.Data.", "");
            return className;
        }
		
        // public static bool operator== (BaseModel obj1, BaseModel obj2)
        // {
        // 	return string.IsNullOrEmpty(obj1?.UnnyId) || string.IsNullOrEmpty(obj2?.UnnyId)
        // 		? obj1 == obj2
        // 		: string.Equals(obj1.UnnyId, obj2.UnnyId);
        // }
        //
        // public static bool operator!= (BaseModel obj1, BaseModel obj2)
        // {
        // 	return string.IsNullOrEmpty(obj1?.UnnyId) || string.IsNullOrEmpty(obj2?.UnnyId)
        // 		? obj1 != obj2
        // 		: !string.Equals(obj1.UnnyId, obj2.UnnyId);
        // }
        
        internal static string GetTemplateName(IntPtr instance)
        {
            return GetStringFromIntPtr(LibraryMethods.Models.balancyGetTemplateName(instance));
        }

        protected UnnyColor GetColor(string paramName)
        {
            return new UnnyColor(GetStringParam(paramName));
        }
        
        protected UnnyColor[] GetColors(string paramName)
        {
            var strings = GetStringArrayParam(paramName);
            var result = new UnnyColor[strings.Length];
            for (int i = 0; i < strings.Length; i++)
                result[i] = new UnnyColor(strings[i]);
            return result;
        }
        
        protected LocalizedString GetLocalizedString(string paramName)
        {
            return new LocalizedString(GetStringParam(paramName));
        }

        protected LocalizedString[] GetLocalizedStrings(string paramName)
        {
            var strings = GetStringArrayParam(paramName);
            var result = new LocalizedString[strings.Length];
            for (int i = 0; i < strings.Length; i++)
                result[i] = new LocalizedString(strings[i]);
            return result;
        }

        protected ScriptRef GetScriptRef(string scriptId)
        {
            if (string.IsNullOrEmpty(scriptId))
                return null;
            return new ScriptRef(scriptId);
        }

        protected ScriptRef[] GetScriptRefs(string[] scriptIds)
        {
            if (scriptIds == null || scriptIds.Length == 0)
                return Array.Empty<ScriptRef>();
            var result = new ScriptRef[scriptIds.Length];
            for (int i = 0; i < scriptIds.Length; i++)
                result[i] = new ScriptRef(scriptIds[i]);
            return result;
        }

        protected T GetObjectParam<T>(string paramName) where T: JsonBasedObject, new()
        {
            var className = GetModelClassName<T>();
            var ptr = GetObjectParamPrivate(paramName, className);
            return CreateObject<T>(ptr, TempCopy);
        }

        private void MarkAsTempObject()
        {
            TempCopy = true;
        }
        
        internal static T CreateObject<T>(IntPtr ptr, bool tempCopy = true) where T: JsonBasedObject, new()
        {
            if (ptr == IntPtr.Zero)
                return null;
            
            var obj = new T();
            obj.SetData(ptr);
            if (tempCopy)
                obj.MarkAsTempObject();
            obj.InitData();
            return obj;
        }
        
        protected T[] GetObjectArrayParam<T>(string paramName) where T: JsonBasedObject, new()
        {
            var className = GetModelClassName<T>();
            
            IntPtr ptr = GetObjectArrayParamPrivate(paramName, className, out int size);
            
            if (ptr == IntPtr.Zero || size <= 0)
                return Array.Empty<T>();

            IntPtr[] list = new IntPtr[size];
            Marshal.Copy(ptr, list, 0, size);
            T[] result = new T[size];
            for (int i = 0; i < size; i++)
            {
                var p = list[i];
                if (p == IntPtr.Zero)
                {
                    result[i] = null;
                    continue;
                }

                var obj = new T();
                obj.SetData(list[i]);
                obj.InitData();

                result[i] = obj;
            }
            return result;
        }
        
        private IntPtr GetObjectParamPrivate(string paramName, string fileName)
        {
            return LibraryMethods.Models.balancyGetObjectParam(_pointer, paramName, fileName);
        }
        
        private IntPtr GetObjectArrayParamPrivate(string paramName, string fileName, out int size)
        {
            return LibraryMethods.Models.balancyGetObjectArrayParam(_pointer, paramName, fileName, out size);
        }
        
        public int GetIntParam(string paramName)
        {
            return LibraryMethods.Models.balancyGetIntParam(_pointer, paramName);
        }
        
        public long GetLongParam(string paramName)
        {
            return LibraryMethods.Models.balancyGetLongParam(_pointer, paramName);
        }
        
        public float GetFloatParam(string paramName)
        {
            return LibraryMethods.Models.balancyGetFloatParam(_pointer, paramName);
        }
        
        public bool GetBoolParam(string paramName)
        {
            return LibraryMethods.Models.balancyGetBoolParam(_pointer, paramName);
        }
        
        public string GetStringParam(string paramName)
        {
            return GetStringFromIntPtr(LibraryMethods.Models.balancyGetStringParam(_pointer, paramName));
        }
        
        protected T[] GetEnumArrayParam<T>(string name)
        {
            var intArray = GetIntArrayParam(name);
            if (intArray.Length == 0)
                return Array.Empty<T>();

            T[] enumArray = new T[intArray.Length];
            for (int i = 0; i < intArray.Length; i++)
                enumArray[i] = (T)Enum.ToObject(typeof(T), intArray[i]);
            return enumArray;
        }
        
        protected int[] GetIntArrayParam(string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetIntArrayParam(_pointer, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
                return Array.Empty<int>();

            int[] result = new int[size];
            Marshal.Copy(ptr, result, 0, size);
            return result;
        }
        
        protected long[] GetLongArrayParam(string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetLongArrayParam(_pointer, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
                return Array.Empty<long>();

            long[] result = new long[size];
            Marshal.Copy(ptr, result, 0, size);
            return result;
        }

        protected float[] GetFloatArrayParam(string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetFloatArrayParam(_pointer, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
                return Array.Empty<float>();

            float[] result = new float[size];
            Marshal.Copy(ptr, result, 0, size);
            return result;
        }

        protected bool[] GetBoolArrayParam(string name)
        {
            int size;
            IntPtr ptr = LibraryMethods.Models.balancyGetBoolArrayParam(_pointer, name, out size);

            if (ptr == IntPtr.Zero || size <= 0)
                return Array.Empty<bool>();

            byte[] byteResult = new byte[size];
            Marshal.Copy(ptr, byteResult, 0, size);
            bool[] boolResult = Array.ConvertAll(byteResult, b => b != 0);
            return boolResult;
        }


        protected string[] GetStringArrayParam(string name)
        {
            IntPtr ptr = LibraryMethods.Models.balancyGetStringArrayParam(_pointer, name, out var size);
            return ReadStringArrayValues(ptr, size);
        }
        
        internal static string[] ReadStringArrayValues(IntPtr ptr, int size)
        {
            if (ptr == IntPtr.Zero || size <= 0)
                return Array.Empty<string>();
            
            IntPtr[] ptrArray = new IntPtr[size];
            Marshal.Copy(ptr, ptrArray, 0, size);

            string[] result = new string[size];
            for (int i = 0; i < size; i++)
                result[i] = Marshal.PtrToStringAnsi(ptrArray[i]);

            LibraryMethods.Models.balancyFreeStringArray(ptr, size);
            return result;
        }
        
        internal static string GetStringFromIntPtr(IntPtr ptr)
        {
            return Marshal.PtrToStringAnsi(ptr);
        }
        
        protected T GetModelByUnnyId<T>(string unnyId) where T: BaseModel
        {
            return CMS.GetModelByUnnyId<T>(unnyId);
        }

        protected T[] GetModelsByUnnyIds<T>(string[] unnyIds) where T : BaseModel
        {
            if (unnyIds == null || unnyIds.Length == 0)
                return Array.Empty<T>();

            var storeItems = new T[unnyIds.Length];
            for (int i = 0; i < unnyIds.Length; i++)
                storeItems[i] = GetModelByUnnyId<T>(unnyIds[i]);
            return storeItems;
        }
        
        public string ToJsonString(int depth, bool pretty)
        {
            return GetStringFromIntPtr(LibraryMethods.General.balancyGetParsedObject(GetRawPointer(), depth, pretty));
        }
    }
}
