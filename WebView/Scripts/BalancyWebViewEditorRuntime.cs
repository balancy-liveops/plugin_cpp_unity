using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

namespace Balancy.WebView
{
#if UNITY_EDITOR_OSX
    /// <summary>
    /// Implementation of WebView using macOS WebKit for runtime in the Unity Editor
    /// </summary>
    public class BalancyWebViewEditorRuntime
    {
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyOpenWebView(string url);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyCloseWebView();
        
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancySendMessage(string message);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern string _balancyCallJavaScript(string function, string[] args, int argsCount);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetViewportRect(float x, float y, float width, float height);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetTransparentBackground(bool transparent);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetOfflineCacheEnabled(bool enabled);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetDebugLogging(bool enabled);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyRegisterMessageCallback(MessageDelegate callback);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyRegisterLoadCompletedCallback(LoadCompletedDelegate callback);

        // Delegates for callbacks from native code
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MessageDelegate(string message);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LoadCompletedDelegate(bool success);
        
        // Singleton instance
        private static BalancyWebViewEditorRuntime _instance;
        
        // Event handlers
        private Action<string> _onMessageReceived;
        private Action<bool> _onLoadCompleted;
        
        // State tracking
        private bool _isInitialized = false;
        private bool _isWebViewOpen = false;
        
        /// <summary>
        /// Initialize the WebView implementation for editor runtime
        /// </summary>
        public static BalancyWebViewEditorRuntime Initialize(Action<string> onMessage, Action<bool> onLoadCompleted)
        {
            if (_instance == null)
            {
                _instance = new BalancyWebViewEditorRuntime(onMessage, onLoadCompleted);
            }
            
            return _instance;
        }
        
        private BalancyWebViewEditorRuntime(Action<string> onMessage, Action<bool> onLoadCompleted)
        {
            _onMessageReceived = onMessage;
            _onLoadCompleted = onLoadCompleted;
            
            try
            {
                // Set up callbacks
                _balancyRegisterMessageCallback(OnMessageCallback);
                _balancyRegisterLoadCompletedCallback(OnLoadCompletedCallback);
                
                _isInitialized = true;
                Debug.Log("[BalancyWebView] Editor runtime initialized with native WebKit");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Failed to initialize Editor runtime: {e.Message}");
                _isInitialized = false;
            }
        }
        
        /// <summary>
        /// Opens a WebView with the specified URL
        /// </summary>
        public bool OpenWebView(string url)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[BalancyWebView] Editor runtime not properly initialized");
                return false;
            }
            
            if (_isWebViewOpen)
            {
                CloseWebView();
            }
            
            try
            {
                bool success = _balancyOpenWebView(url);
                _isWebViewOpen = success;
                
                if (success)
                {
                    Debug.Log($"[BalancyWebView] WebView opened in Editor: {url}");
                }
                else
                {
                    Debug.LogError("[BalancyWebView] Failed to open WebView in Editor");
                }
                
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error opening WebView in Editor: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Closes the currently open WebView
        /// </summary>
        public void CloseWebView()
        {
            if (!_isWebViewOpen || !_isInitialized)
                return;
                
            try
            {
                _balancyCloseWebView();
                _isWebViewOpen = false;
                
                Debug.Log("[BalancyWebView] WebView closed in Editor");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error closing WebView in Editor: {e.Message}");
            }
        }
        
        /// <summary>
        /// Sends a message to the WebView
        /// </summary>
        public bool SendMessage(string message)
        {
            if (!_isWebViewOpen || !_isInitialized)
                return false;
                
            try
            {
                bool success = _balancySendMessage(message);
                
                if (success)
                {
                    Debug.Log($"[BalancyWebView] Message sent to WebView in Editor: {message}");
                }
                
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error sending message to WebView in Editor: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Calls a JavaScript function in the WebView
        /// </summary>
        public string CallJavaScript(string function, string[] args)
        {
            if (!_isWebViewOpen || !_isInitialized)
                return null;
                
            try
            {
                string result = _balancyCallJavaScript(function, args, args.Length);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error calling JavaScript in Editor: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Sets the viewport rectangle for the WebView
        /// </summary>
        public void SetViewportRect(float x, float y, float width, float height)
        {
            if (!_isInitialized)
                return;
                
            try
            {
                _balancySetViewportRect(x, y, width, height);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error setting viewport rect in Editor: {e.Message}");
            }
        }
        
        /// <summary>
        /// Sets the WebView background transparency
        /// </summary>
        public void SetTransparentBackground(bool transparent)
        {
            if (!_isInitialized)
                return;
                
            try
            {
                _balancySetTransparentBackground(transparent);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error setting transparent background in Editor: {e.Message}");
            }
        }
        
        /// <summary>
        /// Sets offline caching enabled/disabled
        /// </summary>
        public void SetOfflineCacheEnabled(bool enabled)
        {
            if (!_isInitialized)
                return;
                
            try
            {
                _balancySetOfflineCacheEnabled(enabled);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error setting offline cache in Editor: {e.Message}");
            }
        }
        
        /// <summary>
        /// Sets debug logging enabled/disabled
        /// </summary>
        public void SetDebugLogging(bool enabled)
        {
            if (!_isInitialized)
                return;
                
            try
            {
                _balancySetDebugLogging(enabled);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error setting debug logging in Editor: {e.Message}");
            }
        }
        
        /// <summary>
        /// Callback function for receiving messages from WebView
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(MessageDelegate))]
        private static void OnMessageCallback(string message)
        {
            if (_instance != null && _instance._onMessageReceived != null)
            {
                UnityEngine.Debug.Log($"[BalancyWebView] Message received from WebView in Editor: {message}");
                
                // Execute on main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    _instance._onMessageReceived(message);
                });
            }
        }
        
        /// <summary>
        /// Callback function for load completed events
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(LoadCompletedDelegate))]
        private static void OnLoadCompletedCallback(bool success)
        {
            if (_instance != null && _instance._onLoadCompleted != null)
            {
                UnityEngine.Debug.Log($"[BalancyWebView] WebView load completed in Editor: {success}");
                
                // Execute on main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    _instance._onLoadCompleted(success);
                });
            }
        }
    }
    
    /// <summary>
    /// Helper class for dispatching callbacks to the main Unity thread
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        
        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
        
        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
        
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
    }
#endif
}
