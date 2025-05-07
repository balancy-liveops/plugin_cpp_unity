using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Balancy
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        private static UnityMainThreadDispatcher _instance;

        private bool _isDestroyed = false;
        
#if UNITY_EDITOR
        // Flag to check if EditorUpdate is registered
        private static bool _isEditorUpdateRegistered = false;
#endif

        // Singleton pattern to get or create the dispatcher
        public static UnityMainThreadDispatcher Instance()
        {
            // Check if the instance already exists and hasn't been destroyed
            if (!_instance || _instance._isDestroyed)
            {
                // Create the dispatcher differently based on whether we're in play mode
                if (Application.isPlaying)
                {
                    var obj = new GameObject("MainThreadDispatcher (Hidden)");

                    // Hide the object from the hierarchy
                    obj.hideFlags = HideFlags.HideAndDontSave;

                    _instance = obj.AddComponent<UnityMainThreadDispatcher>();

                    // Ensure the object is destroyed on game stop or when a new scene is loaded
                    DontDestroyOnLoad(obj);
                }
                else
                {
                    // In Editor mode, we create a hidden game object but don't need DontDestroyOnLoad
                    var obj = new GameObject("MainThreadDispatcher (Editor)");
                    obj.hideFlags = HideFlags.HideAndDontSave;
                    _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    
#if UNITY_EDITOR
                    // Register with EditorApplication.update to process the queue in Editor mode
                    if (!_isEditorUpdateRegistered)
                    {
                        EditorApplication.update += EditorUpdate;
                        _isEditorUpdateRegistered = true;
                        
                        // Make sure we unregister when the editor is exiting play mode
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    }
#endif
                }
            }

            return _instance;
        }

#if UNITY_EDITOR
        // Handler for editor play mode state changes
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Clean up editor update registration when exiting play mode
                if (_isEditorUpdateRegistered)
                {
                    EditorApplication.update -= EditorUpdate;
                    _isEditorUpdateRegistered = false;
                }
                
                // Also clean up the play mode instance
                if (_instance != null)
                {
                    DestroyImmediate(_instance.gameObject);
                    _instance = null;
                }
            }
        }
        
        // Update method for Editor mode that processes the queue
        private static void EditorUpdate()
        {
            if (_instance != null && !_instance._isDestroyed)
            {
                ProcessQueue();
            }
            else if (_isEditorUpdateRegistered)
            {
                // If instance is gone but we're still registered, unregister
                EditorApplication.update -= EditorUpdate;
                _isEditorUpdateRegistered = false;
            }
        }
#endif

        // Enqueue actions to be run on the main thread
        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        // Execute actions from the queue on the main thread (called in Play mode)
        private void Update()
        {
            ProcessQueue();
        }

        // Common queue processing method
        private static void ProcessQueue()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue().Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        // Cleanup the instance on destroy
        private void OnDestroy()
        {
            _isDestroyed = true; // Mark as destroyed to avoid reusing the old instance
            
#if UNITY_EDITOR
            // Unregister editor update when destroyed
            if (_isEditorUpdateRegistered && !Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                _isEditorUpdateRegistered = false;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
#endif
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
                if (Application.isPlaying)
                {
                    Destroy(_instance.gameObject); // Destroy the game object
                }
                else
                {
                    DestroyImmediate(_instance.gameObject);
                }
                
                _instance = null; // Clear the static instance reference
                
#if UNITY_EDITOR
                // Unregister editor update when stopped
                if (_isEditorUpdateRegistered)
                {
                    EditorApplication.update -= EditorUpdate;
                    _isEditorUpdateRegistered = false;
                    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                }
#endif
            }
        }
    }
}
