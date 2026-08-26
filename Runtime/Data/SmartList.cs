using System;
using System.Collections.Generic;

namespace Balancy.Data
{
    public class SmartList<T> : BaseData where T : BaseData, new()
    {
        private List<T> _list = new List<T>();

        public T Add()
        {
            if (!EnsureAlive(nameof(Add)))
                return null;

            var ptr = LibraryMethods.Data.balancySmartListAddElement(_pointer);
            return FindElement(ptr);
        }
        
        public void RemoveAt(int index)
        {
            TryRemoveAt(index);
        }

        /// <summary>
        /// Removes an element when the list is alive and the index is valid.
        /// Returns false and leaves both managed and native lists unchanged otherwise.
        /// </summary>
        public bool TryRemoveAt(int index)
        {
            if (!EnsureAlive(nameof(RemoveAt)) || !ValidateElementIndex(index, nameof(RemoveAt)))
                return false;

            var element = _list[index];
            // Native mutation synchronously notifies the parent. Remove the wrapper
            // before crossing that boundary so RefreshList cannot inspect an object
            // whose native pointer has already been cleared.
            _list.RemoveAt(index);
            element.CleanUp(false);
            LibraryMethods.Data.balancySmartListRemoveElementAt(_pointer, index);
            return true;
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
            if (!ValidateStartIndex(startIndex, nameof(FindIndex)))
                return -1;

            return _list.FindIndex(startIndex, match);
        }

        public void Clear()
        {
            if (!EnsureAlive(nameof(Clear)))
                return;

            foreach (var child in _list)
                child.CleanUp(false);
            _list.Clear();
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
                if (element != null)
                    _list.Add(element);
            }
        }
        
        public T this[int index] => _list[index];

        /// <summary>
        /// Reads an element without throwing when the index is outside the list.
        /// </summary>
        public bool TryGet(int index, out T value)
        {
            if (index >= 0 && index < _list.Count)
            {
                value = _list[index];
                return true;
            }

            value = null;
            return false;
        }

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
            if (_pointer == IntPtr.Zero)
                return;

            var size = LibraryMethods.Data.balancySmartListGetSize(_pointer);
            List<IntPtr> newListIds = new List<IntPtr>();
            for (int i = 0; i < size; i++)
            {
                var ptr = LibraryMethods.Data.balancySmartListGetElementAt(_pointer, i);
                newListIds.Add(ptr);
            }
            
            List<T> newList = new List<T>();
            foreach (var ptr in newListIds)
            {
                bool found = false;
                for (int i = 0; i < _list.Count; i++)
                {
                    var oldElement = _list[i];
                    if (oldElement.Equals(ptr))
                    {
                        newList.Add(oldElement);
                        _list.RemoveAt(i);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var element = CreateObject<T>(ptr, TempCopy);
                    if (element != null)
                        newList.Add(element);
                }
            }
            
            //Clean up the elements that are no longer in the list
            foreach (var child in _list)
                child.CleanUp(false);
            _list.Clear();
            
            _list = newList;
        }

        private bool EnsureAlive(string operation)
        {
            if (_pointer != IntPtr.Zero)
                return true;

            UnityEngine.Debug.LogError($"[Balancy] SmartList<{typeof(T).Name}>.{operation} ignored: the list is no longer valid.");
            return false;
        }

        private bool ValidateElementIndex(int index, string operation)
        {
            if (index >= 0 && index < _list.Count)
                return true;

            UnityEngine.Debug.LogError($"[Balancy] SmartList<{typeof(T).Name}>.{operation} ignored: index {index} is outside [0, {_list.Count}).");
            return false;
        }

        private bool ValidateStartIndex(int startIndex, string operation)
        {
            if (startIndex >= 0 && startIndex <= _list.Count)
                return true;

            UnityEngine.Debug.LogError($"[Balancy] SmartList<{typeof(T).Name}>.{operation} ignored: startIndex {startIndex} is outside [0, {_list.Count}].");
            return false;
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
