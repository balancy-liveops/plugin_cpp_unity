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
        
        [SerializeField]
        private RawImage webViewDisplay;

        [SerializeField]
        private AspectRatioFitter aspectRatioFitter;
        
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
            
            // Set the display target
            if (webViewDisplay != null)
            {
                BalancyWebView2.Instance.SetDisplayTarget(webViewDisplay);
            }

            // Configure aspect ratio if needed
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.aspectRatio = 16f / 9f; // Default to 16:9
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
            
            // Set up WebView events
            BalancyWebView2.Instance.OnMessage += OnWebViewMessage;
            BalancyWebView2.Instance.OnLoadCompleted += OnWebViewLoadCompleted;
            BalancyWebView2.Instance.OnCacheCompleted += OnWebViewCacheCompleted;
            
            // Enable caching
            BalancyWebView2.Instance.SetOfflineCacheEnabled(true);
        }
        
        private void OnDestroy()
        {
            // Clean up event subscriptions
            if (BalancyWebView2.Instance != null)
            {
                BalancyWebView2.Instance.OnMessage -= OnWebViewMessage;
                BalancyWebView2.Instance.OnLoadCompleted -= OnWebViewLoadCompleted;
                BalancyWebView2.Instance.OnCacheCompleted -= OnWebViewCacheCompleted;
            }
        }
        
        private void OpenWebView()
        {
            string url = urlInputField.text;
            bool success = BalancyWebView2.Instance.OpenWebView(url);
            Log($"WebView opened: {success}");
        }
        
        private void CloseWebView()
        {
            BalancyWebView2.Instance.CloseWebView();
            Log("WebView closed");
        }
        
        private void SendMessage()
        {
            string message = $"{{\"action\":\"ping\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeSeconds()}}}";
            bool success = BalancyWebView2.Instance.SendMessage(message);
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