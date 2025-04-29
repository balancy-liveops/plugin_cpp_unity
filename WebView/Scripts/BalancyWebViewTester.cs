using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.WebView
{
    /// <summary>
    /// Component for testing WebView functionality directly in a scene
    /// </summary>
    public class BalancyWebViewTester : MonoBehaviour
    {
        [Header("WebView Settings")]
        [SerializeField] private bool useLocalHtml = true;
        [SerializeField] private string remoteUrl = "https://example.com";
        [SerializeField] private bool transparentBackground = false;
        [SerializeField] private bool fullScreen = true;
        
        [Header("Viewport Settings (if not fullscreen)")]
        [Range(0, 1)]
        [SerializeField] private float viewportX = 0.1f;
        [Range(0, 1)]
        [SerializeField] private float viewportY = 0.1f;
        [Range(0, 1)]
        [SerializeField] private float viewportWidth = 0.8f;
        [Range(0, 1)]
        [SerializeField] private float viewportHeight = 0.8f;
        
        [Header("UI References")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button sendMessageButton;
        [SerializeField] private InputField messageInput;
        [SerializeField] private Text statusText;
        [SerializeField] private Toggle transparentToggle;
        [SerializeField] private Toggle fullScreenToggle;
        
        [Header("Message Log")]
        [SerializeField] private Text messageLog;
        [SerializeField] private int maxLogLines = 20;
        
        // Internal state
        private BalancyWebView _webView;
        private List<string> _logLines = new List<string>();
        private string _testPagePath;
        
        private void Awake()
        {
            // Get WebView instance
            _webView = BalancyWebView.Instance;
            
            // Set up event handlers
            _webView.OnMessage += HandleWebViewMessage;
            _webView.OnLoadCompleted += HandleLoadCompleted;
            _webView.OnClosed += HandleClosed;
            
            // Set up UI button listeners
            if (openButton != null)
                openButton.onClick.AddListener(OpenWebView);
            
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseWebView);
            
            if (sendMessageButton != null)
                sendMessageButton.onClick.AddListener(SendMessage);
            
            if (transparentToggle != null)
            {
                transparentToggle.isOn = transparentBackground;
                transparentToggle.onValueChanged.AddListener(SetTransparentBackground);
            }
            
            if (fullScreenToggle != null)
            {
                fullScreenToggle.isOn = fullScreen;
                fullScreenToggle.onValueChanged.AddListener(SetFullScreen);
            }
            
            // Prepare test page if using local HTML
            if (useLocalHtml)
            {
                StartCoroutine(PrepareTestPage());
            }
            
            UpdateUI();
        }
        
        private void OnDestroy()
        {
            // Clean up event handlers
            if (_webView != null)
            {
                _webView.OnMessage -= HandleWebViewMessage;
                _webView.OnLoadCompleted -= HandleLoadCompleted;
                _webView.OnClosed -= HandleClosed;
                
                // Close WebView if it's open
                if (_webView.IsWebViewOpen())
                {
                    _webView.CloseWebView();
                }
            }
        }
        
        /// <summary>
        /// Prepare the test HTML page
        /// </summary>
        private IEnumerator PrepareTestPage()
        {
            // Load the test page from Resources
            TextAsset testPageAsset = Resources.Load<TextAsset>("balancy-webview-test-page");
            
            if (testPageAsset == null)
            {
                LogStatus("Test page not found in Resources. Using default URL.");
                yield break;
            }
            
            // Create a temporary file with the test page content
            _testPagePath = Path.Combine(Application.temporaryCachePath, "balancy-webview-test.html");
            
            try
            {
                // Inject the bridge code and write the file
                string htmlContent = testPageAsset.text;
                htmlContent = BalancyWebViewBridgeLoader.InjectBridgeIntoHtml(htmlContent);
                File.WriteAllText(_testPagePath, htmlContent);
                
                LogStatus("Test page prepared: " + _testPagePath);
            }
            catch (System.Exception e)
            {
                LogStatus("Error preparing test page: " + e.Message);
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Open the WebView
        /// </summary>
        public void OpenWebView()
        {
            if (_webView.IsWebViewOpen())
            {
                LogStatus("WebView is already open");
                return;
            }
            
            // Apply settings
            _webView.SetTransparentBackground(transparentBackground);
            _webView.SetDebugLogging(true);
            
            if (fullScreen)
                _webView.SetFullScreen(true);
            else
                _webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
            
            // Determine URL to open
            string urlToOpen;
            
            if (useLocalHtml && !string.IsNullOrEmpty(_testPagePath))
                urlToOpen = "file://" + _testPagePath;
            else
                urlToOpen = remoteUrl;
            
            // Open the WebView
            bool success = _webView.OpenWebView(urlToOpen);
            
            if (success)
                LogStatus("Opening WebView: " + urlToOpen);
            else
                LogStatus("Failed to open WebView");
            
            UpdateUI();
        }
        
        /// <summary>
        /// Close the WebView
        /// </summary>
        public void CloseWebView()
        {
            if (!_webView.IsWebViewOpen())
            {
                LogStatus("WebView is not open");
                return;
            }
            
            _webView.CloseWebView();
            LogStatus("WebView closed");
            
            UpdateUI();
        }
        
        /// <summary>
        /// Send a message to the WebView
        /// </summary>
        public void SendMessage()
        {
            if (!_webView.IsWebViewOpen())
            {
                LogStatus("WebView is not open");
                return;
            }
            
            string message;
            
            if (messageInput != null && !string.IsNullOrEmpty(messageInput.text))
            {
                message = messageInput.text;
            }
            else
            {
                // Default test message
                message = "{\"action\":\"test\",\"message\":\"Hello from Unity\",\"timestamp\":" + 
                    System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
            }
            
            bool success = _webView.SendMessage(message);
            
            if (success)
            {
                LogStatus("Message sent");
                AddToLog("To WebView: " + message);
                
                // Clear input field
                if (messageInput != null)
                    messageInput.text = "";
            }
            else
            {
                LogStatus("Failed to send message");
            }
        }
        
        /// <summary>
        /// Set the WebView background transparency
        /// </summary>
        public void SetTransparentBackground(bool transparent)
        {
            transparentBackground = transparent;
            
            if (_webView.IsWebViewOpen())
            {
                _webView.SetTransparentBackground(transparent);
                LogStatus("Background transparency: " + transparent);
            }
        }
        
        /// <summary>
        /// Set the WebView to full screen or custom viewport
        /// </summary>
        public void SetFullScreen(bool isFullScreen)
        {
            fullScreen = isFullScreen;
            
            if (_webView.IsWebViewOpen())
            {
                if (isFullScreen)
                {
                    _webView.SetFullScreen(true);
                    LogStatus("WebView set to full screen");
                }
                else
                {
                    _webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
                    LogStatus("WebView set to custom viewport");
                }
            }
        }
        
        /// <summary>
        /// Handle messages from the WebView
        /// </summary>
        private void HandleWebViewMessage(string message)
        {
            AddToLog("From WebView: " + message);
        }
        
        /// <summary>
        /// Handle WebView load completed event
        /// </summary>
        private void HandleLoadCompleted(bool success)
        {
            if (success)
                LogStatus("WebView loaded successfully");
            else
                LogStatus("WebView failed to load");
        }
        
        /// <summary>
        /// Handle WebView closed event
        /// </summary>
        private void HandleClosed()
        {
            LogStatus("WebView closed");
            UpdateUI();
        }
        
        /// <summary>
        /// Log a status message
        /// </summary>
        private void LogStatus(string message)
        {
            Debug.Log("[BalancyWebViewTester] " + message);
            
            if (statusText != null)
                statusText.text = message;
        }
        
        /// <summary>
        /// Add a message to the log display
        /// </summary>
        private void AddToLog(string message)
        {
            _logLines.Add(message);
            
            // Keep log at max size
            while (_logLines.Count > maxLogLines)
                _logLines.RemoveAt(0);
            
            // Update the log text
            if (messageLog != null)
                messageLog.text = string.Join("\n", _logLines);
        }
        
        /// <summary>
        /// Update UI state based on WebView state
        /// </summary>
        private void UpdateUI()
        {
            bool isOpen = _webView.IsWebViewOpen();
            
            if (openButton != null)
                openButton.interactable = !isOpen;
            
            if (closeButton != null)
                closeButton.interactable = isOpen;
            
            if (sendMessageButton != null)
                sendMessageButton.interactable = isOpen;
            
            if (messageInput != null)
                messageInput.interactable = isOpen;
        }
    }
}
