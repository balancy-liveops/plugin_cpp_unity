using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Balancy.WebView
{
    /// <summary>
    /// Implementation of a WebView that works in the Unity Editor Game view
    /// </summary>
    public class EditorWebViewImplementation : MonoBehaviour
    {
        // Prefab references
        private GameObject webViewPanel;
        private RawImage webViewImage;
        private Button closeButton;
        
        // WebView state
        private string currentUrl;
        private Texture2D webViewTexture;
        private bool isWebViewVisible = false;
        private int webViewWidth = 800;
        private int webViewHeight = 600;
        
        // Events
        public event Action<string> OnMessage;
        public event Action<bool> OnLoadCompleted;
        public event Action<bool> OnCacheCompleted;
        
        // JavaScript bridge (in a real implementation this would communicate with the web content)
        private string lastReceivedMessage = null;
        
        // Create the UI elements for the WebView
        public void Initialize(Transform parentTransform = null)
        {
            // Create webview container
            webViewPanel = new GameObject("WebViewPanel", typeof(RectTransform));
            if (parentTransform != null)
                webViewPanel.transform.SetParent(parentTransform, false);
            else
                webViewPanel.transform.SetParent(GameObject.Find("Canvas").transform, false);
            
            RectTransform rectTransform = webViewPanel.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add panel background
            Image panelImage = webViewPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            
            // Create web content display
            GameObject webContent = new GameObject("WebContent", typeof(RectTransform));
            webContent.transform.SetParent(webViewPanel.transform, false);
            
            RectTransform webContentRect = webContent.GetComponent<RectTransform>();
            webContentRect.anchorMin = new Vector2(0.1f, 0.1f);
            webContentRect.anchorMax = new Vector2(0.9f, 0.9f);
            webContentRect.offsetMin = Vector2.zero;
            webContentRect.offsetMax = Vector2.zero;
            
            webViewImage = webContent.AddComponent<RawImage>();
            
            // Create a simple texture to represent web content
            webViewTexture = new Texture2D(webViewWidth, webViewHeight);
            ClearWebViewTexture();
            webViewImage.texture = webViewTexture;
            
            // Add close button
            GameObject closeButtonObj = new GameObject("CloseButton", typeof(RectTransform));
            closeButtonObj.transform.SetParent(webViewPanel.transform, false);
            
            RectTransform closeButtonRect = closeButtonObj.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = new Vector2(0.95f, 0.95f);
            closeButtonRect.anchorMax = new Vector2(0.99f, 0.99f);
            closeButtonRect.offsetMin = Vector2.zero;
            closeButtonRect.offsetMax = Vector2.zero;
            
            closeButton = closeButtonObj.AddComponent<Button>();
            Image closeButtonImage = closeButtonObj.AddComponent<Image>();
            closeButtonImage.color = Color.red;
            
            // Add text to close button
            GameObject closeText = new GameObject("CloseText", typeof(RectTransform));
            closeText.transform.SetParent(closeButtonObj.transform, false);
            
            RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;
            
            Text text = closeText.AddComponent<Text>();
            text.text = "X";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            
            // Set up close button click handler
            closeButton.onClick.AddListener(() => {
                CloseWebView();
            });
            
            // Hide web view initially
            webViewPanel.SetActive(false);
        }
        
        // Fill texture with a background color
        private void ClearWebViewTexture()
        {
            Color[] pixels = new Color[webViewWidth * webViewHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.95f, 0.95f, 0.95f, 1.0f); // Light gray background
            }
            webViewTexture.SetPixels(pixels);
            webViewTexture.Apply();
        }
        
        // Load and display a URL
        public bool OpenWebView(string url, int width = 0, int height = 0)
        {
            if (webViewPanel == null)
                Initialize();
                
            currentUrl = url;
            
            if (width > 0) webViewWidth = width;
            if (height > 0) webViewHeight = height;
            
            // In a real implementation, this would load the web content
            // For now, we'll just display a simulated web page
            SimulateWebPageLoading();
            
            // Show the WebView panel
            webViewPanel.SetActive(true);
            isWebViewVisible = true;
            
            Debug.Log($"[EditorWebView] Opening URL: {url}");
            
            // Notify that the page has started loading
            StartCoroutine(SimulateLoadCompletion());
            
            return true;
        }
        
        // Close and hide the WebView
        public void CloseWebView()
        {
            if (webViewPanel != null)
            {
                webViewPanel.SetActive(false);
                isWebViewVisible = false;
                Debug.Log("[EditorWebView] WebView closed");
            }
        }
        
        // Send a message to the web content
        public bool SendMessage(string message)
        {
            if (!isWebViewVisible)
                return false;
                
            Debug.Log($"[EditorWebView] Sending message to web page: {message}");
            
            // Simulate the web page responding to the message
            StartCoroutine(SimulateMessageResponse(message));
            
            return true;
        }
        
        // Enable offline caching (simulated)
        public void SetOfflineCacheEnabled(bool enabled)
        {
            Debug.Log($"[EditorWebView] Offline caching set to: {enabled}");
            
            // Simulate cache completion
            if (enabled)
            {
                StartCoroutine(SimulateCacheCompletion());
            }
        }
        
        // Clean up resources when destroyed
        private void OnDestroy()
        {
            if (webViewTexture != null)
            {
                Destroy(webViewTexture);
            }
        }
        
        #region Simulation Methods
        
        // Draw a simple web page on the texture
        private void SimulateWebPageLoading()
        {
            // Clear the texture
            ClearWebViewTexture();
            
            // Draw a simulated web page
            int headerHeight = 50;
            int footerHeight = 30;
            
            // Draw header
            DrawRect(0, 0, webViewWidth, headerHeight, new Color(0.2f, 0.3f, 0.8f));
            DrawText(10, 15, "Balancy WebView Simulation", Color.white);
            
            // Draw page title based on URL
            string title = "Page Title: " + (currentUrl.Length > 30 ? currentUrl.Substring(0, 30) + "..." : currentUrl);
            DrawText(10, 70, title, Color.black);
            
            // Draw content area
            DrawRect(20, 100, webViewWidth - 40, webViewHeight - 150, new Color(0.9f, 0.9f, 0.9f));
            DrawText(30, 110, "Web Content Simulation", Color.black);
            DrawText(30, 140, "URL: " + currentUrl, Color.black);
            
            // Draw footer
            DrawRect(0, webViewHeight - footerHeight, webViewWidth, footerHeight, new Color(0.2f, 0.2f, 0.2f));
            DrawText(10, webViewHeight - 20, "© " + DateTime.Now.Year + " Balancy WebView", Color.white);
            
            // Apply changes to texture
            webViewTexture.Apply();
        }
        
        // Draw a rectangle on the texture
        private void DrawRect(int x, int y, int width, int height, Color color)
        {
            for (int i = x; i < x + width && i < webViewWidth; i++)
            {
                for (int j = y; j < y + height && j < webViewHeight; j++)
                {
                    webViewTexture.SetPixel(i, j, color);
                }
            }
        }
        
        // Draw text on the texture (simplified)
        private void DrawText(int x, int y, string text, Color color)
        {
            // This is a very simple simulation that just fills in pixels
            // In a real implementation you'd use proper text rendering
            int pixelSize = 2;
            int letterWidth = 8 * pixelSize;
            int letterSpacing = 2 * pixelSize;
            
            for (int i = 0; i < text.Length; i++)
            {
                int letterX = x + i * (letterWidth + letterSpacing);
                if (letterX > webViewWidth - letterWidth) break;
                
                for (int px = 0; px < letterWidth; px++)
                {
                    for (int py = 0; py < letterWidth; py++)
                    {
                        int pixelX = letterX + px;
                        int pixelY = y + py;
                        
                        if (pixelX >= 0 && pixelX < webViewWidth && 
                            pixelY >= 0 && pixelY < webViewHeight)
                        {
                            // Create a simple pattern for each letter
                            bool isPixelOn = (px + py) % pixelSize == 0;
                            if (isPixelOn)
                            {
                                webViewTexture.SetPixel(pixelX, pixelY, color);
                            }
                        }
                    }
                }
            }
        }
        
        // Simulate page load completion
        private IEnumerator SimulateLoadCompletion()
        {
            // Simulate loading time
            yield return new WaitForSeconds(1.5f);
            
            // Invoke load completed event
            OnLoadCompleted?.Invoke(true);
            Debug.Log($"[EditorWebView] Page loaded: {currentUrl}");
        }
        
        // Simulate cache completion
        private IEnumerator SimulateCacheCompletion()
        {
            // Simulate caching time
            yield return new WaitForSeconds(2.0f);
            
            // Invoke cache completed event
            OnCacheCompleted?.Invoke(true);
            Debug.Log("[EditorWebView] Cache completed");
        }
        
        // Simulate a response from the web page
        private IEnumerator SimulateMessageResponse(string message)
        {
            // Simulate processing time
            yield return new WaitForSeconds(0.5f);
            
            // Create a response
            string response = $"{{\"response\":\"Web page received message\",\"originalMessage\":{message},\"timestamp\":{DateTimeOffset.Now.ToUnixTimeSeconds()}}}";
            
            // Invoke message received event
            OnMessage?.Invoke(response);
            Debug.Log($"[EditorWebView] Response received: {response}");
        }
        
        #endregion
    }
}
