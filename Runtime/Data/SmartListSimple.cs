using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Balancy.Data
{
    public class SmartListSimple<T> : BaseData
    {
        private List<T> _list = new List<T>();

        public void Add(T value)
        {
            if (typeof(T) == typeof(int))
            {
                LibraryMethods.Data.balancySmartListSimpleIntAddElement(_pointer, Convert.ToInt32(value));
            }
            else if (typeof(T) == typeof(float))
            {
                LibraryMethods.Data.balancySmartListSimpleFloatAddElement(_pointer, Convert.ToSingle(value));
            }
            else if (typeof(T) == typeof(string))
            {
                LibraryMethods.Data.balancySmartListSimpleStringAddElement(_pointer, value as string);
            }
            else if (typeof(T) == typeof(long))
            {
                LibraryMethods.Data.balancySmartListSimpleLongAddElement(_pointer, Convert.ToInt64(value));
            }
            else if (typeof(T) == typeof(bool))
            {
                LibraryMethods.Data.balancySmartListSimpleBoolAddElement(_pointer, Convert.ToBoolean(value));
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }

            // Refresh the list to include the new element
            RefreshList();
        }

        public void RemoveAt(int index)
        {
            if (typeof(T) == typeof(int))
            {
                LibraryMethods.Data.balancySmartListSimpleIntRemoveAt(_pointer, index);
            }
            else if (typeof(T) == typeof(float))
            {
                LibraryMethods.Data.balancySmartListSimpleFloatRemoveAt(_pointer, index);
            }
            else if (typeof(T) == typeof(string))
            {
                LibraryMethods.Data.balancySmartListSimpleStringRemoveAt(_pointer, index);
            }
            else if (typeof(T) == typeof(long))
            {
                LibraryMethods.Data.balancySmartListSimpleLongRemoveAt(_pointer, index);
            }
            else if (typeof(T) == typeof(bool))
            {
                LibraryMethods.Data.balancySmartListSimpleBoolRemoveAt(_pointer, index);
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }

            RefreshList();
        }

        public void Set(int index, T value)
        {
            if (typeof(T) == typeof(int))
            {
                LibraryMethods.Data.balancySmartListSimpleIntSetElementAt(_pointer, index, Convert.ToInt32(value));
            }
            else if (typeof(T) == typeof(float))
            {
                LibraryMethods.Data.balancySmartListSimpleFloatSetElementAt(_pointer, index, Convert.ToSingle(value));
            }
            else if (typeof(T) == typeof(string))
            {
                LibraryMethods.Data.balancySmartListSimpleStringSetElementAt(_pointer, index, value as string);
            }
            else if (typeof(T) == typeof(long))
            {
                LibraryMethods.Data.balancySmartListSimpleLongSetElementAt(_pointer, index, Convert.ToInt64(value));
            }
            else if (typeof(T) == typeof(bool))
            {
                LibraryMethods.Data.balancySmartListSimpleBoolSetElementAt(_pointer, index, Convert.ToBoolean(value));
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }

            RefreshList();
        }

        public int Count
        {
            get { return _list.Count; }
        }

        public List<T>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int FindIndex(Predicate<T> match)
        {
            return _list.FindIndex(match);
        }

        public int FindIndex(int startIndex, Predicate<T> match)
        {
            return _list.FindIndex(startIndex, match);
        }

        public void Clear()
        {
            if (typeof(T) == typeof(int))
            {
                LibraryMethods.Data.balancySmartListSimpleIntClear(_pointer);
            }
            else if (typeof(T) == typeof(float))
            {
                LibraryMethods.Data.balancySmartListSimpleFloatClear(_pointer);
            }
            else if (typeof(T) == typeof(string))
            {
                LibraryMethods.Data.balancySmartListSimpleStringClear(_pointer);
            }
            else if (typeof(T) == typeof(long))
            {
                LibraryMethods.Data.balancySmartListSimpleLongClear(_pointer);
            }
            else if (typeof(T) == typeof(bool))
            {
                LibraryMethods.Data.balancySmartListSimpleBoolClear(_pointer);
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }

            _list.Clear();
        }

        public override void InitData()
        {
            base.InitData();
            RefreshList();
        }

        public T this[int index] => _list[index];

        internal void SubscribeForUpdates(string paramName, BaseData parent)
        {
            parent.SubscribeForParamChange(paramName, RefreshList);
        }

        private void RefreshList()
        {
            int size = GetSize();
            _list.Clear();

            for (int i = 0; i < size; i++)
            {
                T value = GetElementAt(i);
                _list.Add(value);
            }
        }

        private int GetSize()
        {
            if (typeof(T) == typeof(int))
            {
                return LibraryMethods.Data.balancySmartListSimpleIntGetSize(_pointer);
            }
            else if (typeof(T) == typeof(float))
            {
                return LibraryMethods.Data.balancySmartListSimpleFloatGetSize(_pointer);
            }
            else if (typeof(T) == typeof(string))
            {
                return LibraryMethods.Data.balancySmartListSimpleStringGetSize(_pointer);
            }
            else if (typeof(T) == typeof(long))
            {
                return LibraryMethods.Data.balancySmartListSimpleLongGetSize(_pointer);
            }
            else if (typeof(T) == typeof(bool))
            {
                return LibraryMethods.Data.balancySmartListSimpleBoolGetSize(_pointer);
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }
        }

        private T GetElementAt(int index)
        {
            if (typeof(T) == typeof(int))
            {
                int value = LibraryMethods.Data.balancySmartListSimpleIntGetElementAt(_pointer, index);
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(float))
            {
                float value = LibraryMethods.Data.balancySmartListSimpleFloatGetElementAt(_pointer, index);
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(string))
            {
                IntPtr ptr = LibraryMethods.Data.balancySmartListSimpleStringGetElementAt(_pointer, index);
                string value = Marshal.PtrToStringAnsi(ptr);
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(long))
            {
                long value = LibraryMethods.Data.balancySmartListSimpleLongGetElementAt(_pointer, index);
                return (T)(object)value;
            }
            else if (typeof(T) == typeof(bool))
            {
                bool value = LibraryMethods.Data.balancySmartListSimpleBoolGetElementAt(_pointer, index);
                return (T)(object)value;
            }
            else
            {
                throw new NotSupportedException($"SmartListSimple does not support type {typeof(T).Name}");
            }
        }

        internal override void CleanUp(bool parentWasDestroyed)
        {
            base.CleanUp(parentWasDestroyed);
            _list.Clear();
        }
    }
}
