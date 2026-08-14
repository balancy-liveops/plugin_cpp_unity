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
        // Static, and deliberately NOT owned by the component: a native callback
        // arriving on a Balancy worker thread has to be able to queue work
        // without touching a single Unity API. Instance() cannot be used for
        // that — it does new GameObject / AddComponent / DontDestroyOnLoad when
        // the singleton is missing, and even the `!_instance` check is a
        // UnityEngine.Object comparison that calls into native code. All of that
        // is main-thread-only and takes IL2CPP down when hit from a worker.
        //
        // Keeping the queue static also means work queued before the dispatcher
        // exists (or while it is being recreated) is not lost.
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        private static UnityMainThreadDispatcher _instance;

        private bool _isDestroyed = false;
        private float _sessionStartTime = 0f;

        // ManagedThreadId of the thread that drives ProcessQueue (the Unity main
        // thread in play mode, the editor update thread in edit mode). Captured
        // the first time the queue is pumped. Used by RunOnMainThread to decide
        // whether a callback can run inline or must be marshalled.
        private static volatile int _mainThreadId = -1;
        
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
                var obj = new GameObject("MainThreadDispatcher (Hidden)");

                // Hide the object from the hierarchy
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                // Create the dispatcher differently based on whether we're in play mode
                if (Application.isPlaying)
                {
                    obj.hideFlags = HideFlags.HideInHierarchy;
                    DontDestroyOnLoad(obj);
                }
                else
                {
                    obj.hideFlags = HideFlags.HideAndDontSave;
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
                _instance.ProcessQueue();
            }
            else if (_isEditorUpdateRegistered)
            {
                // If instance is gone but we're still registered, unregister
                EditorApplication.update -= EditorUpdate;
                _isEditorUpdateRegistered = false;
            }
        }
#endif

        // Queue an action for the main thread from ANY thread, including native
        // worker threads. Touches no Unity API, allocates no GameObject, and
        // never calls Instance(). This is what P/Invoke callbacks must use.
        public static void EnqueueFromAnyThread(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        // True when the caller is on the thread that pumps the queue. False
        // before the queue has ever been pumped, which is the safe answer: the
        // caller then marshals instead of running inline.
        public static bool IsMainThread =>
            _mainThreadId != -1 &&
            System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        // Run on the main thread — inline if already there, queued otherwise.
        // Safe from any thread; see EnqueueFromAnyThread.
        public static void RunOnMainThreadSafe(Action action)
        {
            if (action == null) return;

            if (IsMainThread)
            {
                action.Invoke();
                return;
            }

            EnqueueFromAnyThread(action);
        }

        // Enqueue actions to be run on the main thread
        public void Enqueue(Action action)
        {
            EnqueueFromAnyThread(action);
        }

        // Run an action on the Unity main thread. If we are already on the main
        // thread, run it inline (no extra frame of latency); otherwise marshal it
        // through the queue. Used so background-thread continuations (e.g. async
        // Tasks.Periodic timers resuming on a thread-pool thread) never P/Invoke
        // into native code off the main thread, which can hit dangling pointers
        // freed concurrently by profile recreation/reset.
        public void RunOnMainThread(Action action)
        {
            RunOnMainThreadSafe(action);
        }

        // Execute actions from the queue on the main thread (called in Play mode)
        private void Update()
        {
            ProcessQueue();
            
#if UNITY_WEBGL && !UNITY_EDITOR
            // Update Balancy timer on WebGL since threads are not available
            // Only call if Controller is ready to avoid errors before initialization
            if (Controller.IsReadyToUse)
            {
                try
                {
                    LibraryMethods.General.balancyUpdate();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
#endif
        }

        // Common queue processing method
        // NOTE: We copy the queue under the lock, then execute outside the lock.
        // This prevents a deadlock where:
        //   - Main thread holds _executionQueue lock, then a callback tries to acquire C++ m_ContextMutex
        //   - Scheduler thread holds m_ContextMutex, then tries to Enqueue (which needs _executionQueue lock)
        private void ProcessQueue()
        {
            // ProcessQueue is only ever driven by the Unity main thread (play
            // mode Update) or the editor update thread. Record its id so
            // RunOnMainThread can detect when it is safe to run inline.
            if (_mainThreadId == -1)
                _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            Action[] actions;
            lock (_executionQueue)
            {
                if (_executionQueue.Count == 0)
                    return;
                actions = _executionQueue.ToArray();
                _executionQueue.Clear();
            }

            foreach (var action in actions)
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
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
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (!Controller.IsReadyToUse)
                return;

            if (pauseStatus)
            {
                int sessionSeconds = Mathf.Max(0, (int)(Time.realtimeSinceStartup - _sessionStartTime));
                API.NotifyAppPause(sessionSeconds);
            }
            else
            {
                _sessionStartTime = Time.realtimeSinceStartup;
                API.NotifyAppResume();
            }
        }

        private void OnApplicationQuit()
        {
            StopDispatcher();
            Main.Stop();
        }

        // Explicitly stop the dispatcher, removing it when no longer needed
        private static void StopDispatcher()
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
