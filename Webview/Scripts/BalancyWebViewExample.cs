using UnityEngine;
using UnityEngine.UI;
using Balancy.WebView;

public class BalancyWebViewExample : MonoBehaviour
{
    [SerializeField] 
    private string url = "https://example.com";
    
    [SerializeField] 
    private Button openButton;
    
    [SerializeField] 
    private Button closeButton;
    
    [SerializeField] 
    private Button sendMessageButton;
    
    [SerializeField] 
    private Toggle offlineCacheToggle;
    
    [SerializeField]
    private Text statusText;
    
    private bool webViewOpen = false;
    
    private void Start()
    {
        Debug.Log("WebView Example starting...");
        
        // Set up event listeners with error handling
        try
        {
            BalancyWebView.Instance.OnMessage += OnMessageReceived;
            BalancyWebView.Instance.OnLoadCompleted += OnLoadCompleted;
            BalancyWebView.Instance.OnCacheCompleted += OnCacheCompleted;
            
            // Set up UI buttons
            if (openButton != null)
            {
                openButton.onClick.AddListener(OpenWebView);
            }
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseWebView);
            }
            
            if (sendMessageButton != null)
            {
                sendMessageButton.onClick.AddListener(SendTestMessage);
            }
            
            if (offlineCacheToggle != null)
            {
                offlineCacheToggle.onValueChanged.AddListener(SetOfflineCacheEnabled);
            }
            
            UpdateStatus("WebView ready");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error initializing WebView example: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }
    
    private void OnDestroy()
    {
        // Remove event listeners
        try
        {
            if (BalancyWebView.Instance != null)
            {
                BalancyWebView.Instance.OnMessage -= OnMessageReceived;
                BalancyWebView.Instance.OnLoadCompleted -= OnLoadCompleted;
                BalancyWebView.Instance.OnCacheCompleted -= OnCacheCompleted;
                
                // Close the WebView if still open
                if (webViewOpen)
                {
                    BalancyWebView.Instance.CloseWebView();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error cleaning up WebView example: {ex.Message}");
        }
    }
    
    private void OpenWebView()
    {
        try
        {
            Debug.Log("Attempting to open WebView...");
            UpdateStatus("Opening WebView...");
            
            bool success = BalancyWebView.Instance.OpenWebView(url);
            if (success)
            {
                webViewOpen = true;
                UpdateStatus("WebView opened");
            }
            else
            {
                UpdateStatus("Failed to open WebView");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error opening WebView: {ex.Message}");
            UpdateStatus($"Error opening: {ex.Message}");
        }
    }
    
    private void CloseWebView()
    {
        try
        {
            UpdateStatus("Closing WebView...");
            BalancyWebView.Instance.CloseWebView();
            webViewOpen = false;
            UpdateStatus("WebView closed");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error closing WebView: {ex.Message}");
            UpdateStatus($"Error closing: {ex.Message}");
        }
    }
    
    private void SendTestMessage()
    {
        try
        {
            string message = $"{{\"action\":\"test\",\"data\":{{\"timestamp\":{Time.time}}}}}";
            UpdateStatus("Sending message: " + message);
            
            bool success = BalancyWebView.Instance.SendMessage(message);
            if (success)
            {
                UpdateStatus("Message sent");
            }
            else
            {
                UpdateStatus("Failed to send message");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error sending message: {ex.Message}");
            UpdateStatus($"Error sending: {ex.Message}");
        }
    }
    
    private void SetOfflineCacheEnabled(bool enabled)
    {
        try
        {
            UpdateStatus($"Setting cache: {enabled}");
            BalancyWebView.Instance.SetOfflineCacheEnabled(enabled);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error setting cache: {ex.Message}");
            UpdateStatus($"Error setting cache: {ex.Message}");
        }
    }
    
    // Event handlers
    private void OnMessageReceived(string message)
    {
        Debug.Log($"[WebView Example] Message received: {message}");
        UpdateStatus($"Received: {message}");
    }
    
    private void OnLoadCompleted(bool success)
    {
        Debug.Log($"[WebView Example] Load completed: {success}");
        UpdateStatus($"Load completed: {success}");
    }
    
    private void OnCacheCompleted(bool success)
    {
        Debug.Log($"[WebView Example] Cache completed: {success}");
        UpdateStatus($"Cache set: {success}");
    }
    
    // Helper to update status text
    private void UpdateStatus(string status)
    {
        Debug.Log($"WebView status: {status}");
        
        if (statusText != null)
        {
            statusText.text = status;
        }
    }
}