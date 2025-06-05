using UnityEngine;
using UnityEngine.UI;
using Balancy.WebView;

namespace Balancy.WebView
{
    /// <summary>
    /// Example component demonstrating how to use the embedded WebView functionality.
    /// This component can be attached to any GameObject with a Renderer to display
    /// WebView content on it.
    /// </summary>
    public class BalancyWebViewEmbeddedExample : MonoBehaviour
    {
        [Header("WebView Configuration")]
        [SerializeField] private string _url = "https://www.google.com";
        [SerializeField] private int _textureWidth = 1024;
        [SerializeField] private int _textureHeight = 768;
        [SerializeField] private bool _autoStart = true;
        
        [Header("UI Controls")]
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _popupModeButton;
        [SerializeField] private InputField _urlInput;
        [SerializeField] private Text _statusText;
        
        [Header("Mode Switching")]
        [SerializeField] private bool _useEmbeddedMode = true;
        
        // Components
        private BalancyWebViewEmbedded _embeddedWebView;
        private BalancyWebView _webView;
        
        #region Unity Lifecycle
        
        private void Start()
        {
            SetupUI();
            
            // Get or add the embedded component
            _embeddedWebView = GetComponent<BalancyWebViewEmbedded>();
            if (_embeddedWebView == null)
            {
                _embeddedWebView = gameObject.AddComponent<BalancyWebViewEmbedded>();
            }
            
            // Get the main WebView singleton
            _webView = BalancyWebView.Instance;
            
            // Setup events
            SetupWebViewEvents();
            
            if (_autoStart && _useEmbeddedMode)
            {
                LoadEmbeddedWebView();
            }
            
            UpdateStatus("Ready");
        }
        
        #endregion
        
        #region UI Setup
        
        private void SetupUI()
        {
            // Setup button events
            if (_loadButton != null)
            {
                _loadButton.onClick.AddListener(() => {
                    if (_useEmbeddedMode)
                        LoadEmbeddedWebView();
                    else
                        LoadPopupWebView();
                });
            }
            
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(CloseWebView);
            }
            
            if (_popupModeButton != null)
            {
                _popupModeButton.onClick.AddListener(ToggleMode);
                UpdateModeButtonText();
            }
            
            // Setup URL input
            if (_urlInput != null)
            {
                _urlInput.text = _url;
                _urlInput.onEndEdit.AddListener((value) => {
                    _url = value;
                });
            }
        }
        
        private void UpdateModeButtonText()
        {
            if (_popupModeButton != null)
            {
                Text buttonText = _popupModeButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = _useEmbeddedMode ? "Switch to Popup" : "Switch to Embedded";
                }
            }
        }
        
        private void UpdateStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = $"Status: {status} | Mode: {(_useEmbeddedMode ? "Embedded" : "Popup")}";
            }
            
            Debug.Log($"[WebViewExample] {status}");
        }
        
        #endregion
        
        #region WebView Control
        
        private void LoadEmbeddedWebView()
        {
            if (_embeddedWebView == null)
            {
                UpdateStatus("Error: Embedded WebView component not found");
                return;
            }
            
            UpdateStatus("Loading embedded WebView...");
            _embeddedWebView.InitializeEmbeddedWebView(_url);
        }
        
        private void LoadPopupWebView()
        {
            if (_webView == null)
            {
                UpdateStatus("Error: WebView instance not found");
                return;
            }
            
            UpdateStatus("Loading popup WebView...");
            _webView.OpenWebView(_url, "{}");
        }
        
        private void CloseWebView()
        {
            if (_useEmbeddedMode && _embeddedWebView != null)
            {
                _embeddedWebView.CloseEmbeddedWebView();
            }
            else if (_webView != null)
            {
                _webView.CloseWebView();
            }
            
            UpdateStatus("WebView closed");
        }
        
        private void ToggleMode()
        {
            // Close current WebView
            CloseWebView();
            
            // Switch mode
            _useEmbeddedMode = !_useEmbeddedMode;
            UpdateModeButtonText();
            
            UpdateStatus($"Switched to {(_useEmbeddedMode ? "Embedded" : "Popup")} mode");
        }
        
        #endregion
        
        #region WebView Events
        
        private void SetupWebViewEvents()
        {
            // Embedded WebView events
            if (_embeddedWebView != null)
            {
                _embeddedWebView.OnLoadCompleted += OnEmbeddedLoadCompleted;
                _embeddedWebView.OnMessage += OnEmbeddedMessage;
                _embeddedWebView.OnClosed += OnEmbeddedClosed;
            }
            
            // Popup WebView events
            if (_webView != null)
            {
                _webView.OnLoadCompleted += OnPopupLoadCompleted;
                _webView.OnMessage += OnPopupMessage;
                _webView.OnClosed += OnPopupClosed;
            }
        }
        
        private void OnEmbeddedLoadCompleted(bool success)
        {
            UpdateStatus(success ? "Embedded WebView loaded successfully" : "Embedded WebView failed to load");
        }
        
        private string OnEmbeddedMessage(string message)
        {
            Debug.Log($"[EmbeddedWebView] Received message: {message}");
            UpdateStatus($"Received embedded message: {message.Substring(0, Mathf.Min(50, message.Length))}...");
            
            // Echo the message back
            return $"{{\"echo\": \"{message}\", \"timestamp\": {System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
        }
        
        private void OnEmbeddedClosed()
        {
            UpdateStatus("Embedded WebView closed");
        }
        
        private void OnPopupLoadCompleted(bool success)
        {
            UpdateStatus(success ? "Popup WebView loaded successfully" : "Popup WebView failed to load");
        }
        
        private string OnPopupMessage(string message)
        {
            Debug.Log($"[PopupWebView] Received message: {message}");
            UpdateStatus($"Received popup message: {message.Substring(0, Mathf.Min(50, message.Length))}...");
            
            // Echo the message back
            return $"{{\"echo\": \"{message}\", \"timestamp\": {System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
        }
        
        private void OnPopupClosed()
        {
            UpdateStatus("Popup WebView closed");
        }
        
        #endregion
        
        #region Public Methods for Testing
        
        /// <summary>
        /// Send a test message to the WebView
        /// </summary>
        [ContextMenu("Send Test Message")]
        public void SendTestMessage()
        {
            string testMessage = $"{{\"type\": \"test\", \"data\": \"Hello from Unity!\", \"timestamp\": {System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
            
            if (_useEmbeddedMode && _embeddedWebView != null)
            {
                _embeddedWebView.SendMessageToWebView(testMessage);
            }
            else if (_webView != null)
            {
                _webView.SendMessageToWebView(testMessage);
            }
            
            UpdateStatus("Test message sent");
        }
        
        /// <summary>
        /// Navigate to a specific URL
        /// </summary>
        /// <param name="url">URL to navigate to</param>
        public void NavigateToUrl(string url)
        {
            _url = url;
            if (_urlInput != null)
            {
                _urlInput.text = url;
            }
            
            if (_useEmbeddedMode && _embeddedWebView != null)
            {
                _embeddedWebView.NavigateTo(url);
            }
            else if (_webView != null)
            {
                CloseWebView();
                LoadPopupWebView();
            }
        }
        
        /// <summary>
        /// Get current WebView mode
        /// </summary>
        /// <returns>True if embedded mode, false if popup mode</returns>
        public bool IsEmbeddedMode()
        {
            return _useEmbeddedMode;
        }
        
        /// <summary>
        /// Get the render texture from embedded WebView
        /// </summary>
        /// <returns>RenderTexture or null if not in embedded mode</returns>
        public RenderTexture GetRenderTexture()
        {
            if (_useEmbeddedMode && _embeddedWebView != null)
            {
                return _embeddedWebView.GetRenderTexture();
            }
            return null;
        }
        
        #endregion
        
        #region Unity Context Menu Methods
        
        [ContextMenu("Load Google")]
        private void LoadGoogle()
        {
            NavigateToUrl("https://www.google.com");
        }
        
        [ContextMenu("Load Unity Website")]
        private void LoadUnity()
        {
            NavigateToUrl("https://unity.com");
        }
        
        [ContextMenu("Toggle WebView Mode")]
        private void ToggleModeFromContext()
        {
            ToggleMode();
        }
        
        [ContextMenu("Reload Current Page")]
        private void ReloadPage()
        {
            if (_useEmbeddedMode && _embeddedWebView != null)
            {
                _embeddedWebView.Reload();
            }
            else
            {
                CloseWebView();
                if (!_useEmbeddedMode)
                {
                    LoadPopupWebView();
                }
            }
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnDestroy()
        {
            // Clean up events
            if (_embeddedWebView != null)
            {
                _embeddedWebView.OnLoadCompleted -= OnEmbeddedLoadCompleted;
                _embeddedWebView.OnMessage -= OnEmbeddedMessage;
                _embeddedWebView.OnClosed -= OnEmbeddedClosed;
            }
            
            if (_webView != null)
            {
                _webView.OnLoadCompleted -= OnPopupLoadCompleted;
                _webView.OnMessage -= OnPopupMessage;
                _webView.OnClosed -= OnPopupClosed;
            }
            
            // Clean up UI events
            if (_loadButton != null)
                _loadButton.onClick.RemoveAllListeners();
            if (_closeButton != null)
                _closeButton.onClick.RemoveAllListeners();
            if (_popupModeButton != null)
                _popupModeButton.onClick.RemoveAllListeners();
            if (_urlInput != null)
                _urlInput.onEndEdit.RemoveAllListeners();
        }
        
        #endregion
    }
}
