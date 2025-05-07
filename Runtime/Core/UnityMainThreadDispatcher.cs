using System;
using System.Collections.Generic;
using UnityEngine;

namespace Balancy
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        private static UnityMainThreadDispatcher _instance;

        private bool _isDestroyed = false;

        // Singleton pattern to get or create the dispatcher
        public static UnityMainThreadDispatcher Instance()
        {
            // Check if the instance already exists and hasn't been destroyed
            if (!_instance || _instance._isDestroyed)
            {
                var obj = new GameObject("MainThreadDispatcher (Hidden)");

                // Hide the object from the hierarchy
                obj.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

                _instance = obj.AddComponent<UnityMainThreadDispatcher>();

                // Ensure the object is destroyed on game stop or when a new scene is loaded
                if (Application.isPlaying)
                    DontDestroyOnLoad(obj);
            }

            return _instance;
        }

        // Enqueue actions to be run on the main thread
        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        // Execute actions from the queue on the main thread
        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        // Cleanup the instance on destroy
        private void OnDestroy()
        {
            _isDestroyed = true; // Mark as destroyed to avoid reusing the old instance
        }
        
        private void OnApplicationQuit()
        {
            Main.Stop();
        }

        // Explicitly stop the dispatcher, removing it when no longer needed
        public static void StopDispatcher()
        {
            if (_instance)
            {
                Destroy(_instance.gameObject); // Destroy the game object
                _instance = null; // Clear the static instance reference
            }
        }
    }
}
