using System;
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
                    _instance = FindAnyObjectByType<BalancyWebView>();

                    // If not, create a new GameObject with the component
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("BalancyWebView");
                        go.hideFlags = HideFlags.HideAndDontSave;
                        _instance = go.AddComponent<BalancyWebView>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        private void OnApplicationQuit()
        {
            if (_instance)
            {
                if (Application.isPlaying)
                    Destroy(_instance.gameObject); // Destroy the game object
                else
                    DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }

        #endregion

        #region Native Logging Method
        
        /// <summary>
        /// Called from native code to log messages to Unity console
        /// This method is called via UnitySendMessage from the native plugin
        /// </summary>
        /// <param name="message">The log message from native code</param>
        public void LogFromNative(string message)
        {
            Debug.Log($"[BalancyWebView Native] {message}");
        }
        
        #endregion

        #region Events

        /// <summary>
        /// Event triggered when a message is received from the WebView
        /// </summary>
        public Func<string, string> OnMessage;

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

        private bool _gameUIMode = true;
        private bool _isWebViewOpen = false;
        private bool _isWebViewEmbedded = false;
        private bool _transparentBackground = false;
        private bool _offlineCacheEnabled = false;
        private float _viewportX = 0f;
        private float _viewportY = 0f;
        private float _viewportWidth = 1f;
        private float _viewportHeight = 1f;
        private bool _debugLogging = false;
        private string _ownerJson = string.Empty;
        private RenderTexture _embeddedTexture = null;
        
        #endregion

        #region Native Plugin Interface
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MessageDelegate(string message);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LoadCompletedDelegate(bool success);

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
        
        [DllImport("__Internal")]
        private static extern void _balancySetGameUIMode(bool enabled);
        
        [DllImport("__Internal")]
        private static extern void _balancyRegisterMessageCallback(MessageDelegate callback);
        
        [DllImport("__Internal")]
        private static extern void _balancyRegisterLoadCompletedCallback(LoadCompletedDelegate callback);
        
        [DllImport("__Internal")]
        private static extern void _balancyRegisterCacheCompletedCallback(LoadCompletedDelegate callback);
        
        [DllImport("__Internal")]
        private static extern bool _balancyInjectJSCode(string code);
        
        //#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        #else
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyOpenWebView(string url);

        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyCloseWebView();

        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancySendMessage(string message);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyInjectJSCode(string message);

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
        
        // Embedding-specific methods (macOS only) - OPTIMIZED VERSION
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyOpenWebViewEmbedded(string url, int width, int height);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyCloseWebViewEmbedded();
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyUpdateEmbeddedTexture(int width, int height);
        
#if UNITY_EDITOR_OSX
        [System.Runtime.InteropServices.DllImport("libBalancyWebViewMac")]
        private static extern bool _balancySendMouseEvent(int x, int y, string eventType);

        [System.Runtime.InteropServices.DllImport("libBalancyWebViewMac")]
        private static extern bool _balancySendScrollEvent(int x, int y, float deltaX, float deltaY);
#endif
        
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyGetEmbeddedPixelData(System.IntPtr buffer, int bufferSize);
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
            
            _balancyRegisterMessageCallback(OnMessageReceived);
            _balancyRegisterLoadCompletedCallback(OnLoadCompletedReceived);
            
            #if UNITY_IOS && !UNITY_EDITOR
            _balancyRegisterCacheCompletedCallback(OnCacheCompletedReceived);
            #endif
        }

        private void OnDestroy()
        {
            if (_isWebViewOpen)
            {
                CloseWebView();
            }
            
            if (_isWebViewEmbedded)
            {
                CloseEmbedded();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Opens a WebView with the specified URL
        /// </summary>
        /// <param name="url">The URL to open in the WebView</param>
        /// <returns>True if the WebView was opened successfully, false otherwise</returns>
        public bool OpenWebView(string url, string ownerJson)
        {
            if (_isWebViewOpen)
            {
                Debug.LogWarning("WebView is already open. Close it first before opening a new one.");
                return false;
            }

            _ownerJson = ownerJson;
            
            // Apply current settings before opening
            ApplySettings();
            
            // Set transparent background by default
            SetTransparentBackground(true);
            
            // Enable game UI mode by default
            SetGameUIMode(_gameUIMode);
            
            SetDebugLogging(true);

            bool success = false;

            #if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            success = _balancyOpenWebView(url);
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

            #if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            _balancyCloseWebView();
            #elif UNITY_EDITOR
            LogDebug("[BalancyWebView] Would close WebView");
            #endif

            _isWebViewOpen = false;
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Sends a message to the WebView
        /// </summary>
        /// <param name="message">The message to send (can be a string or JSON)</param>
        /// <returns>True if the message was sent successfully, false otherwise</returns>
        public bool SendMessageToWebView(string message)
        {
            if (!_isWebViewOpen)
            {
                Debug.LogWarning("Cannot send message: WebView is not open.");
                return false;
            }

            bool success = false;

            #if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            success = _balancySendMessage(message);
            #else
            LogDebug($"[BalancyWebView] Would send message to WebView: {message}");
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

            #if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            result = _balancyCallJavaScript(functionName, args, args.Length);
            #else
            LogDebug($"[BalancyWebView] Would call JavaScript function: {functionName}");
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
        /// Enables or disables game UI mode, which makes the WebView feel more like a part of your game
        /// by disabling browser features like text selection, scrolling, and context menus.
        /// </summary>
        /// <param name="enabled">True to enable game UI mode, false for standard web browsing mode</param>
        public void SetGameUIMode(bool enabled)
        {
            _gameUIMode = enabled;

            if (_isWebViewOpen)
            {
                #if UNITY_IOS && !UNITY_EDITOR
                _balancySetGameUIMode(enabled);
                #elif UNITY_EDITOR
                LogDebug($"[BalancyWebView] Game UI mode {(enabled ? "enabled" : "disabled")}");
                #endif
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
            _balancySetDebugLogging(enabled);
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
        
        /// <summary>
        /// Opens a WebView in embedded mode, rendering to a RenderTexture
        /// Available only in Unity Editor on macOS - OPTIMIZED VERSION
        /// </summary>
        /// <param name="url">The URL to open</param>
        /// <param name="renderTexture">The RenderTexture to render into</param>
        /// <param name="ownerJson">Owner JSON data</param>
        /// <returns>True if opened successfully</returns>
        public bool LoadEmbedded(string url, RenderTexture renderTexture, string ownerJson)
        {
            #if UNITY_EDITOR_OSX
            if (_isWebViewOpen || _isWebViewEmbedded)
            {
                Debug.LogWarning("WebView is already open. Close it first before opening a new one.");
                return false;
            }
            
            if (renderTexture == null)
            {
                Debug.LogError("RenderTexture cannot be null for embedded WebView");
                return false;
            }
            
            _ownerJson = ownerJson;
            _embeddedTexture = renderTexture;
            
            // Apply current settings
            ApplySettings();
            SetDebugLogging(true);
            
            // OPTIMIZATION: No longer pass texturePtr - native code manages its own pixel buffer
            bool success = _balancyOpenWebViewEmbedded(url, renderTexture.width, renderTexture.height);
            _isWebViewEmbedded = success;
            
            if (success)
            {
                Debug.Log($"[BalancyWebView] OPTIMIZED embedded View opened successfully: {renderTexture.width}x{renderTexture.height}");
            }
            
            return success;
            #else
            Debug.LogWarning("Embedded WebView is only supported in Unity Editor on macOS");
            return false;
            #endif
        }
        
        /// <summary>
        /// Closes the embedded WebView
        /// </summary>
        public void CloseEmbedded()
        {
            #if UNITY_EDITOR_OSX
            if (!_isWebViewEmbedded)
            {
                return;
            }
            
            _balancyCloseWebViewEmbedded();
            _isWebViewEmbedded = false;
            _embeddedTexture = null;
            OnClosed?.Invoke();
            #endif
        }
        
        /// <summary>
        /// Updates the texture used for embedded rendering - OPTIMIZED VERSION
        /// </summary>
        /// <param name="renderTexture">New RenderTexture to use</param>
        public void UpdateEmbeddedTexture(RenderTexture renderTexture)
        {
            #if UNITY_EDITOR_OSX
            if (!_isWebViewEmbedded || renderTexture == null)
            {
                return;
            }
            
            _embeddedTexture = renderTexture;
            // OPTIMIZATION: No longer pass texturePtr - native code manages its own pixel buffer
            _balancyUpdateEmbeddedTexture(renderTexture.width, renderTexture.height);
            
            Debug.Log($"[BalancyWebView] OPTIMIZED: Updated embedded texture size: {renderTexture.width}x{renderTexture.height}");
            #endif
        }
        
        /// <summary>
        /// Sends mouse event to the embedded WebView
        /// </summary>
        /// <param name="x">X coordinate in pixels</param>
        /// <param name="y">Y coordinate in pixels</param>
        /// <param name="eventType">"down", "up", "move"</param>
        public void SendMouseEvent(int x, int y, string eventType)
        {
            #if UNITY_EDITOR_OSX
            if (!_isWebViewEmbedded)
            {
                return;
            }
            
            _balancySendMouseEvent(x, y, eventType);
            #endif
        }
        
        public bool SendScrollEvent(int x, int y, float deltaX, float deltaY)
        {
#if UNITY_EDITOR_OSX
            return _balancySendScrollEvent(x, y, deltaX, deltaY);
#else
    return false;
#endif
        }
        
        /// <summary>
        /// Checks if a WebView is currently open in embedded mode
        /// </summary>
        /// <returns>True if an embedded WebView is open, false otherwise</returns>
        public bool IsWebViewEmbedded()
        {
            return _isWebViewEmbedded;
        }

        #endregion

        #region Private Methods

        // Apply all current settings to the WebView
        private void ApplySettings()
        {
            ApplyViewportSettings();
            ApplyTransparencySettings();
            ApplyCacheSettings();
            
            _balancySetDebugLogging(_debugLogging);
        }

        // Apply current viewport settings to the WebView
        private void ApplyViewportSettings()
        {
            _balancySetViewportRect(_viewportX, _viewportY, _viewportWidth, _viewportHeight);
        }

        // Apply current transparency settings to the WebView
        private void ApplyTransparencySettings()
        {
            _balancySetTransparentBackground(_transparentBackground);
        }

        // Apply current cache settings to the WebView
        private void ApplyCacheSettings()
        {
            _balancySetOfflineCacheEnabled(_offlineCacheEnabled);
        }
        
        // Log a debug message if debug logging is enabled
        private void LogDebug(string message)
        {
            if (_debugLogging)
            {
                Debug.Log(message);
            }
        }

        #endregion

        #region Native Callback Methods

        // These methods are called from native code via UnitySendMessage

        /// <summary>
        /// Called from native code when a message is received from the WebView
        /// </summary>
        /// <param name="message">The message received from the WebView</param>
        [AOT.MonoPInvokeCallback(typeof(MessageDelegate))]
        public static void OnMessageReceived(string message)
        {
            _instance.OnMessageReceivedPrivate(message);
        }
        
        private void OnMessageReceivedPrivate(string message)
        {
            if (OnMessage != null)
            {
                try
                {
                    string response = OnMessage.Invoke(message);
                    
                    // If a response is returned, send it back to the WebView
                    if (!string.IsNullOrEmpty(response))
                    {
                        SendMessageToWebView(response);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BalancyWebView] Error in OnMessage event: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called when the WebView finishes loading a page (editor version)
        /// </summary>
        /// <param name="success">True if loading was successful, false otherwise</param>
        [AOT.MonoPInvokeCallback(typeof(LoadCompletedDelegate))]
        private static void OnLoadCompletedReceived(bool success)
        {
            Debug.Log($"[BalancyWebView] Load completed: {success}");

            var bridge = Resources.Load<TextAsset>("balancy-webview-bridge");
            if (bridge)
                _balancyInjectJSCode(bridge.text);

            if (!string.IsNullOrEmpty(_instance._ownerJson))
            {
                //"balancy.owner =
                var injectedCode = "try {\n                " +
                                   $"balancy.owner = JSON.parse('{_instance._ownerJson}');\n" +
                                   "           } catch (error) {\n " +
                                   "               console.error('Error parsing button params JSON:', error);\n" +
                                   "               balancy.owner = null;\n" +
                                   "            }";

                _balancyInjectJSCode(injectedCode);
            }
            
            _instance.OnLoadCompleted?.Invoke(success);
        }
        
        /// <summary>
        /// Called when offline caching is completed
        /// </summary>
        /// <param name="success">True if caching was successful, false otherwise</param>
        [AOT.MonoPInvokeCallback(typeof(LoadCompletedDelegate))]
        private static void OnCacheCompletedReceived(bool success)
        {
            if (_instance._debugLogging)
            {
                Debug.Log($"[BalancyWebView] Cache completed: {success}");
            }
            
            _instance.OnCacheCompleted?.Invoke(success);
        }

        // Note: The previous string-based implementation has been replaced by the bool-based version above
        // that matches the method signature expected by the native code.

        #endregion
    }
}