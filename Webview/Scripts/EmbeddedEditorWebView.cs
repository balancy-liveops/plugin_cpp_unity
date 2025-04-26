using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.WebView
{
    public class EmbeddedEditorWebView : MonoBehaviour
    {
        [SerializeField] private RawImage webViewDisplay;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        
        private Texture2D webViewTexture;
        private IntPtr webViewPtr;
        private int textureWidth = 800;
        private int textureHeight = 600;
        private bool isInitialized = false;
        private Vector2 scrollPosition = Vector2.zero;
        private float scrollSpeed = 15f;
        private byte[] textureBuffer; // Added buffer to avoid marshaling issues
        
        // Events
        public event Action<string> OnMessage;
        public event Action<bool> OnLoadCompleted;
        public event Action<bool> OnCacheCompleted;
        
        #if UNITY_EDITOR_OSX
        // Import functions from the native plugin
        [DllImport("WebViewPlugin")]
        private static extern IntPtr _CreateOffscreenWebView(string url, int width, int height);
        
        [DllImport("WebViewPlugin")]
        private static extern void _CloseWebView(IntPtr webViewPtr);
        
        [DllImport("WebViewPlugin")]
        private static extern bool _LoadURL(IntPtr webViewPtr, string url);
        
        [DllImport("WebViewPlugin")]
        private static extern bool _ExecuteJavaScript(IntPtr webViewPtr, string script);
        
        [DllImport("WebViewPlugin")]
        private static extern void _GetTextureData(IntPtr webViewPtr, IntPtr buffer);
        
        [DllImport("WebViewPlugin")]
        private static extern void _SendMouseEvent(IntPtr webViewPtr, int eventType, float x, float y);
        
        [DllImport("WebViewPlugin")]
        private static extern void _SetJSMessageCallback(IntPtr webViewPtr, JSMessageCallback callback);
        
        [DllImport("WebViewPlugin")]
        private static extern bool _UpdateTexture(IntPtr webViewPtr);
        
        [DllImport("WebViewPlugin")]
        private static extern bool _HasContentChanged(IntPtr webViewPtr);
        
        // Callback delegate for JS messages
        private delegate void JSMessageCallback(string message);
        
        private JSMessageCallback jsMessageCallback;
        #endif
        
        public static EmbeddedEditorWebView Instance 
        {
            get 
            {
                if (s_instance == null)
                {
                    GameObject go = new GameObject("EmbeddedWebView");
                    s_instance = go.AddComponent<EmbeddedEditorWebView>();
                    DontDestroyOnLoad(go);
                }
                return s_instance;
            }
        }

        private static EmbeddedEditorWebView s_instance;
        
        private void Awake()
        {
            s_instance = this;
            #if UNITY_EDITOR_OSX
            // Create JS message callback
            jsMessageCallback = OnJSMessage;
            #endif
        }
        
        public bool Initialize(RawImage targetDisplay = null, int width = 1024, int height = 768)
        {
            if (isInitialized)
                return true;
                
            // Set or create display target
            if (targetDisplay != null)
                webViewDisplay = targetDisplay;
            else if (webViewDisplay == null)
            {
                GameObject displayObj = new GameObject("WebViewDisplay");
                displayObj.transform.SetParent(transform);
                
                RectTransform rect = displayObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                
                webViewDisplay = displayObj.AddComponent<RawImage>();
                
                aspectRatioFitter = displayObj.AddComponent<AspectRatioFitter>();
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
            
            // Set dimensions
            textureWidth = width;
            textureHeight = height;
            
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.aspectRatio = (float)width / height;
            }
                
            // Initialize texture buffer
            textureBuffer = new byte[width * height * 4]; // RGBA format
            
            // Create texture
            webViewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            if (webViewDisplay != null)
                webViewDisplay.texture = webViewTexture;
            
            #if UNITY_EDITOR_OSX
            try
            {
                // Create offscreen webview
                webViewPtr = _CreateOffscreenWebView("about:blank", width, height);
                
                if (webViewPtr != IntPtr.Zero)
                {
                    // Register for JS messages
                    _SetJSMessageCallback(webViewPtr, jsMessageCallback);
                    
                    isInitialized = true;
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError("Error initializing embedded WebView: " + e.Message);
                return false;
            }
            #else
            isInitialized = true;
            return true;
            #endif
        }
        
        public bool OpenWebView(string url, int width = 0, int height = 0)
        {
            if (!isInitialized)
                Initialize(null, width > 0 ? width : textureWidth, height > 0 ? height : textureHeight);
                
            // Configure RawImage and RectTransform
            if (webViewDisplay != null)
            {
                RectTransform rect = webViewDisplay.GetComponent<RectTransform>();
            }
                
            #if UNITY_EDITOR_OSX
            if (webViewPtr != IntPtr.Zero)
            {
                bool success = _LoadURL(webViewPtr, url);
                if (success)
                {
                    // Start updating texture
                    InvokeRepeating("UpdateWebViewTexture", 0.1f, 0.05f); // 20fps update
                    
                    // Inject JavaScript interface after a short delay
                    Invoke("InjectJavaScriptInterface", 0.5f);
                    
                    // Simulate load completed after a short delay
                    Invoke("SimulateLoadCompleted", 1.0f);
                }
                return success;
            }
            return false;
            #else
            return true;
            #endif
        }
        
        public void CloseWebView()
        {
            CancelInvoke("UpdateWebViewTexture");
            
            #if UNITY_EDITOR_OSX
            if (webViewPtr != IntPtr.Zero)
            {
                _CloseWebView(webViewPtr);
                webViewPtr = IntPtr.Zero;
            }
            #endif
            
            isInitialized = false;
        }
        
        public bool SendMessage(string message)
        {
            #if UNITY_EDITOR_OSX
            if (webViewPtr != IntPtr.Zero)
            {
                // Create a JavaScript call to dispatch an event
                string escapedMessage = message.Replace("\"", "\\\"").Replace("'", "\\'");
                string jsCode = 
                    "var event = new CustomEvent('balancyMessage', { detail: '" + escapedMessage + "' });" +
                    "document.dispatchEvent(event);";
                    
                return _ExecuteJavaScript(webViewPtr, jsCode);
            }
            #endif
            
            return false;
        }
        
        public void SetOfflineCacheEnabled(bool enabled)
        {
            // Caching is handled by the native WebKit
            OnCacheCompleted?.Invoke(true);
        }
        
        private void OnDestroy()
        {
            CloseWebView();
            
            if (webViewTexture != null)
            {
                Destroy(webViewTexture);
                webViewTexture = null;
            }
            
            textureBuffer = null;
        }
        
        #if UNITY_EDITOR_OSX
        private void OnJSMessage(string message)
        {
            // Direct dispatch instead of using the thread dispatcher to avoid crashes
            if (OnMessage != null)
            {
                try
                {
                    OnMessage.Invoke(message);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error dispatching WebView message: {ex.Message}");
                }
            }
        }
        #endif
        
        private void SimulateLoadCompleted()
        {
            if (OnLoadCompleted != null)
            {
                try
                {
                    OnLoadCompleted.Invoke(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in load completed callback: {ex.Message}");
                }
            }
        }
        
        private void UpdateWebViewTexture()
        {
            #if UNITY_EDITOR_OSX
            if (webViewPtr == IntPtr.Zero || webViewTexture == null)
                return;
                
            try
            {
                // Only update texture if content has changed
                if (_HasContentChanged(webViewPtr))
                {
                    // Get texture data into a managed buffer first
                    // This prevents potential memory issues between native and managed code
                    GCHandle bufferHandle = GCHandle.Alloc(textureBuffer, GCHandleType.Pinned);
                    
                    try
                    {
                        IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject();
                        _GetTextureData(webViewPtr, bufferPtr);
                        
                        // Now safely load the texture data from our managed buffer
                        webViewTexture.LoadRawTextureData(textureBuffer);
                        webViewTexture.Apply();
                    }
                    finally
                    {
                        // Always release the handle
                        bufferHandle.Free();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error updating WebView texture: {e.Message}");
                CancelInvoke("UpdateWebViewTexture"); // Stop updates if there's an error
            }
            #endif
        }
        
        private void Update()
        {
            if (!isInitialized)
                return;
                
            HandleInput();
            HandleScrolling();
        }
        
        private void HandleInput()
        {
            #if UNITY_EDITOR_OSX
            if (webViewPtr == IntPtr.Zero || webViewDisplay == null)
                return;
            
            try
            {
                // Check if mouse is over the webview
                RectTransform rect = webViewDisplay.GetComponent<RectTransform>();
                Vector2 localPoint;
                Camera eventCamera = null;
                Canvas canvas = webViewDisplay.canvas;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) {
                    eventCamera = canvas.worldCamera;
                }
                
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, eventCamera, out localPoint))
                {
                    // Convert local point to normalized coordinates (0-1 range)
                    Rect rectRect = rect.rect;
                    Vector2 normalized = new Vector2(
                        Mathf.InverseLerp(rectRect.xMin, rectRect.xMax, localPoint.x),
                        Mathf.InverseLerp(rectRect.yMax, rectRect.yMin, localPoint.y)  // Invert Y for WebView coordinates
                    );
                    
                    // Clamp within webview bounds
                    normalized.x = Mathf.Clamp01(normalized.x);
                    normalized.y = Mathf.Clamp01(normalized.y);
                    
                    // Calculate webview coordinates
                    float webX = normalized.x * textureWidth;
                    float webY = normalized.y * textureHeight;
                    
                    // Handle mouse movement
                    _SendMouseEvent(webViewPtr, 2, webX, webY);
                    
                    // Handle mouse clicks
                    if (Input.GetMouseButtonDown(0))
                    {
                        _SendMouseEvent(webViewPtr, 0, webX, webY);
                        
                        // Also use JavaScript to simulate the click at this position
                        string jsClickCode = $@"(function() {{
                            var element = document.elementFromPoint({webX}, {webY});
                            if (element) {{
                                var clickEvent = new MouseEvent('click', {{
                                    bubbles: true,
                                    cancelable: true,
                                    view: window,
                                    clientX: {webX},
                                    clientY: {webY}
                                }});
                                element.dispatchEvent(clickEvent);
                            }}
                        }})()";
                        
                        _ExecuteJavaScript(webViewPtr, jsClickCode);
                    }
                    else if (Input.GetMouseButtonUp(0))
                    {
                        _SendMouseEvent(webViewPtr, 1, webX, webY);
                        
                        // Additional mouseup event via JavaScript for better click handling
                        string jsMouseUpCode = $@"(function() {{
                            var element = document.elementFromPoint({webX}, {webY});
                            if (element) {{
                                var mouseUpEvent = new MouseEvent('mouseup', {{
                                    bubbles: true,
                                    cancelable: true,
                                    view: window,
                                    clientX: {webX},
                                    clientY: {webY}
                                }});
                                element.dispatchEvent(mouseUpEvent);
                            }}
                        }})()";
                        
                        _ExecuteJavaScript(webViewPtr, jsMouseUpCode);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling WebView input: {e.Message}");
            }
            #endif
        }
        
        private void HandleScrolling()
        {
            #if UNITY_EDITOR_OSX
            if (webViewPtr == IntPtr.Zero || webViewDisplay == null)
                return;
            
            try
            {    
                // Check if mouse is over the webview
                RectTransform rect = webViewDisplay.GetComponent<RectTransform>();
                Vector2 localPoint;
                
                Camera eventCamera = null;
                Canvas canvas = webViewDisplay.canvas;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) {
                    eventCamera = canvas.worldCamera;
                }
                
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, eventCamera, out localPoint))
                {
                    // Process mouse wheel for scrolling
                    float scrollDelta = Input.mouseScrollDelta.y;
                    if (scrollDelta != 0)
                    {
                        // Scroll the view
                        scrollPosition.y += scrollDelta * scrollSpeed;
                        
                        // Create a JavaScript scroll command
                        string jsCode = $"window.scrollTo(0, {-scrollPosition.y});";
                        _ExecuteJavaScript(webViewPtr, jsCode);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling WebView scrolling: {e.Message}");
            }
            #endif
        }
        
        public void InjectJavaScriptInterface()
        {
            #if UNITY_EDITOR_OSX
            if (webViewPtr == IntPtr.Zero)
                return;
            
            try
            {
                string jsInterface = @"
                window.BalancyWebView = {
                    postMessage: function(message) {
                        window.webkit.messageHandlers.balancyMessageHandler.postMessage(message);
                    },
                    
                    getVersion: function() {
                        return '1.0.0';
                    }
                };
                
                document.addEventListener('DOMContentLoaded', function() {
                    // BalancyWebView bridge initialized
                });";
                
                _ExecuteJavaScript(webViewPtr, jsInterface);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error injecting JavaScript interface: {e.Message}");
            }
            #endif
        }
    }
}