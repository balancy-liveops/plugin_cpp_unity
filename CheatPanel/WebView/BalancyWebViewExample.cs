using System;
using UnityEngine;
using UnityEngine.UI;
using Balancy.WebView;
using TMPro;

namespace Balancy.Examples
{
    public class BalancyWebViewExample : MonoBehaviour
    {
        [SerializeField] 
        private Button openWebViewButton;
        
        [SerializeField] 
        private Button closeWebViewButton;
        
        [SerializeField] 
        private Button sendMessageButton;
        
        [SerializeField] 
        private TMP_InputField urlInputField;
        
        [SerializeField] 
        private TMP_Text logText;
        
        private void Start()
        {
            // Set default URL
            if (string.IsNullOrEmpty(urlInputField.text))
            {
                urlInputField.text = "https://balancy.co";
            }
            
            // Set up button clicks
            openWebViewButton.onClick.AddListener(OpenWebView);
            closeWebViewButton.onClick.AddListener(CloseWebView);
            sendMessageButton.onClick.AddListener(SendMessage);
            
            // Set up WebView events
            BalancyWebView.Instance.OnMessage += OnWebViewMessage;
            BalancyWebView.Instance.OnLoadCompleted += OnWebViewLoadCompleted;
            BalancyWebView.Instance.OnCacheCompleted += OnWebViewCacheCompleted;
            
            // Enable caching
            BalancyWebView.Instance.SetOfflineCacheEnabled(true);
            
            Log("BalancyWebView initialized");
        }
        
        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (BalancyWebView.Instance != null)
            {
                BalancyWebView.Instance.OnMessage -= OnWebViewMessage;
                BalancyWebView.Instance.OnLoadCompleted -= OnWebViewLoadCompleted;
                BalancyWebView.Instance.OnCacheCompleted -= OnWebViewCacheCompleted;
            }
        }
        
        private void OpenWebView()
        {
            string url = urlInputField.text;
            Log($"Opening WebView with URL: {url}");
            
            bool success = BalancyWebView.Instance.OpenWebView(url);
            Log($"WebView opened: {success}");
        }
        
        private void CloseWebView()
        {
            Log("Closing WebView");
            BalancyWebView.Instance.CloseWebView();
        }
        
        private void SendMessage()
        {
            string message = $"{{\"action\":\"ping\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeSeconds()}}}";
            Log($"Sending message: {message}");
            
            bool success = BalancyWebView.Instance.SendMessage(message);
            Log($"Message sent: {success}");
        }
        
        private void OnWebViewMessage(string message)
        {
            Log($"Received message: {message}");
        }
        
        private void OnWebViewLoadCompleted(bool success)
        {
            Log($"WebView load completed: {success}");
        }
        
        private void OnWebViewCacheCompleted(bool success)
        {
            Log($"WebView cache completed: {success}");
        }
        
        private void Log(string message)
        {
            Debug.Log($"[BalancyWebView] {message}");
            logText.text = $"{DateTime.Now:HH:mm:ss}: {message}\n{logText.text}";
        }
    }
}