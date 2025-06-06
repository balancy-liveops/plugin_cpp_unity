using System;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.WebView
{
    /// <summary>
    /// Component for embedding WebView content into a Unity RenderTexture.
    /// This component works in Unity Editor on macOS by rendering the WebView content
    /// to a texture that can be displayed on any UI element or 3D object.
    /// </summary>
    public class BalancyWebViewEmbedded : MonoBehaviour
    {
        [Header("WebView Settings")]
        [SerializeField] private string _url = "https://example.com";
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private bool _interactable = true;
        [SerializeField] private int _maxTextureSize = 1000; // Maximum dimension (width or height)
        
        [Header("Debug")]
        [SerializeField] private bool _debugLogging = false;
        
        // Events
        public event Action<bool> OnLoadCompleted;
        public event Func<string, string> OnMessage;
        public event Action OnClosed;
        
        // Private fields
        private RenderTexture _renderTexture;
        private RawImage _renderer;
        private RectTransform _rectTransform;
        private bool _isInitialized = false;
        private bool _isLoading = false;
        private Camera _webViewCamera;
        private GameObject _webViewPlane;
        private Texture2D _textureBuffer;
        private byte[] _pixelBuffer;
        private byte[] _flippedPixelBuffer; // Buffer for vertically flipped pixels
        
        // Current texture dimensions (calculated from RectTransform)
        private int _currentTextureWidth;
        private int _currentTextureHeight;
        private Vector2 _lastRectSize; // Track RectTransform size changes
        
        // WebView instance reference
        private BalancyWebView _webView;

        #region Unity Lifecycle

        private void Awake()
        {
            _renderer = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();
            
            // Get or create WebView instance
            _webView = BalancyWebView.Instance;
            
            // Calculate initial texture size from RectTransform
            CalculateTextureSizeFromRect();
        }

        private void Start()
        {
            if (_autoStart)
            {
                InitializeEmbeddedWebView();
            }
        }

        private void OnDestroy()
        {
            CloseEmbeddedWebView();
        }

        private void Update()
        {
            // Check if RectTransform size has changed
            CheckForSizeChanges();
            
            // Handle mouse input if interactable
            if (_interactable && _isInitialized && Input.GetMouseButtonDown(0))
            {
                HandleMouseInput();
            }
            
            // Update texture with pixel data from native side
            #if UNITY_EDITOR_OSX
            if (_isInitialized && _webView != null && _webView.IsWebViewEmbedded())
            {
                // Add a frame counter to reduce log spam
                if (Time.frameCount % 30 == 0) // Log every 30 frames (~1 second at 30fps)
                {
                    LogDebug($"Update: Calling UpdateTextureFromNative, frame {Time.frameCount}");
                }
                UpdateTextureFromNative();
            }
            else if (_isInitialized)
            {
                if (Time.frameCount % 60 == 0) // Log every 60 frames
                {
                    LogDebug($"Update: WebView not embedded. IsInitialized: {_isInitialized}, WebView: {_webView != null}, IsEmbedded: {_webView?.IsWebViewEmbedded()}");
                }
            }
            #endif
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the embedded WebView with the specified URL
        /// </summary>
        /// <param name="url">URL to load in the WebView</param>
        public void InitializeEmbeddedWebView(string url = null)
        {
            if (_isInitialized)
            {
                LogDebug("WebView already initialized");
                return;
            }

            if (!string.IsNullOrEmpty(url))
            {
                _url = url;
            }

            LogDebug($"Initializing embedded WebView with URL: {_url}");

            try
            {
                CreateRenderTexture();
                SetupWebViewEvents();
                
                // Start loading the WebView in embedded mode
                _isLoading = true;
                _webView.LoadEmbedded(_url, _renderTexture, string.Empty);
                
                _isInitialized = true;
                LogDebug("Embedded WebView initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize embedded WebView: {ex.Message}");
            }
        }

        /// <summary>
        /// Close the embedded WebView
        /// </summary>
        public void CloseEmbeddedWebView()
        {
            if (!_isInitialized) return;

            LogDebug("Closing embedded WebView");

            CleanupWebViewEvents();
            
            if (_webView != null)
            {
                _webView.CloseEmbedded();
            }
            
            CleanupRenderTexture();
            
            _isInitialized = false;
            _isLoading = false;
            
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Send a message to the embedded WebView
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>True if message was sent successfully</returns>
        public bool SendMessageToWebView(string message)
        {
            if (!_isInitialized || _webView == null)
            {
                LogDebug("Cannot send message: WebView not initialized");
                return false;
            }

            return _webView.SendMessageToWebView(message);
        }

        /// <summary>
        /// Reload the current page
        /// </summary>
        public void Reload()
        {
            if (!_isInitialized) return;

            LogDebug("Reloading embedded WebView");
            CloseEmbeddedWebView();
            InitializeEmbeddedWebView();
        }

        /// <summary>
        /// Navigate to a new URL
        /// </summary>
        /// <param name="url">New URL to navigate to</param>
        public void NavigateTo(string url)
        {
            _url = url;
            Reload();
        }

        /// <summary>
        /// Get the current render texture
        /// </summary>
        /// <returns>The RenderTexture displaying WebView content</returns>
        public RenderTexture GetRenderTexture()
        {
            return _renderTexture;
        }

        /// <summary>
        /// Set the maximum texture size constraint
        /// </summary>
        /// <param name="maxSize">Maximum width or height dimension</param>
        public void SetMaxTextureSize(int maxSize)
        {
            _maxTextureSize = maxSize;
            CalculateTextureSizeFromRect();
            
            if (_isInitialized)
            {
                CreateRenderTexture();
                // Update the native WebView with new texture
                if (_webView != null)
                {
                    _webView.UpdateEmbeddedTexture(_renderTexture);
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculate optimal texture size from RectTransform with size constraints
        /// </summary>
        private void CalculateTextureSizeFromRect()
        {
            if (_rectTransform == null) return;
            
            // Get the rect size in pixels
            Vector2 rectSize = _rectTransform.rect.size;
            
            // Convert to integer dimensions
            int width = Mathf.RoundToInt(Mathf.Abs(rectSize.x));
            int height = Mathf.RoundToInt(Mathf.Abs(rectSize.y));
            
            // Ensure minimum size
            width = Mathf.Max(width, 64);
            height = Mathf.Max(height, 64);
            
            // Apply size constraint: if min(width, height) > maxTextureSize, scale both proportionally
            int minDimension = Mathf.Min(width, height);
            if (minDimension > _maxTextureSize)
            {
                float scale = (float)_maxTextureSize / minDimension;
                width = Mathf.RoundToInt(width * scale);
                height = Mathf.RoundToInt(height * scale);
                
                LogDebug($"Scaled texture size from ({Mathf.RoundToInt(rectSize.x)}, {Mathf.RoundToInt(rectSize.y)}) to ({width}, {height}) due to max size constraint ({_maxTextureSize})");
            }
            
            _currentTextureWidth = width;
            _currentTextureHeight = height;
            _lastRectSize = rectSize;
            
            LogDebug($"Calculated texture size: {_currentTextureWidth}x{_currentTextureHeight} from RectTransform: {rectSize}");
        }
        
        /// <summary>
        /// Check if RectTransform size has changed and update texture accordingly
        /// </summary>
        private void CheckForSizeChanges()
        {
            if (_rectTransform == null) return;
            
            Vector2 currentRectSize = _rectTransform.rect.size;
            
            // Check if size changed significantly (more than 1 pixel difference)
            if (Vector2.Distance(currentRectSize, _lastRectSize) > 1f)
            {
                LogDebug($"RectTransform size changed from {_lastRectSize} to {currentRectSize}");
                
                int oldWidth = _currentTextureWidth;
                int oldHeight = _currentTextureHeight;
                
                CalculateTextureSizeFromRect();
                
                // If texture dimensions actually changed, recreate texture
                if (_currentTextureWidth != oldWidth || _currentTextureHeight != oldHeight)
                {
                    LogDebug($"Texture size changed from {oldWidth}x{oldHeight} to {_currentTextureWidth}x{_currentTextureHeight}, recreating...");
                    
                    if (_isInitialized)
                    {
                        CreateRenderTexture();
                        
                        // Update the native WebView with new texture size
                        if (_webView != null)
                        {
                            _webView.UpdateEmbeddedTexture(_renderTexture);
                        }
                    }
                }
            }
        }

        private void CreateRenderTexture()
        {
            // Clean up existing texture
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                DestroyImmediate(_renderTexture);
            }
            
            if (_textureBuffer != null)
            {
                DestroyImmediate(_textureBuffer);
            }

            // Create new render texture using calculated dimensions
            _renderTexture = new RenderTexture(_currentTextureWidth, _currentTextureHeight, 24, RenderTextureFormat.ARGB32);
            _renderTexture.name = "WebView_RenderTexture";
            _renderTexture.Create();
            
            // Create texture buffer for pixel data transfer
            _textureBuffer = new Texture2D(_currentTextureWidth, _currentTextureHeight, TextureFormat.RGBA32, false);
            _textureBuffer.name = "WebView_TextureBuffer";
            
            // Allocate pixel buffers
            _pixelBuffer = new byte[_currentTextureWidth * _currentTextureHeight * 4]; // RGBA
            _flippedPixelBuffer = new byte[_currentTextureWidth * _currentTextureHeight * 4]; // RGBA for flipped data

            // Apply to renderer
            if (_renderer != null)
            {
                _renderer.texture = _textureBuffer;
            }

            LogDebug($"Created RenderTexture: {_currentTextureWidth}x{_currentTextureHeight}");
        }

        private void CleanupRenderTexture()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }
        }

        private void SetupWebViewEvents()
        {
            if (_webView == null) return;

            _webView.OnLoadCompleted += OnWebViewLoadCompleted;
            _webView.OnMessage += OnWebViewMessage;
            _webView.OnClosed += OnWebViewClosed;
        }

        private void CleanupWebViewEvents()
        {
            if (_webView == null) return;

            _webView.OnLoadCompleted -= OnWebViewLoadCompleted;
            _webView.OnMessage -= OnWebViewMessage;
            _webView.OnClosed -= OnWebViewClosed;
        }

        private void OnWebViewLoadCompleted(bool success)
        {
            _isLoading = false;
            LogDebug($"WebView load completed: {success}");
            OnLoadCompleted?.Invoke(success);
        }

        private string OnWebViewMessage(string message)
        {
            LogDebug($"Received message from WebView: {message}");
            return OnMessage?.Invoke(message);
        }

        private void OnWebViewClosed()
        {
            LogDebug("WebView closed");
            _isInitialized = false;
            _isLoading = false;
            OnClosed?.Invoke();
        }

        private void HandleMouseInput()
        {
            // Convert mouse position to UV coordinates relative to this object
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject)
            {
                // Convert hit point to UV coordinates
                Vector2 uv = hit.textureCoord;
                
                // Convert UV to pixel coordinates
                int pixelX = Mathf.RoundToInt(uv.x * _currentTextureWidth);
                int pixelY = Mathf.RoundToInt((1f - uv.y) * _currentTextureHeight); // Flip Y coordinate

                LogDebug($"Mouse click at UV: {uv}, Pixel: ({pixelX}, {pixelY})");

                // Send mouse event to WebView
                if (_webView != null)
                {
                    _webView.SendMouseEvent(pixelX, pixelY, true);
                }
            }
        }

        private void LogDebug(string message)
        {
            if (_debugLogging)
            {
                Debug.Log($"[BalancyWebViewEmbedded] {message}");
            }
        }
        
        #if UNITY_EDITOR_OSX
        [System.Runtime.InteropServices.DllImport("libBalancyWebViewMac")]
        private static extern bool _balancyGetEmbeddedPixelData(System.IntPtr buffer, int bufferSize);
        
        private void UpdateTextureFromNative()
        {
            if (_pixelBuffer == null || _flippedPixelBuffer == null || _textureBuffer == null) 
            {
                LogDebug("UpdateTextureFromNative: pixel buffer or texture buffer is null");
                return;
            }
            
            // Get pixel data from native code
            unsafe
            {
                fixed (byte* ptr = _pixelBuffer)
                {
                    System.IntPtr bufferPtr = new System.IntPtr(ptr);
                    bool success = _balancyGetEmbeddedPixelData(bufferPtr, _pixelBuffer.Length);
                    
                    if (success)
                    {
                        // Vertically flip the pixel data before loading into texture
                        FlipPixelDataVertically(_pixelBuffer, _flippedPixelBuffer, _currentTextureWidth, _currentTextureHeight);
                        
                        // Load flipped pixel data into texture
                        _textureBuffer.LoadRawTextureData(_flippedPixelBuffer);
                        _textureBuffer.Apply();
                        
                        // Only log success occasionally to reduce spam
                        if (Time.frameCount % 120 == 0) // Log every 120 frames (~4 seconds at 30fps)
                        {
                            LogDebug("UpdateTextureFromNative: Texture updated successfully (periodic log)");
                        }
                    }
                    // else
                    // {
                    //     LogDebug("UpdateTextureFromNative: Failed to get pixel data from native");
                    // }
                }
            }
        }
        
        /// <summary>
        /// Flip pixel data vertically to correct for coordinate system differences
        /// </summary>
        /// <param name="source">Source pixel buffer (RGBA format)</param>
        /// <param name="destination">Destination buffer for flipped pixels</param>
        /// <param name="width">Image width in pixels</param>
        /// <param name="height">Image height in pixels</param>
        private void FlipPixelDataVertically(byte[] source, byte[] destination, int width, int height)
        {
            int bytesPerPixel = 4; // RGBA
            int rowSize = width * bytesPerPixel;
            
            for (int y = 0; y < height; y++)
            {
                int sourceRowStart = y * rowSize;
                int destRowStart = (height - 1 - y) * rowSize;
                
                // Copy entire row
                Array.Copy(source, sourceRowStart, destination, destRowStart, rowSize);
            }
        }
        #endif

        #endregion

        #region Inspector Methods

        [ContextMenu("Initialize WebView")]
        private void InitializeFromContext()
        {
            InitializeEmbeddedWebView();
        }

        [ContextMenu("Close WebView")]
        private void CloseFromContext()
        {
            CloseEmbeddedWebView();
        }

        [ContextMenu("Reload WebView")]
        private void ReloadFromContext()
        {
            Reload();
        }

        #endregion
    }
}
