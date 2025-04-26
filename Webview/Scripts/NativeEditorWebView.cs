using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Balancy.WebView
{
    public class NativeEditorWebView : MonoBehaviour
    {
        #if UNITY_EDITOR_OSX
        // Import functions from the native plugin
        [DllImport("WebViewPlugin")]
        private static extern IntPtr _CreateWebView(string url, int x, int y, int width, int height);
        
        [DllImport("WebViewPlugin")]
        private static extern void _CloseWebView();
        
        [DllImport("WebViewPlugin")]
        private static extern bool _LoadURL(string url);
        
        [DllImport("WebViewPlugin")]
        private static extern bool _ExecuteJavaScript(string script);
        #endif
        
        // Events
        public event Action<string> OnMessage;
        public event Action<bool> OnLoadCompleted;
        public event Action<bool> OnCacheCompleted;
        
        // WebView state
        private bool isWebViewOpen = false;
        private string currentUrl = "";
        
        // Initialize the WebView
        public void Initialize() {
            // No initialization needed for native WebView
        }
        
        // Open WebView with a URL
        public bool OpenWebView(string url, int width = 800, int height = 600) {
            #if UNITY_EDITOR_OSX
            if (isWebViewOpen) {
                CloseWebView();
            }
            
            // Position the window near the game view
            int x = Screen.width / 4;
            int y = Screen.height / 4;
            
            // Create the WebView
            IntPtr webViewPtr = _CreateWebView(url, x, y, width, height);
            
            if (webViewPtr != IntPtr.Zero) {
                isWebViewOpen = true;
                currentUrl = url;
                
                // Simulate load completed event
                OnLoadCompleted?.Invoke(true);
                
                return true;
            }
            return false;
            #else
            Debug.LogWarning("NativeEditorWebView is only supported in macOS Editor");
            return false;
            #endif
        }
        
        // Close the WebView
        public void CloseWebView() {
            #if UNITY_EDITOR_OSX
            if (isWebViewOpen) {
                _CloseWebView();
                isWebViewOpen = false;
            }
            #endif
        }
        
        // Send a message to the WebView
        public bool SendMessage(string message) {
            #if UNITY_EDITOR_OSX
            if (!isWebViewOpen)
                return false;
                
            // Create a JavaScript call to dispatch an event
            string escapedMessage = message.Replace("\"", "\\\"").Replace("'", "\\'");
            string jsCode = 
                "var event = new CustomEvent('balancyMessage', { detail: '" + escapedMessage + "' });" +
                "document.dispatchEvent(event);";
                
            return _ExecuteJavaScript(jsCode);
            #else
            return false;
            #endif
        }
        
        // Enable/disable caching
        public void SetOfflineCacheEnabled(bool enabled) {
            // Caching is handled by the native WebKit
            OnCacheCompleted?.Invoke(true);
        }
        
        // Clean up resources when destroyed
        private void OnDestroy() {
            CloseWebView();
        }
    }
}