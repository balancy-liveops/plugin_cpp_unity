using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;

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
        
        // Dictionary to hold request handlers
        private readonly Dictionary<string, Func<JObject, object>> _requestHandlers = new Dictionary<string, Func<JObject, object>>();
        
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
        private static extern void _balancySendResponse(string requestId, string resultJson, string errorMessage);
        //#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        #else
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
        private static extern void _balancySendResponse(string requestId, string resultJson, string errorMessage);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyRegisterMessageCallback(MessageDelegate callback);
        
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyRegisterLoadCompletedCallback(LoadCompletedDelegate callback);
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
            
            // Set up default request handlers
            SetupDefaultRequestHandlers();
            
            _balancyRegisterMessageCallback(OnMessageReceived);
            _balancyRegisterLoadCompletedCallback(OnLoadCompletedReceived);
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

            #if UNITY_IOS && !UNITY_EDITOR
            success = _balancySendMessage(message);
            #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            success = _balancySendMessage(message);
            #elif UNITY_EDITOR_OSX
            success = _balancySendMessage(message);
            // if (_editorRuntime != null)
            // {
            //     success = _editorRuntime.SendMessage(message);
            // }
            // else
            // {
            //     LogDebug($"[BalancyWebView] Would send message to WebView: {message}");
            //     success = true;
            // }
            #elif UNITY_EDITOR
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
        /// Registers a handler for a specific request action from the WebView
        /// </summary>
        /// <param name="action">The action name to handle</param>
        /// <param name="handler">Function that will process the request and return a response</param>
        public void RegisterRequestHandler(string action, Func<JObject, object> handler)
        {
            if (string.IsNullOrEmpty(action))
            {
                Debug.LogError("[BalancyWebView] Cannot register handler with null or empty action");
                return;
            }
            
            _requestHandlers[action] = handler;
            LogDebug($"[BalancyWebView] Registered handler for action: {action}");
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
        
        // Set up default request handlers
        private void SetupDefaultRequestHandlers()
        {
            // Echo handler - returns whatever is sent
            RegisterRequestHandler("echo", parameters => {
                return parameters;
            });
            
            // Time handler - returns current time
            RegisterRequestHandler("getTime", _ => {
                return new { 
                    time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
                };
            });
            
            // Unity info handler
            RegisterRequestHandler("getUnityInfo", _ => {
                return new {
                    version = Application.unityVersion,
                    platform = Application.platform.ToString(),
                    productName = Application.productName,
                    companyName = Application.companyName,
                    systemLanguage = Application.systemLanguage.ToString()
                };
            });
        }
        
        // Process a request from the WebView
        private void ProcessRequest(string requestId, string action, JObject parameters)
        {
            LogDebug($"[BalancyWebView] Processing request: {action} (ID: {requestId})");
            
            try
            {
                object result = null;
                string error = null;
                
                // Look for a handler for this action
                if (_requestHandlers.TryGetValue(action, out var handler))
                {
                    try
                    {
                        // Execute the handler
                        result = handler(parameters);
                    }
                    catch (Exception ex)
                    {
                        error = $"Handler exception: {ex.Message}";
                        Debug.LogException(ex);
                    }
                }
                else
                {
                    error = $"No handler registered for action: {action}";
                    Debug.LogWarning($"[BalancyWebView] {error}");
                }
                
                // Send the response
                SendResponse(requestId, result, error);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BalancyWebView] Error processing request: {ex.Message}");
                
                // Try to send error response
                SendResponse(requestId, null, $"Internal error: {ex.Message}");
            }
        }
        
        // Send a response to the WebView
        private void SendResponse(string requestId, object result, string error)
        {
            try
            {
                string resultJson = null;
                
                if (result != null)
                {
                    // Serialize the result to JSON
                    // resultJson = JsonConvert.SerializeObject(result);
                    //Create one object using requestId, result and error and then serialize it as string:
                    resultJson = JsonConvert.SerializeObject(new
                    {
                        type = "response",
                        id = requestId, 
                        result, 
                        error
                    });
                    
                }
                SendMessageToWebView(resultJson);
                
                LogDebug($"[BalancyWebView] Response sent for request {requestId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BalancyWebView] Error sending response: {ex.Message}");
            }
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
            LogDebug($"[BalancyWebView] Message received: {message}");
            
            try
            {
                // Try to parse as JSON to see if it's a request
                JObject msgObj = JObject.Parse(message);
                
                // Check if this is a request message (has type, id, and action)
                string messageType = msgObj["type"]?.ToString();
                string id = msgObj["id"]?.ToString();
                string action = msgObj["action"]?.ToString();
                
                if (messageType == "request" && !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(action))
                {
                    // This is a request message, process it
                    JObject parameters = msgObj["params"] as JObject ?? new JObject();
                    ProcessRequest(id, action, parameters);
                    return;
                }
            }
            catch (JsonException)
            {
                // Not a JSON message or invalid format, treat as regular message
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BalancyWebView] Error processing message: {ex.Message}");
            }

            OnMessage?.Invoke(message);
        }

        /// <summary>
        /// Called when the WebView finishes loading a page (editor version)
        /// </summary>
        /// <param name="success">True if loading was successful, false otherwise</param>
        [AOT.MonoPInvokeCallback(typeof(LoadCompletedDelegate))]
        private static void OnLoadCompletedReceived(bool success)
        {
            Debug.Log($"[BalancyWebView] Load completed: {success}");

            _instance.OnLoadCompleted?.Invoke(success);
        }

        /// <summary>
        /// Called from native code when offline caching is completed
        /// </summary>
        /// <param name="successStr">String "true" if caching was successful, "false" otherwise</param>
        private void OnCacheCompletedReceived(string successStr)
        {
            bool success = successStr.ToLower() == "true";

            LogDebug($"[BalancyWebView] Cache completed: {success}");

            OnCacheCompleted?.Invoke(success);
        }

        #endregion
    }
}