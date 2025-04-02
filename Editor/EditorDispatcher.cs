using System.Collections.Generic;
using UnityEditor;
using System;
using UnityEngine;

namespace Balancy.Editor
{
    public class EditorDispatcher
    {
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        private bool _isInitialized = false;

        public void StartEditorDispatcher()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                EditorApplication.update += UpdateEditorQueue;
            }
        }

        public void StopEditorDispatcher()
        {
            if (_isInitialized)
            {
                EditorApplication.update -= UpdateEditorQueue;
                _isInitialized = false;
            }
        }

        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void UpdateEditorQueue()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }
}