using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.WebView.Examples
{
    /// <summary>
    /// Example class demonstrating how to use the Balancy WebView plugin
    /// </summary>
    public class BalancyWebViewExample : MonoBehaviour
    {
        [Header("WebView Settings")]
        [SerializeField] private string webViewUrl = "https://example.com/webview-test.html";
        [SerializeField] private bool useLocalHtml = true;
        [SerializeField] private TextAsset localHtmlAsset;
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
        
        [Header("UI Elements")]
        [SerializeField] private Button openWebViewButton;
        [SerializeField] private Button closeWebViewButton;
        [SerializeField] private Button sendMessageButton;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Toggle transparentToggle;
        [SerializeField] private Toggle fullScreenToggle;
        
        // Reference to the WebView instance
        private BalancyWebView _webView;
        
        // Path for saving local HTML file
        private string _localHtmlPath;
        
        private void Awake()
        {
            // Initialize WebView reference
            _webView = BalancyWebView.Instance;
            
            // Register event handlers
            _webView.OnMessage += HandleWebViewMessage;
            _webView.OnLoadCompleted += HandleLoadCompleted;
            _webView.OnClosed += HandleWebViewClosed;
            
            // Set up UI button listeners
            if (openWebViewButton != null)
                openWebViewButton.onClick.AddListener(OpenWebView);
                
            if (closeWebViewButton != null)
                closeWebViewButton.onClick.AddListener(CloseWebView);
                
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
            
            // Create local HTML file if needed
            if (useLocalHtml && localHtmlAsset != null)
            {
                StartCoroutine(CreateLocalHtmlFile());
            }
            
            UpdateUIState();
        }
        
        private void OnDestroy()
        {
            // Unregister event handlers
            if (_webView != null)
            {
                _webView.OnMessage -= HandleWebViewMessage;
                _webView.OnLoadCompleted -= HandleLoadCompleted;
                _webView.OnClosed -= HandleWebViewClosed;
                
                // Close WebView if it's open
                if (_webView.IsWebViewOpen())
                {
                    _webView.CloseWebView();
                }
            }
        }
        
        /// <summary>
        /// Creates a local HTML file from the TextAsset
        /// </summary>
        private IEnumerator CreateLocalHtmlFile()
        {
            // Get persistent data path for this application
            string directoryPath = Application.persistentDataPath + "/WebView";
            
            // Create directory if it doesn't exist
            if (!System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }
            
            // Path for the HTML file
            _localHtmlPath = directoryPath + "/webview-test.html";
            
            // Write the HTML content to file
            System.IO.File.WriteAllText(_localHtmlPath, localHtmlAsset.text);
            
            LogStatus("Local HTML file created at: " + _localHtmlPath);
            
            yield return null;
        }
        
        /// <summary>
        /// Opens the WebView with current settings
        /// </summary>
        public void OpenWebView()
        {
            if (_webView.IsWebViewOpen())
            {
                LogStatus("WebView is already open");
                return;
            }
            
            // Apply settings before opening
            _webView.SetTransparentBackground(transparentBackground);
            
            if (fullScreen)
            {
                _webView.SetFullScreen(true);
            }
            else
            {
                _webView.SetViewportRect(viewportX, viewportY, viewportWidth, viewportHeight);
            }
            
            // Enable debug logging
            _webView.SetDebugLogging(true);
            
            // Determine URL to load
            string urlToLoad;
            
            if (useLocalHtml && !string.IsNullOrEmpty(_localHtmlPath))
            {
                urlToLoad = "file://" + _localHtmlPath;
            }
            else
            {
                urlToLoad = webViewUrl;
            }
            
            // Open the WebView
            bool success = _webView.OpenWebView(urlToLoad);
            
            if (success)
            {
                LogStatus("Opening WebView: " + urlToLoad);
            }
            else
            {
                LogStatus("Failed to open WebView");
            }
            
            UpdateUIState();
        }
        
        /// <summary>
        /// Closes the WebView
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
            
            UpdateUIState();
        }
        
        /// <summary>
        /// Sends a message to the WebView
        /// </summary>
        public void SendMessage()
        {
            if (!_webView.IsWebViewOpen())
            {
                LogStatus("WebView is not open");
                return;
            }
            
            string message = messageInputField != null ? messageInputField.text : "Hello from Unity!";
            
            if (string.IsNullOrEmpty(message))
            {
                message = "Hello from Unity!";
            }
            
            bool success = _webView.SendMessageToWebView(message);
            
            if (success)
            {
                LogStatus("Message sent: " + message);
                
                // Clear input field
                if (messageInputField != null)
                {
                    messageInputField.text = "";
                }
            }
            else
            {
                LogStatus("Failed to send message");
            }
        }
        
        /// <summary>
        /// Sends example JSON data to the WebView
        /// </summary>
        public void SendJsonData()
        {
            if (!_webView.IsWebViewOpen())
            {
                LogStatus("WebView is not open");
                return;
            }
            
            // Example JSON data
            string jsonData = JsonUtility.ToJson(new PlayerData {
                playerId = "player123",
                playerName = "JohnDoe",
                score = 1500,
                level = 5,
                items = new string[] { "sword", "shield", "potion" }
            });
            
            bool success = _webView.SendMessageToWebView(jsonData);
            
            if (success)
            {
                LogStatus("JSON data sent");
            }
            else
            {
                LogStatus("Failed to send JSON data");
            }
        }
        
        /// <summary>
        /// Sets the WebView background transparency
        /// </summary>
        public void SetTransparentBackground(bool transparent)
        {
            transparentBackground = transparent;
            
            if (_webView.IsWebViewOpen())
            {
                _webView.SetTransparentBackground(transparent);
                LogStatus("WebView transparency set to: " + transparent);
            }
        }
        
        /// <summary>
        /// Toggles between full screen and custom viewport
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
        /// Handle messages received from the WebView
        /// </summary>
        private void HandleWebViewMessage(string message)
        {
            LogStatus("Message from WebView: " + message);
            
            // Try to parse as JSON
            try
            {
                // Simple validation to check if it's JSON
                if (message.StartsWith("{") && message.EndsWith("}"))
                {
                    // Handle different message types based on action
                    // (In a real app you would use proper JSON parsing)
                    if (message.Contains("\"action\":\"getPlayerInfo\""))
                    {
                        // Send player info back to the WebView
                        SendPlayerInfo();
                    }
                    else if (message.Contains("\"action\":\"getGameState\""))
                    {
                        // Send game state back to the WebView
                        SendGameState();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error parsing WebView message: " + e.Message);
            }
        }
        
        /// <summary>
        /// Handle WebView load completed event
        /// </summary>
        private void HandleLoadCompleted(bool success)
        {
            if (success)
            {
                LogStatus("WebView loaded successfully");
                
                // You can send initial data to the WebView here
                SendPlayerInfo();
            }
            else
            {
                LogStatus("WebView failed to load");
            }
        }
        
        /// <summary>
        /// Handle WebView closed event
        /// </summary>
        private void HandleWebViewClosed()
        {
            LogStatus("WebView was closed");
            UpdateUIState();
        }
        
        /// <summary>
        /// Send player information to the WebView
        /// </summary>
        private void SendPlayerInfo()
        {
            string playerInfo = JsonUtility.ToJson(new PlayerData {
                playerId = "player123",
                playerName = "JohnDoe",
                score = 1500,
                level = 5,
                items = new string[] { "sword", "shield", "potion" }
            });
            
            _webView.SendMessageToWebView("{\"action\":\"playerInfo\",\"data\":" + playerInfo + "}");
        }
        
        /// <summary>
        /// Send game state to the WebView
        /// </summary>
        private void SendGameState()
        {
            string gameState = JsonUtility.ToJson(new GameState {
                currentLevel = 5,
                score = 12500,
                timeRemaining = 120,
                lives = 3
            });
            
            _webView.SendMessageToWebView("{\"action\":\"gameState\",\"data\":" + gameState + "}");
        }
        
        /// <summary>
        /// Log a status message to the UI
        /// </summary>
        private void LogStatus(string message)
        {
            Debug.Log("[BalancyWebViewExample] " + message);
            
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
        
        /// <summary>
        /// Update UI element states based on WebView state
        /// </summary>
        private void UpdateUIState()
        {
            bool isWebViewOpen = _webView.IsWebViewOpen();
            
            if (openWebViewButton != null)
                openWebViewButton.interactable = !isWebViewOpen;
                
            if (closeWebViewButton != null)
                closeWebViewButton.interactable = isWebViewOpen;
                
            if (sendMessageButton != null)
                sendMessageButton.interactable = isWebViewOpen;
                
            if (messageInputField != null)
                messageInputField.interactable = isWebViewOpen;
        }
    }
    
    /// <summary>
    /// Example player data class for JSON serialization
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public int score;
        public int level;
        public string[] items;
    }
    
    /// <summary>
    /// Example game state class for JSON serialization
    /// </summary>
    [Serializable]
    public class GameState
    {
        public int currentLevel;
        public int score;
        public int timeRemaining;
        public int lives;
    }
}
