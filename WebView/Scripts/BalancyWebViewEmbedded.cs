#if UNITY_EDITOR
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
        private const int _maxTextureSize = 1000;
        private const bool _debugLogging = false;
        
        private RenderTexture _renderTexture;
        private RawImage _renderer;
        private RectTransform _rectTransform;
        private bool _isInitialized = false;
        private bool _isLoading = false;
        private Texture2D _textureBuffer;
        private byte[] _pixelBuffer;
        private byte[] _flippedPixelBuffer; // Buffer for vertically flipped pixels
        
        private int _currentTextureWidth;
        private int _currentTextureHeight;
        private Vector2 _lastRectSize; // Track RectTransform size changes
        
        private BalancyWebView _webView;

        #region Unity Lifecycle
        
        private static BalancyWebViewEmbedded _instance;

        /// <summary>
        /// Singleton instance of the BalancyWebView
        /// </summary>
        public static BalancyWebViewEmbedded Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (_instance == null)
                        _instance = CreateGameObject();
                }
                return _instance;
            }
        }
        
        private GameObject _parentGameObject;

        private static BalancyWebViewEmbedded CreateGameObject()
        {
            GameObject goCanvas = new GameObject("BalancyEmbeddedViewCanvas", typeof(RectTransform));
            var parentTrm = goCanvas.GetComponent<RectTransform>();
            parentTrm.localScale = Vector3.one;
            var canvas = goCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            goCanvas.AddComponent<GraphicRaycaster>();
            var scaler = goCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            if (Screen.width > Screen.height)
            {
                scaler.referenceResolution = new Vector2(2 * _maxTextureSize, _maxTextureSize);
                scaler.matchWidthOrHeight = 1;
            }
            else
            {
                scaler.referenceResolution = new Vector2(_maxTextureSize, 2 * _maxTextureSize);
                scaler.matchWidthOrHeight = 0;
            }
            GameObject go = new GameObject("BalancyEmbeddedView", typeof(RectTransform));
            var trm = go.GetComponent<RectTransform>();
            trm.SetParent(parentTrm);
            trm.localScale = Vector3.one;
            trm.anchorMin = Vector2.zero;
            trm.anchorMax = Vector2.one;
            trm.offsetMin = Vector2.zero;
            trm.offsetMax = Vector2.zero;
            go.AddComponent<RawImage>();
            var instance = go.AddComponent<BalancyWebViewEmbedded>();

            goCanvas.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(goCanvas);

            instance._parentGameObject = goCanvas;
            goCanvas.SetActive(false);
            return instance;
        } 

        private void Awake()
        {
            _instance = this;
            _renderer = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();
            
            _webView = BalancyWebView.Instance;
            
            CalculateTextureSizeFromRect();
        }
        
        // private void Start()
        // {
        //     InitializeEmbeddedWebView("file:///Users/pavelignatov/Library/Application Support/DefaultCompany/plugin_cpp_unity/Balancy/Models/67933064-2b8a-11f0-b3bd-1fec53a055ba_Cache/Files/644_1748945133983/index.html", null);
        // }

        private void OnDestroy()
        {
            CloseEmbeddedWebView();
        }

        private void Update()
        {
            CheckForSizeChanges();
            
            if (_isInitialized)
            {
                HandleMouseInput();
                HandleScrollInput();
            }
            
            // Update texture with pixel data from native side
            #if UNITY_EDITOR_OSX
            if (_isInitialized && _webView != null && _webView.IsWebViewEmbedded())
            {
                UpdateTextureFromNative();
            }
            #endif
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initialize the embedded WebView with the specified URL
        /// </summary>
        /// <param name="url">URL to load in the WebView</param>
        public bool InitializeEmbeddedWebView(string url, string ownerJson)
        {
            if (_isInitialized)
            {
                LogDebug("WebView already initialized");
                return false;
            }

            try
            {
                CreateRenderTexture();
                SetupWebViewEvents();
                
                // Start loading the WebView in embedded mode
                _isLoading = true;
                bool success = _webView.LoadEmbedded(url, _renderTexture, ownerJson);
                
                if (success)
                    _parentGameObject.SetActive(true);
                
                _isInitialized = true;
                LogDebug("Embedded View initialized successfully");
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize embedded WebView: {ex.Message}");
            }

            return false;
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
            
            _parentGameObject.SetActive(false);
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
            _webView.OnClosed += OnWebViewClosed;
        }

        private void CleanupWebViewEvents()
        {
            if (_webView == null) return;

            _webView.OnLoadCompleted -= OnWebViewLoadCompleted;
            _webView.OnClosed -= OnWebViewClosed;
        }

        private void OnWebViewLoadCompleted(bool success)
        {
            _isLoading = false;
            LogDebug($"WebView load completed: {success}");
        }

        private void OnWebViewClosed()
        {
            LogDebug("WebView closed");
            _isInitialized = false;
            _isLoading = false;
        }

        private void HandleMouseInput()
        {
            // Check if mouse is over the WebView area
            Vector2 localMousePosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform, 
                    Input.mousePosition, 
                    null, // For screen space overlay
                    out localMousePosition))
            {
                return; // Mouse not over WebView
            }
    
            // Convert local position to UV coordinates (0-1 range)
            Rect rect = _rectTransform.rect;
            Vector2 uv = new Vector2(
                (localMousePosition.x - rect.x) / rect.width,
                (localMousePosition.y - rect.y) / rect.height
            );
    
            // Check if UV is within bounds
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                return;
    
            // Convert UV to pixel coordinates
            int pixelX = Mathf.RoundToInt(uv.x * _currentTextureWidth);
            int pixelY = Mathf.RoundToInt((1f - uv.y) * _currentTextureHeight); // Flip Y coordinate
    
            // Handle mouse down
            if (Input.GetMouseButtonDown(0))
            {
                if (_webView != null)
                {
                    _webView.SendMouseEvent(pixelX, pixelY, "down");
                }
            }
    
            // Handle mouse up
            if (Input.GetMouseButtonUp(0))
            {
                if (_webView != null)
                {
                    _webView.SendMouseEvent(pixelX, pixelY, "up");
                }
            }
    
            // Handle mouse move (while button is held down for drag)
            if (Input.GetMouseButton(0))
            {
                if (_webView != null)
                {
                    _webView.SendMouseEvent(pixelX, pixelY, "move");
                }
            }
        }
        
        private void HandleScrollInput()
        {
            // Check if mouse is over the WebView area
            Vector2 localMousePosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform, 
                    Input.mousePosition, 
                    null,
                    out localMousePosition))
            {
                return; // Mouse not over WebView
            }
    
            // Get scroll delta
            Vector2 scrollDelta = Input.mouseScrollDelta;
            if (scrollDelta.magnitude > 0.01f)
            {
                // Convert local position to pixel coordinates for scroll position
                Rect rect = _rectTransform.rect;
                Vector2 uv = new Vector2(
                    (localMousePosition.x - rect.x) / rect.width,
                    (localMousePosition.y - rect.y) / rect.height
                );
        
                if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1)
                {
                    int pixelX = Mathf.RoundToInt(uv.x * _currentTextureWidth);
                    int pixelY = Mathf.RoundToInt((1f - uv.y) * _currentTextureHeight);
            
                    if (_webView != null)
                    {
                        _webView.SendScrollEvent(pixelX, pixelY, scrollDelta.x, scrollDelta.y);
                    }
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
                        FlipPixelDataVertically(_pixelBuffer, _flippedPixelBuffer, _currentTextureWidth, _currentTextureHeight);
                        
                        _textureBuffer.LoadRawTextureData(_flippedPixelBuffer);
                        _textureBuffer.Apply();
                    }
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
    }
}
#endif