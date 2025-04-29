using System;
using System.IO;
using UnityEngine;

namespace Balancy.WebView
{
    /// <summary>
    /// Helper class to load and manage the JavaScript bridge code
    /// </summary>
    public static class BalancyWebViewBridgeLoader
    {
        // Cache the bridge code
        private static string _bridgeCode;
        
        /// <summary>
        /// Get the JavaScript bridge code that will be injected into WebViews
        /// </summary>
        public static string GetBridgeCode()
        {
            if (string.IsNullOrEmpty(_bridgeCode))
            {
                LoadBridgeCode();
            }
            
            return _bridgeCode;
        }
        
        /// <summary>
        /// Load the bridge code from the Resources folder
        /// </summary>
        private static void LoadBridgeCode()
        {
            try
            {
                // Load the bridge code from the Resources folder
                TextAsset bridgeAsset = Resources.Load<TextAsset>("balancy-webview-bridge");
                
                if (bridgeAsset != null)
                {
                    _bridgeCode = bridgeAsset.text;
                    Debug.Log("[BalancyWebView] Bridge code loaded successfully");
                }
                else
                {
                    Debug.LogError("[BalancyWebView] Failed to load bridge code from Resources");
                    _bridgeCode = "// Bridge code not found";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BalancyWebView] Error loading bridge code: {e.Message}");
                _bridgeCode = "// Error loading bridge code";
            }
        }
        
        /// <summary>
        /// Inject the bridge code into a HTML page
        /// </summary>
        /// <param name="htmlContent">Original HTML content</param>
        /// <returns>HTML content with bridge code injected</returns>
        public static string InjectBridgeIntoHtml(string htmlContent)
        {
            string bridgeCode = GetBridgeCode();
            
            // Simple injection - insert before the closing head tag
            if (htmlContent.Contains("</head>"))
            {
                string scriptTag = $"<script type=\"text/javascript\">\n{bridgeCode}\n</script>";
                htmlContent = htmlContent.Replace("</head>", $"{scriptTag}\n</head>");
            }
            
            return htmlContent;
        }
        
        /// <summary>
        /// Create a complete HTML page with the bridge code injected
        /// </summary>
        /// <param name="content">HTML body content</param>
        /// <param name="title">Page title</param>
        /// <returns>Complete HTML page with bridge</returns>
        public static string CreateHtmlWithBridge(string content, string title = "Balancy WebView")
        {
            string bridgeCode = GetBridgeCode();
            
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
    <script type=""text/javascript"">
{bridgeCode}
    </script>
</head>
<body>
{content}
</body>
</html>";
        }
    }
}
