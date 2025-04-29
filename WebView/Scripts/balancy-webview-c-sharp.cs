using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy.WebView
{
    /// <summary>
    /// Main interface for the Balancy WebView plugin.
    /// Provides methods to open, close, and interact with a WebView overlay.
    /// </summary>
    public class BalancyWebView : MonoBehaviour
    {
        #region Singleton Implementation

        private static BalancyWebView _instance;

        /// <summary>
        /// Singleton instance of the BalancyWebView
        /// </summary>
        public static BalancyWebView Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Check if an instance already exists in the scene
                    _instance = FindObjectOfType<BalancyWebView>();

                    // If not, create a new GameObject with the component
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("BalancyWebView");
                        _instance = go.AddComponent<BalancyWebView>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event triggered when a message is received from the WebView
        /// </summary>
        public event Action<string> OnMessage;

        /// <summary>
        /// Event triggered when the WebView finishes loading a page
        /// </summary>
        public event Action<bool> OnLoadCompleted;

        /// <summary>
        /// Event triggered when offline caching is completed
        /// </summary>
        public event Action<bool> OnCacheCompleted;

        /// <summary>
        /// Event triggered when the WebView is closed
        /// </summary>
        public event Action OnClosed;

        #endregion

        #region Private fields

        private bool _isWebViewOpen = false;
        private bool _transparentBackground = false;
        private bool _offlineCacheEnabled = false;
        private float _viewportX = 0f;
        private float _viewportY = 0f;
        private float _viewportWidth = 1f;
        private float _viewportHeight = 1f;
        private bool _debugLogging = false;

        #endregion

        #region Native Plugin Interface

        // Native plugin methods - these will be implemented differently for each platform
        #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool _balancyOpenWebView(string url);

        [DllImport("__Internal")]
        private static extern void _balancyCloseWebView();

        [DllImport("__Internal")]
        private static extern bool _balancySendMessage(string message);

        [DllImport("__Internal")]
        private static extern string _balancyCallJavaScript(string function, string[] args, int argsCount);

        [DllImport("__Internal")]
        private static extern void _balancySetViewportRect(float x, float y, float width, float height);

        [DllImport("__Internal")]
        private static extern void _balancySetTransparentBackground(bool transparent);

        [DllImport("__Internal")]
        private static extern void _balancySetOfflineCacheEnabled(bool enabled);

        [DllImport("__Internal")]
        private static extern void _balancySetDebugLogging(bool enabled);
        #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("BalancyWebViewMac")]
        private static extern bool _balancyOpenWebView(string url);

        [DllImport("BalancyWebViewMac")]
        private static extern void _balancyCloseWebView();

        [DllImport("BalancyWebViewMac")]
        private static extern bool _balancySendMessage(string message);

        [DllImport("BalancyWebViewMac")]
        private static extern string _balancyCallJavaScript(string function, string[] args, int argsCount);

        [DllImport("BalancyWebViewMac")]
        private static extern void _balancySetViewportRect(float x, float y, float width, float height);

        [DllImport("BalancyWebViewMac")]
        private static extern void _balancySetTransparentBackground(bool transparent);

        [DllImport("BalancyWebViewMac")]
        private static extern void _balancySetOfflineCacheEnabled(bool enabled);

        [DllImport("BalancyWebViewMac")]
        private static extern void _balancySetDebugLogging(bool enabled);
        #endif

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_isWebViewOpen)
            {
                CloseWebView();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Opens a WebView with the specified URL
        /// </summary>
        /// <param name="url">The URL to open in the WebView</param>
        /// <returns>True if the WebView was opened successfully, false otherwise</returns>
        public bool OpenWebView(string url)
        {
            if (_isWebViewOpen)
            {
                Debug.LogWarning("WebView is already open. Close it first before opening a new one.");
                return false;
            }

            // Apply current settings before opening
            ApplySettings();

            bool success = false;

            #if UNITY_IOS && !UNITY_EDITOR
            success = _balancyOpenWebView(url);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            success = _balancyOpenWebView(url);
            #elif UNITY_EDITOR
            Debug.Log($"[BalancyWebView] Would open URL in WebView: {url}");
            success = true;
            #else
            Debug.LogWarning("BalancyWebView is not supported on this platform.");
            success = false;
            #endif

            _isWebViewOpen = success;
            return success;
        }

        /// <summary>
        /// Closes the currently open WebView
        /// </summary>
        public void CloseWebView()
        {
            if (!_isWebViewOpen)
            {
                return;
            }

            #if UNITY_IOS && !UNITY_EDITOR
            _balancyCloseWebView();
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            _balancyCloseWebView();
            #elif UNITY_EDITOR
            Debug.Log("[BalancyWebView] Would close WebView");
            #endif

            _isWebViewOpen = false;
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Sends a message to the WebView
        /// </summary>
        /// <param name="message">The message to send (can be a string or JSON)</param>
        /// <returns>True if the message was sent successfully, false otherwise</returns>
        public bool SendMessage(string message)
        {
            if (!_isWebViewOpen)
            {
                Debug.LogWarning("Cannot send message: WebView is not open.");
                return false;
            }

            bool success = false;

            #if UNITY_IOS && !UNITY_EDITOR
            success = _balancySendMessage(message);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            success = _balancySendMessage(message);
            #elif UNITY_EDITOR
            Debug.Log($"[BalancyWebView] Would send message to WebView: {message}");
            success = true;
            #endif

            return success;
        }

        /// <summary>
        /// Calls a JavaScript function in the WebView
        /// </summary>
        /// <param name="functionName">The name of the JavaScript function to call</param>
        /// <param name="args">Arguments to pass to the function</param>
        /// <returns>The result of the JavaScript function call as a string</returns>
        public string CallJavaScript(string functionName, params string[] args)
        {
            if (!_isWebViewOpen)
            {
                Debug.LogWarning("Cannot call JavaScript: WebView is not open.");
                return null;
            }

            string result = null;

            #if UNITY_IOS && !UNITY_EDITOR
            result = _balancyCallJavaScript(functionName, args, args.Length);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            result = _balancyCallJavaScript(functionName, args, args.Length);
            #elif UNITY_EDITOR
            Debug.Log($"[BalancyWebView] Would call JavaScript function: {functionName}");
            result = "{}"; // Mock result in editor
            #endif

            return result;
        }

        /// <summary>
        /// Sets the position and size of the WebView
        /// </summary>
        /// <param name="x">X position (0-1, percentage of screen width from left)</param>
        /// <param name="y">Y position (0-1, percentage of screen height from top)</param>
        /// <param name="width">Width (0-1, percentage of screen width)</param>
        /// <param name="height">Height (0-1, percentage of screen height)</param>
        public void SetViewportRect(float x, float y, float width, float height)
        {
            // Clamp values to valid range (0-1)
            _viewportX = Mathf.Clamp01(x);
            _viewportY = Mathf.Clamp01(y);
            _viewportWidth = Mathf.Clamp01(width);
            _viewportHeight = Mathf.Clamp01(height);

            if (_isWebViewOpen)
            {
                ApplyViewportSettings();
            }
        }

        /// <summary>
        /// Sets the WebView to full screen mode
        /// </summary>
        /// <param name="fullScreen">True for full screen, false for current viewport settings</param>
        public void SetFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                SetViewportRect(0f, 0f, 1f, 1f);
            }
        }

        /// <summary>
        /// Enables or disables transparent background for the WebView
        /// </summary>
        /// <param name="transparent">True for transparent background, false for opaque</param>
        public void SetTransparentBackground(bool transparent)
        {
            _transparentBackground = transparent;

            if (_isWebViewOpen)
            {
                ApplyTransparencySettings();
            }
        }

        /// <summary>
        /// Enables or disables offline caching of web content
        /// </summary>
        /// <param name="enabled">True to enable offline caching, false to disable</param>
        public void SetOfflineCacheEnabled(bool enabled)
        {
            _offlineCacheEnabled = enabled;

            if (_isWebViewOpen)
            {
                ApplyCacheSettings();
            }
        }

        /// <summary>
        /// Enables or disables debug logging
        /// </summary>
        /// <param name="enabled">True to enable debug logging, false to disable</param>
        public void SetDebugLogging(bool enabled)
        {
            _debugLogging = enabled;

            #if UNITY_IOS && !UNITY_EDITOR
            _balancySetDebugLogging(enabled);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            _balancySetDebugLogging(enabled);
            #endif
        }

        /// <summary>
        /// Injects CSS into the WebView
        /// </summary>
        /// <param name="cssCode">The CSS code to inject</param>
        public void InjectCSS(string cssCode)
        {
            string script = $"(function() {{ " +
                $"var style = document.createElement('style'); " +
                $"style.type = 'text/css'; " +
                $"style.innerHTML = '{cssCode.Replace("'", "\\'")}'; " +
                $"document.head.appendChild(style); " +
                $"return true; " +
                $"}})();";

            CallJavaScript("eval", script);
        }

        /// <summary>
        /// Injects JavaScript into the WebView
        /// </summary>
        /// <param name="jsCode">The JavaScript code to inject</param>
        public void InjectJavaScript(string jsCode)
        {
            CallJavaScript("eval", jsCode);
        }

        /// <summary>
        /// Checks if a WebView is currently open
        /// </summary>
        /// <returns>True if a WebView is open, false otherwise</returns>
        public bool IsWebViewOpen()
        {
            return _isWebViewOpen;
        }

        #endregion

        #region Private Methods

        // Apply all current settings to the WebView
        private void ApplySettings()
        {
            ApplyViewportSettings();
            ApplyTransparencySettings();
            ApplyCacheSettings();
            
            #if UNITY_IOS && !UNITY_EDITOR || UNITY_STANDALONE_OSX && !UNITY_EDITOR
            _balancySetDebugLogging(_debugLogging);
            #endif
        }

        // Apply current viewport settings to the WebView
        private void ApplyViewportSettings()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _balancySetViewportRect(_viewportX, _viewportY, _viewportWidth, _viewportHeight);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            _balancySetViewportRect(_viewportX, _viewportY, _viewportWidth, _viewportHeight);
            #endif
        }

        // Apply current transparency settings to the WebView
        private void ApplyTransparencySettings()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _balancySetTransparentBackground(_transparentBackground);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            _balancySetTransparentBackground(_transparentBackground);
            #endif
        }

        // Apply current cache settings to the WebView
        private void ApplyCacheSettings()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _balancySetOfflineCacheEnabled(_offlineCacheEnabled);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            _balancySetOfflineCacheEnabled(_offlineCacheEnabled);
            #endif
        }

        #endregion

        #region Native Callback Methods

        // These methods are called from native code via UnitySendMessage

        /// <summary>
        /// Called from native code when a message is received from the WebView
        /// </summary>
        /// <param name="message">The message received from the WebView</param>
        private void OnMessageReceived(string message)
        {
            if (_debugLogging)
            {
                Debug.Log($"[BalancyWebView] Message received: {message}");
            }

            // Invoke the OnMessage event on the main thread
            MainThreadDispatcher.Instance.Enqueue(() => OnMessage?.Invoke(message));
        }

        /// <summary>
        /// Called from native code when the WebView finishes loading a page
        /// </summary>
        /// <param name="successStr">String "true" if loading was successful, "false" otherwise</param>
        private void OnLoadCompletedReceived(string successStr)
        {
            bool success = successStr.ToLower() == "true";

            if (_debugLogging)
            {
                Debug.Log($"[BalancyWebView] Load completed: {success}");
            }

            // Invoke the OnLoadCompleted event on the main thread
            MainThreadDispatcher.Instance.Enqueue(() => OnLoadCompleted?.Invoke(success));
        }

        /// <summary>
        /// Called from native code when offline caching is completed
        /// </summary>
        /// <param name="successStr">String "true" if caching was successful, "false" otherwise</param>
        private void OnCacheCompletedReceived(string successStr)
        {
            bool success = successStr.ToLower() == "true";

            if (_debugLogging)
            {
                Debug.Log($"[BalancyWebView] Cache completed: {success}");
            }

            // Invoke the OnCacheCompleted event on the main thread
            MainThreadDispatcher.Instance.Enqueue(() => OnCacheCompleted?.Invoke(success));
        }

        #endregion
    }

    /// <summary>
    /// Helper class for dispatching callbacks on the main thread
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;

        /// <summary>
        /// Singleton instance of the MainThreadDispatcher
        /// </summary>
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        /// <summary>
        /// Enqueues an action to be executed on the main thread
        /// </summary>
        /// <param name="action">The action to execute</param>
        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            // Execute all queued actions
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}