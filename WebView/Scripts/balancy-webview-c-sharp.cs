using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy.WebView
{
    /// <summary>
    /// Main interface for the Balancy WebView plugin.
    /// Provides methods to open, close, and interact with a WebView overlay.
    /// 
    /// Example usage for animation settings:
    /// 
    /// // Set 200ms delay and 300ms fade-in animation
    /// BalancyWebView.Instance.SetShowDelay(0.2f);
    /// BalancyWebView.Instance.SetAnimationDuration(0.3f);
    /// 
    /// // Open WebView - it will be invisible for 200ms, then fade in over 300ms
    /// BalancyWebView.Instance.OpenWebView("https://example.com", ownerJson, additionalInfo);
    /// </summary>
    public class BalancyWebView : MonoBehaviour
    {
        private string _scriptsCode = "";

        /// <summary>
        /// Store compiled scripts code for injection into WebView.
        /// Called from RenderViewsManager after compiling scripts via native layer.
        /// </summary>
        public void SetScriptsCode(string scriptsCode)
        {
            _scriptsCode = scriptsCode ?? "";
        }

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
                        GameObject go = new GameObject("BalancyView");
                        go.hideFlags = HideFlags.HideInHierarchy;
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

        #region Android Unity Messaging Methods
        
        // These methods are called from Android Java via UnitySendMessage
        // Format: UnitySendMessage("BalancyView", "OnAndroidMessageReceived", message)
        
        /// <summary>
        /// Called from Android Java when a message is received from WebView
        /// </summary>
        /// <param name="message">Message from WebView</param>
        public void OnAndroidMessageReceived(string message)
        {
            // Debug.Log($"[BalancyWebView] Android Unity Message Received: {message.Substring(0, Math.Min(100, message.Length))}...");
            OnMessageReceivedPrivate(message);
        }
        
        /// <summary>
        /// Called from Android Java when page load is completed
        /// </summary>
        /// <param name="successString">"true" or "false" as string</param>
        public void OnAndroidLoadCompleted(string successString)
        {
            bool success = successString.ToLower() == "true";
            
            // Always log this important event
            Debug.Log($"[BalancyWebView] Android Load completed: {success}");

            OnLoadCompletedReceived(success);
        }
        
        /// <summary>
        /// Called from Android Java when cache operation is completed
        /// </summary>
        /// <param name="successString">"true" or "false" as string</param>
        public void OnAndroidCacheCompleted(string successString)
        {
            bool success = successString.ToLower() == "true";
            LogDebug($"Android Unity Cache Completed: {success}");
            OnCacheCompleted?.Invoke(success);
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

        #region WebGL Unity Messaging Methods

        // These methods are called from JavaScript via SendMessage
        // Format: SendMessage("BalancyView", "OnWebGLMessageReceived", message)

        #if UNITY_WEBGL && !UNITY_EDITOR

        /// <summary>
        /// Called from JavaScript when a message is received from WebView
        /// </summary>
        /// <param name="message">Message from WebView</param>
        public void OnWebGLMessageReceived(string message)
        {
            //LogDebug($"WebGL Unity Message Received: {message.Substring(0, Math.Min(100, message.Length))}...");
            OnMessageReceivedPrivate(message);
        }

        /// <summary>
        /// Called from JavaScript when page load is completed
        /// </summary>
        /// <param name="successString">"true" or "false" as string</param>
        public void OnWebGLLoadCompleted(string successString)
        {
            bool success = successString.ToLower() == "true";
            Debug.Log($"[BalancyWebView] WebGL Load completed: {success}");
            OnLoadCompletedReceived(success);
        }

        /// <summary>
        /// Called from JavaScript when WebView is closed
        /// </summary>
        public void OnWebGLClosed(string ignored)
        {
            LogDebug("WebGL WebView closed");
            _isWebViewOpen = false;
            OnClosed?.Invoke();
        }

        #endif

        #endregion

        #region Events

        /// <summary>
        /// Event triggered when a message is received from the WebView
        /// </summary>
        public Action<string> OnMessage;

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
        private string _additionalInfo = string.Empty;
        private string _lastUrl = string.Empty;
        private float _showDelay = 0.1f; // Default 100ms delay
        private float _animationDuration = 0.1f; // Default 100ms animation duration

        // Persistent WebView mode state
        private bool _isPersistentMode = false;
        private bool _isPreparing = false;
        private Action _onShellReady = null;
        private Action _onViewReady = null;
        private string _currentViewId = null;
        #if UNITY_EDITOR_OSX
        private RenderTexture _embeddedTexture = null;
        #endif
        
        #endregion

        #region Native Plugin Interface
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MessageDelegate(string message);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LoadCompletedDelegate(bool success);

        // Platform-specific native method declarations
        #if UNITY_IOS && !UNITY_EDITOR
        
        [DllImport("__Internal")]
        private static extern bool _balancyOpenWebViewWithSize(string url, int width, int height);
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
        [DllImport("__Internal")]
        private static extern void _balancySetShowDelay(float delaySeconds);
        [DllImport("__Internal")]
        private static extern void _balancySetAnimationDuration(float durationSeconds);
        [DllImport("__Internal")]
        private static extern bool _balancyPrepareWebView(string shellUrl);
        [DllImport("__Internal")]
        private static extern void _balancyShowWebView();
        [DllImport("__Internal")]
        private static extern void _balancyHideWebView();
        private static extern void _balancySetEmergencyExitEnabled(bool enabled);

        #elif UNITY_WEBGL && !UNITY_EDITOR

        // WebGL implementation using JavaScript plugin (.jslib)
        [DllImport("__Internal")]
        private static extern bool _balancyOpenWebView(string url, string ownerJson, string additionalInfo, int width, int height);

        [DllImport("__Internal")]
        private static extern void _balancyCloseWebView();

        [DllImport("__Internal")]
        private static extern bool _balancySendMessage(string message);

        [DllImport("__Internal")]
        private static extern bool _balancyInjectCode(string code);

        [DllImport("__Internal")]
        private static extern void _balancySetViewportRect(float x, float y, float width, float height);

        [DllImport("__Internal")]
        private static extern void _balancySetTransparentBackground(bool transparent);

        [DllImport("__Internal")]
        private static extern void _balancySetGameUIMode(bool enabled);

        [DllImport("__Internal")]
        private static extern bool _balancyIsWebViewOpen();

        [DllImport("__Internal")]
        private static extern bool _balancyOpenWebViewWithHtmlContent(string htmlContent, string ownerJson, string additionalInfo, string manifestJson);

        // WebGL unified interface wrapper (instance method to access instance fields)
        private bool _balancyOpenWebViewWithSize(string url, int width, int height)
        {
            return _balancyOpenWebView(url, _ownerJson ?? "{}", _additionalInfo ?? "{}", width, height);
        }

        // WebGL wrapper for opening with HTML content (instance method to match pattern)
        private bool _balancyOpenWebViewWithHtml(string htmlContent, string ownerJson, string additionalInfo, string manifestJson)
        {
            return _balancyOpenWebViewWithHtmlContent(htmlContent, ownerJson, additionalInfo, manifestJson);
        }

        // WebGL wrapper for code injection (static to match other platforms)
        private static bool _balancyInjectJSCode(string code)
        {
            return _balancyInjectCode(code);
        }

        // Stub methods for WebGL (not implemented in .jslib yet)
        private static string _balancyCallJavaScript(string function, string[] args, int argsCount)
        {
            Debug.LogWarning("[BalancyWebView WebGL] CallJavaScript not implemented for WebGL");
            return null;
        }

        private static void _balancySetDebugLogging(bool enabled)
        {
            // Debug logging is always on for WebGL (console.log)
        }

        private static void _balancySetOfflineCacheEnabled(bool enabled)
        {
            Debug.LogWarning("[BalancyWebView WebGL] Offline cache not implemented for WebGL");
        }

        private static void _balancySetShowDelay(float delaySeconds)
        {
            Debug.LogWarning("[BalancyWebView WebGL] Show delay not implemented for WebGL");
        }

        private static void _balancySetAnimationDuration(float durationSeconds)
        {
            Debug.LogWarning("[BalancyWebView WebGL] Animation duration not implemented for WebGL");
        }

        private static bool _balancyPrepareWebView(string shellUrl)
        {
            Debug.LogWarning("[BalancyWebView WebGL] PrepareWebView not yet implemented for WebGL");
            return false;
        }

        private static void _balancyShowWebView()
        {
            Debug.LogWarning("[BalancyWebView WebGL] ShowWebView not yet implemented for WebGL");
        }

        private static void _balancyHideWebView()
        {
            Debug.LogWarning("[BalancyWebView WebGL] HideWebView not yet implemented for WebGL");
        }

        private static void _balancySetEmergencyExitEnabled(bool enabled)
        {
            // No-op for WebGL - no emergency exit button in browser
        }

        #elif UNITY_ANDROID && !UNITY_EDITOR
        
        // Android implementation using AndroidJavaObject
        private static AndroidJavaObject s_pluginInstance;
        
        private static AndroidJavaObject GetPluginInstance()
        {
            if (s_pluginInstance == null)
            {
                using (AndroidJavaClass pluginClass = new AndroidJavaClass("com.balancy.webview.BalancyWebViewPlugin"))
                {
                    s_pluginInstance = pluginClass.CallStatic<AndroidJavaObject>("getInstance");
                }
            }
            return s_pluginInstance;
        }
        
        // Android native methods - unified interface
        private static bool _balancyOpenWebViewWithSize(string url, int width, int height)
        {
            try
            {
                var plugin = GetPluginInstance();
                return plugin.Call<bool>("openWebView", url, _instance._ownerJson, width, height);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancyOpenWebViewWithSize failed: {e.Message}");
                return false;
            }
        }
        
        private static void _balancyCloseWebView()
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("closeWebView");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancyCloseWebView failed: {e.Message}");
            }
        }
        
        private static bool _balancySendMessage(string message)
        {
            try
            {
                var plugin = GetPluginInstance();
                return plugin.Call<bool>("sendMessage", message);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySendMessage failed: {e.Message}");
                return false;
            }
        }
        
        private static string _balancyCallJavaScript(string function, string[] args, int argsCount)
        {
            try
            {
                // For Android, simplified JS approach
                if (function == "eval" && args.Length > 0)
                {
                    Debug.Log($"Android JavaScript eval: {args[0]}");
                    return "{\"success\": true}";
                }
                return "{\"success\": true}";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancyCallJavaScript failed: {e.Message}");
                return "{\"error\": \"" + e.Message + "\"}";
            }
        }
        
        private static void _balancySetViewportRect(float x, float y, float width, float height)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setViewportRect", x, y, width, height);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetViewportRect failed: {e.Message}");
            }
        }
        
        private static void _balancySetTransparentBackground(bool transparent)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setTransparentBackground", transparent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetTransparentBackground failed: {e.Message}");
            }
        }
        
        private static void _balancySetOfflineCacheEnabled(bool enabled)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setOfflineCacheEnabled", enabled);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetOfflineCacheEnabled failed: {e.Message}");
            }
        }
        
        private static void _balancySetDebugLogging(bool enabled)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setDebugLogging", enabled);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetDebugLogging failed: {e.Message}");
            }
        }
        
        private static void _balancySetGameUIMode(bool enabled)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setGameUIMode", enabled);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetGameUIMode failed: {e.Message}");
            }
        }
        
        private static bool _balancyInjectJSCode(string code)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("injectJavaScript", code);
                Debug.Log($"Android _balancyInjectJSCode: injected {code.Length} characters");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancyInjectJSCode failed: {e.Message}");
                return false;
            }
        }
        
        // Android animation methods
        private static void _balancySetShowDelay(float delaySeconds)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setShowDelay", delaySeconds);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetShowDelay failed: {e.Message}");
            }
        }

        private static void _balancySetAnimationDuration(float durationSeconds)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setAnimationDuration", durationSeconds);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetAnimationDuration failed: {e.Message}");
            }
        }
        
        // Android emergency exit method
        private static void _balancySetEmergencyExitEnabled(bool enabled)
        {
            try
            {
                var plugin = GetPluginInstance();
                plugin.Call("setEmergencyExitEnabled", enabled);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Android _balancySetEmergencyExitEnabled failed: {e.Message}");
            }
        }
        
        // Android persistent-mode stubs (not yet implemented)
        private static bool _balancyPrepareWebView(string shellUrl)
        {
            Debug.LogWarning("[BalancyWebView Android] PrepareWebView not yet implemented for Android");
            return false;
        }

        private static void _balancyShowWebView()
        {
            Debug.LogWarning("[BalancyWebView Android] ShowWebView not yet implemented for Android");
        }

        private static void _balancyHideWebView()
        {
            Debug.LogWarning("[BalancyWebView Android] HideWebView not yet implemented for Android");
        }

        // Android callbacks - NO-OP implementations (using Unity messaging instead)
        private static void _balancyRegisterMessageCallback(MessageDelegate callback)
        {
            // NO-OP for Android - using Unity messaging instead
            Debug.Log("Android: Callback registration skipped - using Unity messaging");
        }
        
        private static void _balancyRegisterLoadCompletedCallback(LoadCompletedDelegate callback)
        {
            // NO-OP for Android - using Unity messaging instead
            Debug.Log("Android: Callback registration skipped - using Unity messaging");
        }
        
        private static void _balancyRegisterCacheCompletedCallback(LoadCompletedDelegate callback)
        {
            // NO-OP for Android - using Unity messaging instead
            Debug.Log("Android: Callback registration skipped - using Unity messaging");
        }
        
        #else
        
        // macOS/Editor implementation
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyOpenWebViewWithSize(string url, int width, int height);
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
        private static extern void _balancySetGameUIMode(bool enabled);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyRegisterMessageCallback(MessageDelegate callback);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyRegisterLoadCompletedCallback(LoadCompletedDelegate callback);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetShowDelay(float delaySeconds);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetAnimationDuration(float durationSeconds);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetEmergencyExitEnabled(bool enabled);

        // Embedding-specific methods (macOS only)
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyOpenWebViewEmbedded(string url, int width, int height);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyCloseWebViewEmbedded();
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyUpdateEmbeddedTexture(int width, int height);
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyGetEmbeddedPixelData(System.IntPtr buffer, int bufferSize);
        
        #if UNITY_EDITOR_OSX
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancySendMouseEvent(int x, int y, string eventType);
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancySendScrollEvent(int x, int y, float deltaX, float deltaY);
        #endif
        
        // Web Inspector functionality (macOS only)
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancySetWebInspectorEnabled(bool enabled);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyShowWebInspector();

        // Persistent WebView mode (macOS)
        [DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyPrepareWebView(string shellUrl);
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyShowWebView();
        [DllImport("libBalancyWebViewMac")]
        private static extern void _balancyHideWebView();

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
            SetWebInspectorEnabled(true);
            DontDestroyOnLoad(gameObject);

            // Initialize platform-specific plugin
#if !UNITY_EDITOR
#if UNITY_ANDROID
            // For Android, initialize via AndroidJavaObject
            try 
            {
                var plugin = GetPluginInstance();
                plugin.Call("initialize");
                LogDebug("Android WebView plugin initialized via AndroidJavaObject");
                
                // For Android, we DON'T register JNI callbacks - using Unity messaging instead
                LogDebug("Android: Using Unity messaging for callbacks (no JNI callback registration)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize Android WebView plugin: {e.Message}");
            }
#endif
#if UNITY_IOS
            _balancyRegisterCacheCompletedCallback(OnCacheCompletedReceived);
#endif
#endif
            
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            _balancyRegisterMessageCallback(OnMessageReceived);
            _balancyRegisterLoadCompletedCallback(OnLoadCompletedReceived);
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
        public bool OpenWebView(string url, string ownerJson, string additionalInfo)
        {
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL))
            // Use Screen dimensions to match game view size
            return OpenWebView(url, ownerJson, additionalInfo, Screen.width, Screen.height);
#endif
            Debug.LogWarning("Embedded WebView is only supported in Unity Editor on macOS, iOS, Android, and WebGL");

            return false;
        }
        
        /// <summary>
        /// Validates a local file URL before attempting to load it
        /// </summary>
        /// <param name="url">The file URL to validate</param>
        /// <returns>True if the file exists and can be accessed, false otherwise</returns>
        public bool ValidateLocalFile(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("file://"))
            {
                return true; // Not a local file, let WebView handle it
            }

            // android_asset URLs are served directly from the APK and can't be validated via File.Exists
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (url.StartsWith("file:///android_asset/"))
            {
                return true;
            }
            #endif

            string filePath = url.Substring(7); // Remove "file://" prefix

            // On Android, convert Unity path format if needed
            #if UNITY_ANDROID && !UNITY_EDITOR
            // Make sure we're using the correct path format
            if (filePath.Contains(Application.persistentDataPath))
            {
                LogDebug($"File is in persistent data path: {filePath}");
            }
            #endif
            
            bool fileExists = System.IO.File.Exists(filePath);
            LogDebug($"File validation - Path: {filePath}, Exists: {fileExists}");
            
            if (fileExists)
            {
                try
                {
                    var fileInfo = new System.IO.FileInfo(filePath);
                    LogDebug($"File info - Size: {fileInfo.Length} bytes, ReadOnly: {fileInfo.IsReadOnly}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error getting file info: {e.Message}");
                    return false;
                }
            }
            else
            {
                Debug.LogError($"Local file does not exist: {filePath}");
                
                // List files in the directory for debugging
                try
                {
                    string directory = System.IO.Path.GetDirectoryName(filePath);
                    if (System.IO.Directory.Exists(directory))
                    {
                        var files = System.IO.Directory.GetFiles(directory);
                        LogDebug($"Files in directory {directory}: {string.Join(", ", files)}");
                    }
                    else
                    {
                        Debug.LogError($"Directory does not exist: {directory}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error listing directory: {e.Message}");
                }
            }
            
            return fileExists;
        }

        /// <summary>
        /// Opens a WebView with the specified URL and custom size
        /// </summary>
        /// <param name="url">The URL to open in the WebView</param>
        /// <param name="ownerJson">Owner JSON data</param>
        /// <param name="width">Width of the WebView window</param>
        /// <param name="height">Height of the WebView window</param>
        /// <returns>True if the WebView was opened successfully, false otherwise</returns>
        public bool OpenWebView(string url, string ownerJson, string additionalInfo, int width, int height)
        {
            if (_isWebViewOpen)
            {
                Debug.LogWarning("WebView is already open. Close it first before opening a new one.");
                return false;
            }
            
            // Validate local files before attempting to open
            if (!ValidateLocalFile(url))
            {
                Debug.LogError($"Cannot open WebView: Local file validation failed for URL: {url}");
                return false;
            }

            _lastUrl = url;
            _ownerJson = ownerJson;
            _additionalInfo = additionalInfo;
            
            LogDebug($"Opening WebView with URL: {url}");
            LogDebug($"Screen dimensions: {width}x{height}");
            LogDebug($"Persistent data path: {Application.persistentDataPath}");
            
            // Apply current settings before opening
            ApplySettings();
            
            // Set transparent background by default
            SetTransparentBackground(true);
            
            // Enable game UI mode by default
            SetGameUIMode(_gameUIMode);
            
            // Only enable debug logging if it was explicitly requested
            SetDebugLogging(_debugLogging);

            // Unified call - platform-specific implementation handled in native layer
            bool success = _balancyOpenWebViewWithSize(url, width, height);

            _isWebViewOpen = success;
            return success;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Opens the WebView with HTML content instead of a URL (WebGL only)
        /// </summary>
        /// <param name="htmlContent">The HTML content to display</param>
        /// <param name="ownerJson">Owner JSON data</param>
        /// <param name="additionalInfo">Additional info JSON</param>
        /// <param name="manifestJson">Manifest JSON</param>
        /// <returns>True if the WebView was opened successfully, false otherwise</returns>
        public bool OpenWebViewHtml(string htmlContent, string ownerJson, string additionalInfo, string manifestJson)
        {
            if (_isWebViewOpen)
            {
                Debug.LogWarning("WebView is already open. Close it first before opening a new one.");
                return false;
            }

            _lastUrl = ""; // No URL for HTML content
            _ownerJson = ownerJson;
            _additionalInfo = additionalInfo;

            LogDebug($"Opening WebView with HTML content: {htmlContent.Length} bytes");
            LogDebug($"Owner JSON length: {ownerJson.Length}");
            LogDebug($"Manifest JSON length: {manifestJson.Length}");

            // Apply current settings before opening
            ApplySettings();

            // Set transparent background by default
            SetTransparentBackground(true);

            // Enable game UI mode by default
            SetGameUIMode(_gameUIMode);

            // Only enable debug logging if it was explicitly requested
            SetDebugLogging(_debugLogging);

            // Call JavaScript function to open WebView with HTML content
            bool success = _balancyOpenWebViewWithHtml(htmlContent, ownerJson, additionalInfo, manifestJson);

            _isWebViewOpen = success;
            return success;
        }
#endif

        /// <summary>
        /// Persistent WebView mode: creates the WebView and loads the shell page once.
        /// The bridge and script class declarations are injected after the shell page loads.
        /// Subsequent views are loaded via ShowView() without recreating the WebView.
        /// </summary>
        /// <param name="onReady">Callback fired when the shell page and bridge are ready.</param>
        public void PrepareWebView(Action onReady = null)
        {
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID))
            if (_isPersistentMode || _isPreparing)
            {
                Debug.LogWarning("[BalancyWebView] PrepareWebView already called.");
                return;
            }

            _isPreparing = true;
            _onShellReady = onReady;

            string shellPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Balancy", "balancy-shell.html");
            string shellUrl = "file://" + shellPath;

            ApplySettings();
            SetTransparentBackground(true);
            SetGameUIMode(_gameUIMode);
            SetDebugLogging(_debugLogging);

            _balancyPrepareWebView(shellUrl);
#else
            Debug.LogWarning("[BalancyWebView] PrepareWebView is not yet supported on this platform.");
            onReady?.Invoke();
#endif
        }

        /// <summary>
        /// Persistent-mode: inject a view's HTML into the shell page and show the WebView.
        /// Requires PrepareWebView() to have completed first.
        /// The bridge dispatches loadView to JS; the onViewReady callback fires when the view is rendered.
        /// </summary>
        public void ShowView(string html, string ownerJson, string additionalInfo, Action onViewReady = null)
        {
            if (!_isPersistentMode)
            {
                Debug.LogError("[BalancyWebView] ShowView requires PrepareWebView() to complete first.");
                return;
            }

            _ownerJson = ownerJson;
            _additionalInfo = additionalInfo;
            _onViewReady = onViewReady;
            _currentViewId = System.Guid.NewGuid().ToString("N");

            _balancyShowWebView();
            _isWebViewOpen = true;

            // HTML is base64-encoded to safely pass through the native string-injection layer
            string htmlBase64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
            string message = $"{{\"type\":\"loadView\",\"viewId\":\"{_currentViewId}\",\"htmlBase64\":\"{htmlBase64}\"}}";
            _balancySendMessage(message);
        }

        /// <summary>
        /// Persistent-mode: hide the WebView window without destroying it.
        /// Content and caches remain in memory for the next ShowView() call.
        /// </summary>
        public void HideWebView()
        {
            if (!_isPersistentMode || !_isWebViewOpen)
                return;

            _balancyHideWebView();
            _isWebViewOpen = false;
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Persistent-mode: destroy script components in the current view, clear the DOM,
        /// and hide the WebView. The WebView stays alive for the next ShowView() call.
        /// </summary>
        public void CloseView()
        {
            if (!_isPersistentMode)
            {
                CloseWebView();
                return;
            }

            if (!_isWebViewOpen)
                return;

            if (_currentViewId != null)
            {
                string message = $"{{\"type\":\"clearView\",\"viewId\":\"{_currentViewId}\"}}";
                _balancySendMessage(message);
                _currentViewId = null;
            }

            _balancyHideWebView();
            _isWebViewOpen = false;
            OnClosed?.Invoke();
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

            // Unified call - platform-specific implementation handled in native layer
            _balancyCloseWebView();

            _isWebViewOpen = false;
            
            // Reset debug logging state to prevent log accumulation
            _debugLogging = false;
            
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Sends a message to the WebView
        /// </summary>
        /// <param name="message">The message to send (can be a string or JSON)</param>
        /// <returns>True if the message was sent successfully, false otherwise</returns>
        public bool SendMessageToWebView(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            
            if (!_isWebViewOpen && !_isWebViewEmbedded)
            {
                // Silently return false without logging - this is normal during WebView closure
                return false;
            }

            // Unified call - platform-specific implementation handled in native layer
            return _balancySendMessage(message);
        }

        /// <summary>
        /// Calls a JavaScript function in the WebView
        /// </summary>
        /// <param name="functionName">The name of the JavaScript function to call</param>
        /// <param name="args">Arguments to pass to the function</param>
        /// <returns>The result of the JavaScript function call as a string</returns>
        public string CallJavaScript(string functionName, params string[] args)
        {
            if (!_isWebViewOpen && !_isWebViewEmbedded)
            {
                // Silently return null without logging - this is normal during WebView closure
                return null;
            }

            // Unified call - platform-specific implementation handled in native layer
            return _balancyCallJavaScript(functionName, args, args.Length);
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
                // Unified call - platform-specific implementation handled in native layer
                _balancySetGameUIMode(enabled);
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
            
            // Unified call - platform-specific implementation handled in native layer
            _balancySetDebugLogging(enabled);
        }

        /// <summary>
        /// Sets the delay before showing the WebView after it has loaded
        /// </summary>
        /// <param name="delaySeconds">Delay in seconds (default: 0.1)</param>
        public void SetShowDelay(float delaySeconds)
        {
            _showDelay = Mathf.Max(0f, delaySeconds);
            
            if (_debugLogging)
            {
                Debug.Log($"[BalancyWebView] Show delay set to: {_showDelay:F3} seconds");
            }
            
            // Apply immediately if WebView is open
            if (_isWebViewOpen || _isWebViewEmbedded)
            {
                _balancySetShowDelay(_showDelay);
            }
        }

        /// <summary>
        /// Sets the duration of the fade-in animation when showing the WebView
        /// </summary>
        /// <param name="durationSeconds">Animation duration in seconds (default: 0.1)</param>
        public void SetAnimationDuration(float durationSeconds)
        {
            _animationDuration = Mathf.Max(0f, durationSeconds);
            
            if (_debugLogging)
            {
                Debug.Log($"[BalancyWebView] Animation duration set to: {_animationDuration:F3} seconds");
            }
            
            // Apply immediately if WebView is open
            if (_isWebViewOpen || _isWebViewEmbedded)
            {
                _balancySetAnimationDuration(_animationDuration);
            }
        }

        /// <summary>
        /// Gets the current show delay setting
        /// </summary>
        /// <returns>Show delay in seconds</returns>
        public float GetShowDelay()
        {
            return _showDelay;
        }

        /// <summary>
        /// Gets the current animation duration setting
        /// </summary>
        /// <returns>Animation duration in seconds</returns>
        public float GetAnimationDuration()
        {
            return _animationDuration;
        }

        /// <summary>
        /// Enables or disables the emergency exit button (triple-tap to close).
        /// When disabled, triple-tap will not show the close button.
        /// </summary>
        /// <param name="enabled">True to enable emergency exit (default), false to disable</param>
        public void SetEmergencyExitEnabled(bool enabled)
        {
            _balancySetEmergencyExitEnabled(enabled);
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
        public bool LoadEmbedded(string url, RenderTexture renderTexture, string ownerJson, string additionalInfo)
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

            _lastUrl = url;
            _ownerJson = ownerJson;
            _additionalInfo = additionalInfo;
            _embeddedTexture = renderTexture;
            
            // Apply current settings
            ApplySettings();
            SetDebugLogging(_debugLogging);
            
            // OPTIMIZATION: No longer pass texturePtr - native code manages its own pixel buffer
            bool success = _balancyOpenWebViewEmbedded(url, renderTexture.width, renderTexture.height);
            _isWebViewEmbedded = success;
            
            if (success)
            {
                Debug.Log($"[BalancyWebView] OPTIMIZED embedded View opened successfully: {renderTexture.width}x{renderTexture.height}");
            }
            
            return success;
            #else
            Debug.LogWarning("Embedded WebView is only supported in Unity Editor on macOS r on device");
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
            
            // Reset debug logging state to prevent log accumulation
            _debugLogging = false;
            
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
        /// Enables or disables the Web Inspector for debugging (macOS only)
        /// When enabled, right-click in the WebView will show a context menu with "Inspect Element"
        /// </summary>
        /// <param name="enabled">True to enable Web Inspector, false to disable</param>
        public void SetWebInspectorEnabled(bool enabled)
        {
            #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _balancySetWebInspectorEnabled(enabled);
            Debug.Log($"[BalancyWebView] Web Inspector {(enabled ? "enabled" : "disabled")}");
            #else
            Debug.LogWarning("Web Inspector is only supported on macOS");
            #endif
        }
        
        /// <summary>
        /// Programmatically shows the Web Inspector window (macOS only)
        /// </summary>
        public static void ShowWebInspector()
        {
            #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _balancyShowWebInspector();
            Debug.Log("[BalancyWebView] Showing Web Inspector");
            #else
            Debug.LogWarning("Web Inspector is only supported on macOS");
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
            ApplyAnimationSettings();
            
            // Unified call - platform-specific implementation handled in native layer
            _balancySetDebugLogging(_debugLogging);
        }

        // Apply current viewport settings to the WebView
        private void ApplyViewportSettings()
        {
            // Unified call - platform-specific implementation handled in native layer
            _balancySetViewportRect(_viewportX, _viewportY, _viewportWidth, _viewportHeight);
        }

        // Apply current transparency settings to the WebView
        private void ApplyTransparencySettings()
        {
            // Unified call - platform-specific implementation handled in native layer
            _balancySetTransparentBackground(_transparentBackground);
        }

        // Apply current cache settings to the WebView
        private void ApplyCacheSettings()
        {
            // Unified call - platform-specific implementation handled in native layer
            _balancySetOfflineCacheEnabled(_offlineCacheEnabled);
        }
        
        // Apply current animation settings to the WebView
        private void ApplyAnimationSettings()
        {
            // Unified call - platform-specific implementation handled in native layer
            _balancySetShowDelay(_showDelay);
            _balancySetAnimationDuration(_animationDuration);
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
            // Intercept viewReady ACK from the bridge (persistent mode only)
            if (_isPersistentMode && message.Contains("\"viewReady\"") && _currentViewId != null
                && message.Contains(_currentViewId))
            {
                var callback = _onViewReady;
                _onViewReady = null;
                callback?.Invoke();
                return;
            }

            if (OnMessage != null)
            {
                try
                {
                    OnMessage.Invoke(message);
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
            if (success)
            {
                Debug.Log($"[BalancyWebView] Load completed: {success}");

                #if !(UNITY_WEBGL && !UNITY_EDITOR)
                // For WebGL, bridge injection is handled by TypeScript (unity-entry.ts)
                // to ensure proper timing with iframe load events

                string ownerInfo = "";
                if (!string.IsNullOrEmpty(_instance._ownerJson) && !_instance._isPreparing)
                {
                    // In persistent mode the owner is not known at shell-load time;
                    // it is set per-view via ShowView() using loadView message metadata.
                    ownerInfo = "(function() {\n" +
                                "    try {\n" +
                                (!string.IsNullOrEmpty(_instance._additionalInfo)
                                    ? $"        window.balancySettings = JSON.parse('{_instance._additionalInfo}');\n"
                                    : "") +
                                $"        window.balancyViewOwner = JSON.parse('{_instance._ownerJson}');\n" +
                                "    } catch (error) {\n" +
                                "        console.error('Error parsing owner JSON:', error);\n" +
                                "        window.balancyViewOwner = null;\n" +
                                "    }\n" +
                                "    return true;\n" +
                                "})();";
                }

                var bridgeCode = Resources.Load<TextAsset>("balancy-webview-bridge");
                var scriptsCode = _instance._scriptsCode;

                // Build full injection code with all 4 sections, wrapped in try/catch
                var fullCode =
                    "try {\n" +
                    // #1 Owner Info (empty string for shell page loads)
                    $"  {ownerInfo}\n" +
                    // #2 Bridge
                    $"  {bridgeCode}\n" +
                    // #3 Scripts
                    $"  {scriptsCode}\n" +
                    // #4 Initialize
                    "  window.balancy.initResponseHandler();\n" +
                    "  console.log('Balancy fully initialized!');\n" +
                    "} catch (error) {\n" +
                    "  console.error('***Balancy Injection Error***', error);\n" +
                    "  console.error('Error message:', error.message);\n" +
                    "  console.error('Error stack:', error.stack);\n" +
                    "}\n" +
                    "true;";

                _balancyInjectJSCode(fullCode);
                #else
                Debug.Log("[BalancyWebView] WebGL: Bridge injection handled by TypeScript");
                #endif
            }

            // Persistent mode shell page: switch to persistent mode, fire shell ready callback
            if (_instance._isPreparing)
            {
                _instance._isPreparing = false;
                if (success)
                    _instance._isPersistentMode = true;

                var shellReady = _instance._onShellReady;
                _instance._onShellReady = null;
                shellReady?.Invoke();
                return; // Do NOT fire OnLoadCompleted for shell page loads
            }

            _instance.OnLoadCompleted?.Invoke(success);
        }

        private static void InjectFileFromResources(string fileName)
        {
            var fileContent = Resources.Load<TextAsset>(fileName);
            if (fileContent)
                _balancyInjectJSCode(fileContent.text);
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