using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;
using System.Text.RegularExpressions;

namespace Balancy.WebView
{
    /// <summary>
    /// Enhanced WebView implementation for Unity Editor that can actually load and display web content
    /// </summary>
    public class EnhancedEditorWebView : MonoBehaviour
    {
        [Serializable]
        public class WebViewStyle
        {
            public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            public Color headerColor = new Color(0.1f, 0.1f, 0.3f, 1.0f);
            public Color textColor = Color.white;
            public Color webBackgroundColor = Color.white;
            public int headerHeight = 40;
            public Font font;
        }
        
        // Canvas references
        private GameObject webViewPanel;
        private RectTransform panelRectTransform;
        private RawImage webContentImage;
        private Button closeButton;
        private Text titleText;
        private GameObject loadingIndicator;
        private ScrollRect scrollRect;
        
        // WebView state
        private string currentUrl;
        private Texture2D webContentTexture;
        private bool isWebViewVisible = false;
        private bool isLoading = false;
        private int webViewWidth = 800;
        private int webViewHeight = 600;
        
        // Style
        public WebViewStyle style = new WebViewStyle();
        
        // Events
        public event Action<string> OnMessage;
        public event Action<bool> OnLoadCompleted;
        public event Action<bool> OnCacheCompleted;
        
        // Cache settings
        private bool cacheEnabled = false;
        private string cachePath => Path.Combine(Application.temporaryCachePath, "WebViewCache");
        
        // JavaScript bridge
        private string lastReceivedMessage = null;
        
        // Create the UI elements for the WebView
        public void Initialize(Transform parentTransform = null)
        {
            // Ensure we have a canvas to attach to
            Canvas canvas = null;
            if (parentTransform != null && parentTransform.GetComponentInParent<Canvas>() != null)
                canvas = parentTransform.GetComponentInParent<Canvas>();
            else
            {
                canvas = FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    // Create a new canvas if none exists
                    GameObject canvasObj = new GameObject("WebViewCanvas");
                    canvas = canvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }
            
            // Create web view panel
            webViewPanel = new GameObject("WebViewPanel", typeof(RectTransform));
            webViewPanel.transform.SetParent(canvas.transform, false);
            
            panelRectTransform = webViewPanel.GetComponent<RectTransform>();
            panelRectTransform.anchorMin = new Vector2(0.1f, 0.1f);
            panelRectTransform.anchorMax = new Vector2(0.9f, 0.9f);
            panelRectTransform.offsetMin = Vector2.zero;
            panelRectTransform.offsetMax = Vector2.zero;
            
            // Add panel background
            Image panelImage = webViewPanel.AddComponent<Image>();
            panelImage.color = style.backgroundColor;
            
            // Add header
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(webViewPanel.transform, false);
            
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, style.headerHeight);
            
            Image headerImage = header.AddComponent<Image>();
            headerImage.color = style.headerColor;
            
            // Add title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(header.transform, false);
            
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.8f, 1);
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, 0);
            
            titleText = titleObj.AddComponent<Text>();
            titleText.text = "WebView";
            titleText.font = style.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 16;
            titleText.color = style.textColor;
            titleText.alignment = TextAnchor.MiddleLeft;
            
            // Create scroll view for web content
            GameObject scrollView = new GameObject("ScrollView", typeof(RectTransform));
            scrollView.transform.SetParent(webViewPanel.transform, false);
            
            RectTransform scrollViewRect = scrollView.GetComponent<RectTransform>();
            scrollViewRect.anchorMin = new Vector2(0, 0);
            scrollViewRect.anchorMax = new Vector2(1, 1);
            scrollViewRect.offsetMin = new Vector2(0, 0);
            scrollViewRect.offsetMax = new Vector2(0, -style.headerHeight);
            
            scrollRect = scrollView.AddComponent<ScrollRect>();
            Image scrollViewImage = scrollView.AddComponent<Image>();
            scrollViewImage.color = style.webBackgroundColor;
            
            // Create content container
            GameObject contentContainer = new GameObject("Content", typeof(RectTransform));
            contentContainer.transform.SetParent(scrollView.transform, false);
            
            RectTransform contentRect = contentContainer.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.sizeDelta = Vector2.zero;
            contentRect.pivot = new Vector2(0.5f, 1); // Top center pivot for proper scrolling
            
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            
            // Add web content image
            GameObject webContent = new GameObject("WebContent", typeof(RectTransform));
            webContent.transform.SetParent(contentContainer.transform, false);
            
            RectTransform webContentRect = webContent.GetComponent<RectTransform>();
            webContentRect.anchorMin = new Vector2(0, 1);
            webContentRect.anchorMax = new Vector2(1, 1);
            webContentRect.pivot = new Vector2(0.5f, 1);
            webContentRect.sizeDelta = new Vector2(0, 800); // Initial height, will adjust based on content
            
            webContentImage = webContent.AddComponent<RawImage>();
            
            // Create a texture to display web content
            webContentTexture = new Texture2D(webViewWidth, webViewHeight);
            webContentTexture.filterMode = FilterMode.Bilinear;
            ClearWebViewTexture();
            webContentImage.texture = webContentTexture;
            
            // Add close button
            GameObject closeButtonObj = new GameObject("CloseButton", typeof(RectTransform));
            closeButtonObj.transform.SetParent(header.transform, false);
            
            RectTransform closeButtonRect = closeButtonObj.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = new Vector2(1, 0);
            closeButtonRect.anchorMax = new Vector2(1, 1);
            closeButtonRect.pivot = new Vector2(1, 0.5f);
            closeButtonRect.sizeDelta = new Vector2(style.headerHeight, 0);
            
            closeButton = closeButtonObj.AddComponent<Button>();
            Image closeButtonImage = closeButtonObj.AddComponent<Image>();
            closeButtonImage.color = new Color(0.8f, 0.2f, 0.2f, 1.0f);
            
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
            text.font = style.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 20;
            
            // Set up close button click handler
            closeButton.onClick.AddListener(() => {
                CloseWebView();
            });
            
            // Add loading indicator
            loadingIndicator = new GameObject("LoadingIndicator", typeof(RectTransform));
            loadingIndicator.transform.SetParent(webViewPanel.transform, false);
            
            RectTransform loadingRect = loadingIndicator.GetComponent<RectTransform>();
            loadingRect.anchorMin = Vector2.zero;
            loadingRect.anchorMax = Vector2.one;
            loadingRect.offsetMin = new Vector2(0, 0);
            loadingRect.offsetMax = new Vector2(0, -style.headerHeight);
            
            Image loadingBg = loadingIndicator.AddComponent<Image>();
            loadingBg.color = new Color(0, 0, 0, 0.5f);
            
            // Add loading text
            GameObject loadingText = new GameObject("LoadingText", typeof(RectTransform));
            loadingText.transform.SetParent(loadingIndicator.transform, false);
            
            RectTransform loadingTextRect = loadingText.GetComponent<RectTransform>();
            loadingTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingTextRect.sizeDelta = new Vector2(200, 50);
            
            Text loadingTextComponent = loadingText.AddComponent<Text>();
            loadingTextComponent.text = "Loading...";
            loadingTextComponent.alignment = TextAnchor.MiddleCenter;
            loadingTextComponent.color = Color.white;
            loadingTextComponent.font = style.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            loadingTextComponent.fontSize = 24;
            
            // Hide web view initially
            webViewPanel.SetActive(false);
            loadingIndicator.SetActive(false);
            
            // Create cache directory if enabled
            if (cacheEnabled && !Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }
        }
        
        private void ClearWebViewTexture()
        {
            Color[] pixels = new Color[webViewWidth * webViewHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = style.webBackgroundColor;
            }
            webContentTexture.SetPixels(pixels);
            webContentTexture.Apply();
        }
        
        // Load and display a URL
        public bool OpenWebView(string url, int width = 0, int height = 0)
        {
            if (webViewPanel == null)
                Initialize();
                
            currentUrl = url;
            
            if (width > 0) webViewWidth = width;
            if (height > 0) webViewHeight = height;
            
            // Update title
            titleText.text = ShortenUrl(url);
            
            // Show the WebView panel
            webViewPanel.SetActive(true);
            isWebViewVisible = true;
            
            // Start loading the web content
            StartCoroutine(LoadWebContent(url));
            
            Debug.Log($"[EnhancedEditorWebView] Opening URL: {url}");
            
            return true;
        }
        
        // Close and hide the WebView
        public void CloseWebView()
        {
            if (webViewPanel != null)
            {
                webViewPanel.SetActive(false);
                isWebViewVisible = false;
                StopAllCoroutines();
                Debug.Log("[EnhancedEditorWebView] WebView closed");
            }
        }
        
        // Send a message to the web content
        public bool SendMessage(string message)
        {
            if (!isWebViewVisible)
                return false;
                
            Debug.Log($"[EnhancedEditorWebView] Sending message to web page: {message}");
            
            // Simulate the web page responding to the message
            StartCoroutine(SimulateMessageResponse(message));
            
            return true;
        }
        
        // Enable offline caching (simulated)
        public void SetOfflineCacheEnabled(bool enabled)
        {
            cacheEnabled = enabled;
            Debug.Log($"[EnhancedEditorWebView] Offline caching set to: {enabled}");
            
            if (enabled && !Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }
            
            // Notify of cache status
            StartCoroutine(SimulateCacheCompletion());
        }
        
        // Clean up resources when destroyed
        private void OnDestroy()
        {
            if (webContentTexture != null)
            {
                Destroy(webContentTexture);
            }
        }
        
        // Load web content using UnityWebRequest
        private IEnumerator LoadWebContent(string url)
        {
            isLoading = true;
            loadingIndicator.SetActive(true);
            
            // Check if we have this page cached
            string cacheFile = GetCacheFilePath(url);
            bool useCache = cacheEnabled && File.Exists(cacheFile);
            
            if (useCache)
            {
                Debug.Log($"[EnhancedEditorWebView] Loading from cache: {url}");
                byte[] cachedData = File.ReadAllBytes(cacheFile);
                
                // If it's an image, load it as a texture
                if (IsImageUrl(url))
                {
                    yield return LoadImageContent(cachedData);
                }
                else
                {
                    // Otherwise treat as HTML
                    string htmlContent = System.Text.Encoding.UTF8.GetString(cachedData);
                    RenderHtmlContent(htmlContent);
                }
            }
            else
            {
                // Not cached, load from web
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
                {
                    yield return webRequest.SendWebRequest();
                    
                    if (webRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[EnhancedEditorWebView] Error loading URL: {webRequest.error}");
                        RenderErrorPage(url, webRequest.error);
                        
                        isLoading = false;
                        loadingIndicator.SetActive(false);
                        OnLoadCompleted?.Invoke(false);
                        yield break;
                    }
                    
                    // Cache the result if caching is enabled
                    if (cacheEnabled)
                    {
                        File.WriteAllBytes(cacheFile, webRequest.downloadHandler.data);
                    }
                    
                    // Handle content based on type
                    string contentType = webRequest.GetResponseHeader("Content-Type");
                    
                    if (contentType != null && contentType.StartsWith("image/"))
                    {
                        yield return LoadImageContent(webRequest.downloadHandler.data);
                    }
                    else
                    {
                        // Default to HTML rendering
                        string htmlContent = webRequest.downloadHandler.text;
                        RenderHtmlContent(htmlContent);
                    }
                }
            }
            
            // Notify loading complete
            isLoading = false;
            loadingIndicator.SetActive(false);
            OnLoadCompleted?.Invoke(true);
        }
        
        // Load and display an image
        private IEnumerator LoadImageContent(byte[] imageData)
        {
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageData))
            {
                // Resize our texture to fit the image
                webContentTexture = texture;
                webContentImage.texture = webContentTexture;
                
                // Resize content area to fit image
                RectTransform contentRect = webContentImage.GetComponent<RectTransform>();
                float aspectRatio = (float)texture.width / texture.height;
                float width = contentRect.rect.width;
                float height = width / aspectRatio;
                contentRect.sizeDelta = new Vector2(0, height);
                
                yield return null;
            }
            else
            {
                Debug.LogError("[EnhancedEditorWebView] Failed to load image data");
                RenderErrorPage(currentUrl, "Failed to load image");
            }
        }
        
        // Render HTML content (simplified)
        private void RenderHtmlContent(string htmlContent)
        {
            // Extract title from HTML if available
            string title = ExtractTitle(htmlContent);
            if (!string.IsNullOrEmpty(title))
            {
                titleText.text = title;
            }
            
            // Very simplified HTML rendering
            // In a real implementation, you'd use a proper HTML renderer
            string textContent = StripHtml(htmlContent);
            
            // Create a new texture to render the HTML
            int width = webViewWidth;
            int height = Mathf.Max(800, CalculateTextHeight(textContent, width)); 
            
            if (webContentTexture.width != width || webContentTexture.height != height)
            {
                Destroy(webContentTexture);
                webContentTexture = new Texture2D(width, height);
                webContentImage.texture = webContentTexture;
            }
            
            // Fill with white background
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = style.webBackgroundColor;
            }
            webContentTexture.SetPixels(pixels);
            
            // Render text content
            RenderTextToTexture(textContent, webContentTexture);
            
            // Update UI
            RectTransform contentRect = webContentImage.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, height);
        }
        
        // Render error page
        private void RenderErrorPage(string url, string error)
        {
            int width = webViewWidth;
            int height = 400;
            
            if (webContentTexture.width != width || webContentTexture.height != height)
            {
                Destroy(webContentTexture);
                webContentTexture = new Texture2D(width, height);
                webContentImage.texture = webContentTexture;
            }
            
            // Fill with light pink background for error
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1.0f, 0.9f, 0.9f, 1.0f);
            }
            webContentTexture.SetPixels(pixels);
            
            // Render error message
            string errorText = $"Error loading: {url}\n\n{error}";
            RenderTextToTexture(errorText, webContentTexture);
            
            // Update UI
            RectTransform contentRect = webContentImage.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, height);
        }
        
        // Render text to texture (simplified)
        private void RenderTextToTexture(string text, Texture2D texture)
        {
            int width = texture.width;
            int lineHeight = 20;
            int margin = 20;
            int x = margin;
            int y = margin;
            
            // Split text into lines
            List<string> lines = WrapText(text, width - margin * 2);
            foreach (string line in lines)
            {
                if (y + lineHeight >= texture.height)
                    break;
                
                DrawTextLine(line, x, y, texture);
                y += lineHeight;
            }
            
            texture.Apply();
        }
        
        // Draw a line of text on the texture (very simplified)
        private void DrawTextLine(string line, int x, int y, Texture2D texture)
        {
            // Very simplified text rendering - just draws pixels
            // In a real implementation, you'd use Unity's text rendering
            for (int i = 0; i < line.Length; i++)
            {
                int charX = x + i * 9; // Fixed width font
                if (charX > texture.width - 10) break;
                
                DrawChar(line[i], charX, y, texture);
            }
        }
        
        // Draw a character (extremely simplified)
        private void DrawChar(char c, int x, int y, Texture2D texture)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;
                
            // Just draw a simple block for each character
            int charSize = 8;
            for (int px = 0; px < charSize; px++)
            {
                for (int py = 0; py < charSize; py++)
                {
                    int posX = x + px;
                    int posY = y + py;
                    
                    if (posX >= 0 && posX < texture.width && posY >= 0 && posY < texture.height)
                    {
                        // Create a simple pattern based on the character
                        bool isPixelOn = (c % 2 == 0) ? ((px + py) % 2 == 0) : ((px * py) % 3 == 0);
                        Color pixelColor = isPixelOn ? Color.black : texture.GetPixel(posX, posY);
                        texture.SetPixel(posX, posY, pixelColor);
                    }
                }
            }
        }
        
        // Wrap text to fit width
        private List<string> WrapText(string text, int width)
        {
            List<string> lines = new List<string>();
            string[] words = text.Split(' ');
            string currentLine = "";
            int charWidth = 9; // Simplified fixed width
            
            foreach (string word in words)
            {
                // Check if adding this word exceeds the width
                if ((currentLine.Length + word.Length + 1) * charWidth > width)
                {
                    // Add current line and start a new one
                    if (!string.IsNullOrEmpty(currentLine))
                        lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    // Add word to current line
                    if (string.IsNullOrEmpty(currentLine))
                        currentLine = word;
                    else
                        currentLine += " " + word;
                }
                
                // Handle newlines in the text
                if (word.Contains("\n"))
                {
                    int newlineIndex = word.IndexOf('\n');
                    if (newlineIndex >= 0)
                    {
                        lines.Add(currentLine);
                        currentLine = word.Substring(newlineIndex + 1);
                    }
                }
            }
            
            // Add the last line
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);
                
            return lines;
        }
        
        // Calculate approximate text height
        private int CalculateTextHeight(string text, int width)
        {
            List<string> lines = WrapText(text, width - 40); // Account for margins
            return lines.Count * 20 + 40; // 20px line height + margins
        }
        
        // Extract title from HTML
        private string ExtractTitle(string html)
        {
            Match match = Regex.Match(html, @"<title>(.+?)</title>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }
        
        // Strip HTML tags to get plain text
        private string StripHtml(string html)
        {
            // Replace common HTML entities
            html = html.Replace("&nbsp;", " ")
                       .Replace("&lt;", "<")
                       .Replace("&gt;", ">")
                       .Replace("&amp;", "&")
                       .Replace("&quot;", "\"");
                       
            // Replace line breaks
            html = html.Replace("<br>", "\n")
                       .Replace("<br/>", "\n")
                       .Replace("<br />", "\n")
                       .Replace("<p>", "\n")
                       .Replace("</p>", "\n");
                       
            // Remove all other HTML tags
            string text = Regex.Replace(html, @"<[^>]+>", string.Empty);
            
            // Replace consecutive whitespace
            text = Regex.Replace(text, @"\s+", " ");
            
            return text.Trim();
        }
        
        // Get cache file path for a URL
        private string GetCacheFilePath(string url)
        {
            // Create a filename based on the URL hash
            string hash = url.GetHashCode().ToString("X");
            return Path.Combine(cachePath, hash);
        }
        
        // Check if URL points to an image
        private bool IsImageUrl(string url)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            foreach (string ext in imageExtensions)
            {
                if (url.ToLower().EndsWith(ext))
                    return true;
            }
            return false;
        }
        
        // Shorten URL for display
        private string ShortenUrl(string url)
        {
            // Remove protocol
            if (url.StartsWith("http://"))
                url = url.Substring(7);
            else if (url.StartsWith("https://"))
                url = url.Substring(8);
                
            // Truncate if too long
            int maxLength = 50;
            if (url.Length > maxLength)
                url = url.Substring(0, maxLength - 3) + "...";
                
            return url;
        }
        
        #region Simulation Methods
        
        // Simulate a response from the web page
        private IEnumerator SimulateMessageResponse(string message)
        {
            // Simulate processing time
            yield return new WaitForSeconds(0.5f);
            
            // Create a response
            string response = $"{{\"response\":\"Web page received message\",\"originalMessage\":{message},\"timestamp\":{DateTimeOffset.Now.ToUnixTimeSeconds()}}}";
            
            // Invoke message received event
            OnMessage?.Invoke(response);
            Debug.Log($"[EnhancedEditorWebView] Response received: {response}");
        }
        
        // Simulate cache completion
        private IEnumerator SimulateCacheCompletion()
        {
            // Simulate caching time
            yield return new WaitForSeconds(1.0f);
            
            // Invoke cache completed event
            OnCacheCompleted?.Invoke(true);
            Debug.Log("[EnhancedEditorWebView] Cache completed");
        }
        
        #endregion
    }
}
