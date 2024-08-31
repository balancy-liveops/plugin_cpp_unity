using System;
using System.Collections.Generic;

namespace Balancy.Data
{
    public class SmartList<T> : BaseData where T : BaseData, new()
    {
        private readonly List<T> _list = new List<T>();

        public T Add()
        {
            var ptr = LibraryMethods.Data.balancySmartListAddElement(_pointer);
            return FindElement(ptr);
        }
        
        public void RemoveAt(int index)
        {
            var element = _list[index];
            element.CleanUp(false);
            // _list.RemoveAt(index);
            LibraryMethods.Data.balancySmartListRemoveElementAt(_pointer, index);
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
            CleanUp(false);
            LibraryMethods.Data.balancySmartListClear(_pointer);
        }

        public override void InitData()
        {
            base.InitData();

            var size = LibraryMethods.Data.balancySmartListGetSize(_pointer);
            for (int i = 0; i < size; i++)
            {
                var ptr = LibraryMethods.Data.balancySmartListGetElementAt(_pointer, i);
                var element = CreateObject<T>(ptr, TempCopy);
                _list.Add(element);
            }
        }
        
        public T this[int index] => _list[index];

        // public List<T> ToList()
        // {
        //     return new List<T>(_list);
        // }

        // public T[] ToArray()
        // {
        //     var arr = new T[_list.Count];
        //     for (int i = 0; i < _list.Count; i++)
        //         arr[i] = _list[i];
        //     return arr;
        // }

        internal void SubscribeForUpdates(string paramName, BaseData parent)
        {
            parent.SubscribeForParamChange(paramName, RefreshList);
        }

        private T FindElement(IntPtr ptr)
        {
            foreach (var child in _list)
                if (child.Equals(ptr))
                    return child;
            return null;
        }

        private void RefreshList()
        {
            CleanUp(false);
            InitData();
        }

        internal override void CleanUp(bool parentWasDestroyed)
        {
            base.CleanUp(parentWasDestroyed);
            foreach (var child in _list)
                child.CleanUp(parentWasDestroyed);
            _list.Clear();
        }
    }
}
